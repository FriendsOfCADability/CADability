using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using CADability.Attribute;
using CADability.GeoObject;
using System.Drawing;
using System.Windows.Forms;
using MathNet.Numerics.LinearAlgebra.Double;


namespace CADability.Forms
{
    /// <summary>
    /// Exception thrown when OpenGL-specific errors occur during initialization or rendering
    /// </summary>
    public class PaintToOpenGLException : ApplicationException
    {
        internal PaintToOpenGLException(String msg)
            : base(msg)
        {
            if (!Settings.GlobalSettings.GetBoolValue("DontUse.WindowsForms", false))
                MessageBox.Show(msg);
        }
    }
    
    /// <summary>
    /// Implementation of <see cref="IPaintTo3D"/> interface using OpenGL for hardware-accelerated 3D rendering.
    /// This class manages OpenGL contexts, display lists, textures, and the complete rendering pipeline.
    /// </summary>
    /// <remarks>
    /// Key Features:
    /// - Display list management for efficient rendering
    /// - Texture and bitmap caching
    /// - Font rendering with outline fonts
    /// - Error checking and resource tracking
    /// - Support for both window and bitmap rendering
    /// 
    /// Threading: OpenGL operations must occur on the thread that created the rendering context.
    /// Resource Management: Uses display lists for compiled geometry, automatically manages cleanup via GC.
    /// </remarks>
    public class PaintToOpenGL : IPaintTo3D
    {
        #region IPaintTo3D data
        public static int debugNumTriangles = 0;
        public static Thread MainThread = null;
        public static List<IntPtr> ContextsToDelete = new List<IntPtr>();
        public static List<IntPtr> activeRenderContexts = new List<IntPtr>();
        
        // Modern OpenGL integration
        private ModernOpenGLIntegration modernOpenGL;
        private ModernRenderingPath modernRenderingPath;
        private bool useModernRendering = false;
        
        bool paintSurfaces;
        bool paintEdges;
        bool paintSurfaceEdges;
        bool triangulateText = true;
        bool selectMode;
        bool delayText;
        bool delayAll;
        bool dontRecalcTriangulation;
        bool isBitmap; // Bitmaps verhalten sich komisch in Bezug auf DisplayListen (zumindest auf meinem Rechner, G.)
        double lineWidthFactor;
        float lineWidthMin, lineWidthMax;
        bool useLineWidth;
        double precision;
        double pixelToWorld;
        const int selectBufSize = 1000; // const ist immer auch static
        int[] selectBuf;
        int clientwidth, clientheight;
        struct state
        {   // der state wird in einem Stack gehalten und wieder restauriert
            // da können noch mehr Dinge dazukommen
            public bool useZBuffer;
            public bool blending;
        }
        Stack<state> stateStack;
        GeoVector projectionDirection;
        bool isPerspective;
        Color backgroundColor; // Die Hintergrundfarbe um sicherzustellen, dass nicht mit dieser farbe
                               // gezeichnet wird
        Color selectColor;
        bool colorOverride = false;

        // Glu.GLUnurbs nurbsRenderer;
        OpenGlList currentList;
        IntPtr deviceContext = IntPtr.Zero, renderContext = IntPtr.Zero;
        IntPtr controlHandle = IntPtr.Zero;
        Graphics graphics = null;
        byte accumBits = 0, colorBits = 32, depthBits = 16;
        static IntPtr MainRenderContext = IntPtr.Zero;
        static IntPtr LastRenderContext = IntPtr.Zero;
        private static List<Bitmap> bitmapList = null;
        internal static List<Bitmap> BitmapList
        {
            get
            {
                if (bitmapList == null)
                {
                    bitmapList = new List<Bitmap>();
                    Bitmap bmp;
                    bmp = BitmapTable.GetBitmap("PointSymbols.bmp");
                    // PointSymbols.bmp and PointSymbolsB.bmp (B for bold) must have this form:
                    // 6 square pointsymbols horizontally placed with and odd number of pixels.
                    // the upper left pixel is the transparent color
                    Color clr = bmp.GetPixel(0, 0);
                    if (clr.A != 0) bmp.MakeTransparent(clr);
                    int h = bmp.Height;
                    ImageList imageList = new ImageList();
                    imageList.ImageSize = new Size(h, h);
                    imageList.Images.AddStrip(bmp); // the non-bold symbols
                    if (Settings.GlobalSettings.GetBoolValue("PointSymbolsBold", false))
                    {
                        bmp = BitmapTable.GetBitmap("PointSymbolsB.bmp"); // the bold symbols
                        clr = bmp.GetPixel(0, 0);
                        if (clr.A != 0) bmp.MakeTransparent(clr);
                        imageList.Images.AddStrip(bmp);
                    }
                    else
                    {   // again the non bold symbols
                        imageList.Images.AddStrip(bmp);
                    }
                    // full black square for selecting
                    bmp = new Bitmap(h, h);
                    for (int i = 0; i < h; i++)
                    {
                        for (int j = 0; j < h; j++)
                        {
                            bmp.SetPixel(i, j, Color.Black);
                        }
                    }
                    imageList.Images.Add(bmp);
                    for (int i = 0; i < imageList.Images.Count; ++i)
                    {
                        bitmapList.Add(imageList.Images[i] as Bitmap);
                    }
                }
                return bitmapList;
            }
        }

        // für Fonts:
        struct CharacterDisplayList
        {
            public Gdi.GLYPHMETRICSFLOAT glyphmetrics; // Größe und Abstand
            public OpenGlList displaylist; //zum darstellen, mit Besitz
        }
        class FontDisplayList : Dictionary<char, CharacterDisplayList>
        {
            public string fontName;
            public IntPtr deviceContext;
            public void AssertCharacter(char c)
            {
                CharacterDisplayList cdl;
                if (!TryGetValue(c, out cdl))
                {
                    IntPtr fnt = Gdi.CreateFont(100, 0, 0, 0, 0, false, false, false, 1, 0, 0, 0, 0, fontName);
                    IntPtr oldfont = Gdi.SelectObject(deviceContext, fnt);
                    Gdi.GLYPHMETRICSFLOAT[] glyphmetrics = new Gdi.GLYPHMETRICSFLOAT[1];
                    OpenGlList list = new OpenGlList(fontName + "-" + c);
                    if (Wgl.wglUseFontOutlines(deviceContext, (int)c, 1, list.ListNumber, 20.0f, 0.0f, Wgl.WGL_FONT_POLYGONS, glyphmetrics))
                    {
#if DEBUG
                        //System.Diagnostics.Trace.WriteLine("wglUseFontOutlines success: " + deviceContext.ToString() + ", " + c);
#endif
                        cdl.glyphmetrics = glyphmetrics[0];
                        cdl.displaylist = list;
                        this[c] = cdl;
                    }
                    else
                    {
                        fnt = Gdi.CreateFont(-100, 0, 0, 0, 0, false, false, false, 1, 0, 0, 0, 0, fontName);
                        bool dbg = Wgl.wglUseFontOutlines(deviceContext, (int)c, 1, list.ListNumber, 20.0f, 0.0f, Wgl.WGL_FONT_POLYGONS, glyphmetrics);
                    }
                    Gdi.SelectObject(deviceContext, oldfont);
                    Gdi.DeleteObject(fnt);
                }
            }
        }
        // alle FontDisplayListen statisch, da von mehreren renderContexts aus gleichzeitig benutzt
        static Dictionary<string, FontDisplayList> fonts = new Dictionary<string, FontDisplayList>();
        Dictionary<string, FontDisplayList> localFonts;
        private FontDisplayList GetFontDisplayList(string fontName)
        {
            FontDisplayList res;
            Dictionary<string, FontDisplayList> theFonts;
            if (isBitmap) theFonts = localFonts;
            else theFonts = fonts;
            if (!theFonts.TryGetValue(fontName, out res))
            {
                res = new FontDisplayList();
                res.fontName = fontName;
                res.deviceContext = deviceContext;
                // die Standardzeichen generieren (fehlt noch bold, italic)
                // um den text selbst zu generieren könnte man wie folgt vorgehen:
                // System.Drawing.Drawing2D.GraphicsPath, System.Drawing.Drawing2D.GraphicsPath. Flatten, 
                // System.Drawing.Drawing2D.PathPointType
                // eine Nurbs Ebene generieren und die polygone als Trimmkurve nehmen

                //IntPtr fnt = Gdi.CreateFont(100, 0, 0, 0, 400, false, false, false, 1, 0, 0, 0, 0, fontName);
                //IntPtr oldfont = Gdi.SelectObject(deviceContext, fnt);
                //Gdi.GLYPHMETRICSFLOAT[] glyphmetrics = new Gdi.GLYPHMETRICSFLOAT[127 - 32];
                //OpenGlList[] lists = OpenGlList.CreateMany(127 - 32);
                //if (Wgl.wglUseFontOutlines(deviceContext, 32, 127 - 32, lists[0].ListNumber, 20.0f, 0.0f, Wgl.WGL_FONT_LINES, glyphmetrics))
                //{
                //    for (int i = 0; i < 127-32; ++i)
                //    {
                //        CharacterDisplayList cdl;
                //        cdl.glyphmetrics = glyphmetrics[i];
                //        cdl.displaylist = lists[i];
                //        res[(char)(i + 32)] = cdl;
                //    }
                //}
                //Gdi.SelectObject(deviceContext, oldfont);
                //Gdi.DeleteObject(fnt);

                theFonts[fontName] = res;
            }
            return res;
        }
        /// <summary>
        /// Initializes a new instance of PaintToOpenGL with specified precision for tessellation
        /// </summary>
        /// <param name="precision">Precision value for curve approximation and surface tessellation (default: 1e-6)</param>
        /// <remarks>
        /// This constructor initializes the rendering state but does not create an OpenGL context.
        /// Call <see cref="Init(Control)"/> or <see cref="Init(IntPtr, int, int, bool)"/> to create the context.
        /// </remarks>
        public PaintToOpenGL(double precision = 1e-6)
        {
            try
            {
                // Clear any existing OpenGL errors from previous operations
                CheckError(true);
            }
            catch { }
            if (MainThread == null) MainThread = Thread.CurrentThread;
            this.precision = precision;
            paintSurfaces = true;
            paintEdges = true;
            paintSurfaceEdges = true;
            currentList = null;
            selectColor = Color.Yellow;
            stateStack = new Stack<state>();
            selectBuf = new int[selectBufSize];
            lineWidthFactor = 10.0;
            icons = new Dictionary<Bitmap, IPaintTo3DList>();
        }
        ~PaintToOpenGL()
        {
			try
			{
				OnDisposed(null, null);
			}
			catch (Exception ex)
			{
				// Suppress finalizer exceptions to prevent application crash
				try
				{
					OpenGLErrorHandler.LogError($"Exception in PaintToOpenGL finalizer during OnDisposed: {ex.Message}");
				}
				catch { } // Suppress any logging errors
			}

			// Dispose of device context
			try
			{
				if (deviceContext != IntPtr.Zero && controlHandle != IntPtr.Zero)
				{
					bool ok = User.ReleaseDC(controlHandle, deviceContext);
					if (!ok)
					{
						int lastError = Marshal.GetLastWin32Error();
						try
						{
							OpenGLErrorHandler.LogWarning($"Failed to release device context in finalizer (error {lastError})");
						}
						catch { } // Suppress logging errors
					}
				}
			}
			catch (Exception ex)
			{
				// Suppress device context cleanup errors to prevent finalizer crash
				try
				{
					OpenGLErrorHandler.LogError($"Exception releasing device context in finalizer: {ex.Message}");
				}
				catch { } // Suppress any logging errors
			}
		}
        public void SetClientSize(Size sz)
        {
            clientwidth = sz.Width;
            clientheight = sz.Height;
        }
        public void Init(Bitmap image)
        {
            graphics = Graphics.FromImage(image);
            IntPtr dc = graphics.GetHdc();
            Init(dc, image.Width, image.Height, true);
        }
        public void Init(IntPtr deviceContext, int width, int height, bool toBitmap)
        {
            // CheckError(); // kein OpenGL

            this.deviceContext = deviceContext;
            isBitmap = toBitmap;

            // ============================================================================
            // CRITICAL CONTEXT INITIALIZATION SEQUENCE
            // ============================================================================
            // The OpenGL context lifecycle is:
            // 1. Device context already created (passed as parameter)
            // 2. Pixel format will be set below
            // 3. Rendering context will be created below
            // 4. Context will be made current below (wglMakeCurrent)
            // 5. Modern OpenGL capabilities detection happens AFTER step 4
            //
            // ERROR PREVENTION:
            // - glGetString() and other GL functions require an active context
            // - DetectCapabilities() MUST be called after wglMakeCurrent()
            // - Calling it before will return false/null and all modern features disabled
            // - This is moved to occur after (this as IPaintTo3D).MakeCurrent() below
            // ============================================================================

            //Setup pixel format
            Gdi.PIXELFORMATDESCRIPTOR pixelFormat = new Gdi.PIXELFORMATDESCRIPTOR();
            //int numpf = Gdi.DescribePixelFormat(deviceContext, 1, (uint)0, IntPtr.Zero);
            //uint pfdsize = (uint)Marshal.SizeOf(pixelFormat);
            //for (int i = 0; i < numpf; i++)
            //{
            //    Gdi.DescribePixelFormat(deviceContext, i + 1, pfdsize, new IntPtr(&pixelFormat));
            //    if ((pixelFormat.dwFlags & Gdi.PFD_SUPPORT_OPENGL) != 0 && (pixelFormat.dwFlags & Gdi.PFD_DRAW_TO_WINDOW) != 0 && (pixelFormat.dwFlags & Gdi.PFD_DOUBLEBUFFER) != 0)
            //    {
            //        System.Diagnostics.Trace.WriteLine(pixelFormat.cRedBits.ToString() + ", " + pixelFormat.cAlphaBits.ToString());
            //    }
            //}

            pixelFormat.nSize = (short)Marshal.SizeOf(pixelFormat);
            pixelFormat.nVersion = 1;
            if (toBitmap)
            {
                pixelFormat.dwFlags = Gdi.PFD_SUPPORT_OPENGL | Gdi.PFD_DRAW_TO_BITMAP;
            }
            else
            {
                pixelFormat.dwFlags = Gdi.PFD_DRAW_TO_WINDOW | Gdi.PFD_SUPPORT_OPENGL |
                    Gdi.PFD_DOUBLEBUFFER;
            }
            pixelFormat.iPixelType = (byte)Gdi.PFD_TYPE_RGBA;
            pixelFormat.cColorBits = colorBits;
            pixelFormat.cRedBits = 0;
            pixelFormat.cRedShift = 0;
            pixelFormat.cGreenBits = 0;
            pixelFormat.cGreenShift = 0;
            pixelFormat.cBlueBits = 0;
            pixelFormat.cBlueShift = 0;
            pixelFormat.cAlphaBits = 0;
            pixelFormat.cAlphaShift = 0;
            pixelFormat.cAccumBits = accumBits;
            pixelFormat.cAccumRedBits = 0;
            pixelFormat.cAccumGreenBits = 0;
            pixelFormat.cAccumBlueBits = 0;
            pixelFormat.cAccumAlphaBits = 0;
            pixelFormat.cDepthBits = depthBits;
            pixelFormat.cStencilBits = 1; // stencilBits;
            pixelFormat.cAuxBuffers = 0;
            pixelFormat.iLayerType = (byte)Gdi.PFD_MAIN_PLANE;
            pixelFormat.bReserved = 0;
            pixelFormat.dwLayerMask = 0;
            pixelFormat.dwVisibleMask = 0;
            pixelFormat.dwDamageMask = 0;

            if (deviceContext == IntPtr.Zero)
            {
                throw new PaintToOpenGLException("CreateContexts: Unable to create an OpenGL device context");
            }

            //Set pixel format
            int selectedFormat = Gdi.ChoosePixelFormat(deviceContext, ref pixelFormat);
            // CheckError(); // kein OpenGL

            //Make sure the requested pixel format is available
            if (selectedFormat == 0)
            {
                throw new PaintToOpenGLException("CreateContexts: Unable to find a suitable pixel format");
            }

            if (!Gdi.SetPixelFormat(deviceContext, selectedFormat, ref pixelFormat))
            {
                int lastError = Marshal.GetLastWin32Error();
                int pf = Gdi.GetPixelFormat(deviceContext);
                lastError = Marshal.GetLastWin32Error();
                throw new PaintToOpenGLException(string.Format("CreateContexts: Unable to set the requested pixel format ({0})", selectedFormat));
            }

            //Create rendering context
            renderContext = Wgl.wglCreateContext(deviceContext);

            activeRenderContexts.Add(renderContext);
            // CheckError(); kein OpenGL
            // wir wollen nur einen Satz von Listen verwenden, sonst ist das mit dem Löschen der Listen
            // ein Problem, da es die gleichen Nummern mehrfach gibt. Da das Löschen der Listen zu unberechenbaren
            // Zeitpunkten kommt, werden die Listen sonst im falschen Kontext gelöscht
            if (MainRenderContext == IntPtr.Zero)
            {
                if (!toBitmap)
                {
                    MainRenderContext = renderContext;
                    LastRenderContext = renderContext;
                    Application.ApplicationExit += new EventHandler(RemoveMainRenderContext);
                }
            }
            else
            {   // geht nur sorum, keine Ahnung warum. Der MainRenderContext darf nie gelöscht werden
                if (!toBitmap)
                {
                    bool ok = Wgl.wglShareLists(LastRenderContext, renderContext);
                    LastRenderContext = renderContext;
                    if (!ok)
                    {
                        // ok = Wgl.wglShareLists(renderContext,MainRenderContext);
                        MainRenderContext = renderContext;
                        fonts = new Dictionary<string, FontDisplayList>();
                    }
                }
            }

            if (renderContext == IntPtr.Zero)
            {
                throw new PaintToOpenGLException("CreateContexts: Unable to create an OpenGL rendering context");
            }
            clientwidth = width;
            clientheight = height;

            //Make this the current context
            (this as IPaintTo3D).MakeCurrent();

            // CRITICAL: Detect modern OpenGL capabilities AFTER context is made current
            // This ensures glGetString() and other GL queries have a valid context to work with
            if (modernOpenGL == null)
            {
                modernOpenGL = new ModernOpenGLIntegration(this);
                OpenGLErrorHandler.LogDebug("Init: Making context current and detecting modern OpenGL capabilities...");
                ModernOpenGLIntegration.DetectCapabilities();
                useModernRendering = Settings.GlobalSettings.GetBoolValue("UseModernOpenGL", true) && 
                                    ModernOpenGLIntegration.SupportsShaders;
                string renderingStatus = useModernRendering ? "enabled" : "disabled";
                OpenGLErrorHandler.LogDebug($"Init: Modern OpenGL rendering {renderingStatus}");
            }

            int dbgscentil;
            Gl.glGetIntegerv(Gl.GL_STENCIL_BITS, out dbgscentil);
            Gl.glGetIntegerv(Gl.GL_RED_BITS, out dbgscentil);
            Gl.glGetIntegerv(Gl.GL_DEPTH_BITS, out dbgscentil);

            // der Scentil Buffer hat bei mir 8 Bit, vermutlich der Alpha Wert für die Durchlässigkeit

            //nurbsRenderer = Glu.gluNewNurbsRenderer();
            //Glu.gluNurbsProperty(nurbsRenderer, Glu.GLU_SAMPLING_TOLERANCE, (float)precision);
            Gl.glShadeModel(Gl.GL_SMOOTH);
            CheckError();
            
            // Initialize modern rendering path if available
            if (useModernRendering && modernRenderingPath == null)
            {
                try
                {
                    modernRenderingPath = new ModernRenderingPath(modernOpenGL);
                    OpenGLErrorHandler.LogDebug("Modern OpenGL rendering path initialized");
                }
                catch (Exception ex)
                {
                    OpenGLErrorHandler.LogError($"Failed to initialize modern rendering path: {ex.Message}");
                    useModernRendering = false;
                }
            }

            if (toBitmap)
            {
                localFonts = new Dictionary<string, FontDisplayList>();
            }
            lock (ContextsToDelete)
            {   // hier alle zu löschenden löschen, da ShareList shon gelaufen ist.
                for (int i = 0; i < ContextsToDelete.Count; i++)
                {
                    bool ok = Wgl.wglDeleteContext(ContextsToDelete[i]);
                    activeRenderContexts.Remove(ContextsToDelete[i]);
                }
                ContextsToDelete.Clear();
            }
            CheckError();
        }
        public bool UseSharedLists => !isBitmap;

        /// <summary>
        /// Cleanup handler called when the application exits. Ensures all OpenGL resources
        /// and rendering contexts are properly released before shutdown.
        /// </summary>
        /// <param name="sender">Event sender (typically Application)</param>
        /// <param name="e">Event arguments</param>
        void RemoveMainRenderContext(object sender, EventArgs e)
        {
            try
            {
                OpenGLErrorHandler.LogDebug("RemoveMainRenderContext: Starting cleanup...");
                
                // CRITICAL: Free deferred deletion lists first (requires valid context)
                try
                {
                    OpenGlList.FreeLists();
                    OpenGLErrorHandler.LogDebug("RemoveMainRenderContext: Deferred lists freed");
                }
                catch (Exception ex)
                {
                    OpenGLErrorHandler.LogError($"RemoveMainRenderContext: Error freeing deferred lists: {ex.Message}");
                }
                
                // CRITICAL: Free all currently open lists
                try
                {
                    OpenGlList.FreeAllOpenLists();
                    OpenGLErrorHandler.LogDebug("RemoveMainRenderContext: All open lists freed");
                }
                catch (Exception ex)
                {
                    OpenGLErrorHandler.LogError($"RemoveMainRenderContext: Error freeing all open lists: {ex.Message}");
                }
                
                // Delete contexts marked for deletion (safer to delete before active contexts)
                lock (ContextsToDelete)
                {
                    int deletedCount = 0;
                    for (int i = 0; i < ContextsToDelete.Count; i++)
                    {
                        try
                        {
                            IntPtr ctx = ContextsToDelete[i];
                            bool ok = Wgl.wglDeleteContext(ctx);
                            if (ok)
                            {
                                activeRenderContexts.Remove(ctx);
                                deletedCount++;
                                OpenGLErrorHandler.LogDebug($"RemoveMainRenderContext: Deferred context {ctx} deleted successfully");
                            }
                            else
                            {
                                int lastError = Marshal.GetLastWin32Error();
                                OpenGLErrorHandler.LogWarning($"RemoveMainRenderContext: Failed to delete deferred context {ctx} (error {lastError})");
                                activeRenderContexts.Remove(ctx); // Remove even if delete failed
                            }
                        }
                        catch (Exception ex)
                        {
                            OpenGLErrorHandler.LogError($"RemoveMainRenderContext: Exception deleting deferred context {i}: {ex.Message}");
                        }
                    }
                    ContextsToDelete.Clear();
                    OpenGLErrorHandler.LogDebug($"RemoveMainRenderContext: {deletedCount}/{ContextsToDelete.Count} deferred contexts deleted");
                }
                
                // Delete remaining active rendering contexts (except main context if somehow still present)
                try
                {
                    int deletedCount = 0;
                    int failedCount = 0;
                    var contextsToDelete = new List<IntPtr>(activeRenderContexts);
                    
                    foreach (IntPtr ctx in contextsToDelete)
                    {
                        // Skip main render context (shouldn't be in active list, but be safe)
                        if (ctx == MainRenderContext)
                        {
                            OpenGLErrorHandler.LogWarning($"RemoveMainRenderContext: Skipping MainRenderContext {ctx} in active list (should not be here)");
                            continue;
                        }
                        
                        try
                        {
                            bool ok = Wgl.wglDeleteContext(ctx);
                            if (ok)
                            {
                                activeRenderContexts.Remove(ctx);
                                deletedCount++;
                                OpenGLErrorHandler.LogDebug($"RemoveMainRenderContext: Active context {ctx} deleted successfully");
                            }
                            else
                            {
                                int lastError = Marshal.GetLastWin32Error();
                                OpenGLErrorHandler.LogWarning($"RemoveMainRenderContext: Failed to delete active context {ctx} (error {lastError})");
                                activeRenderContexts.Remove(ctx);
                                failedCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            OpenGLErrorHandler.LogError($"RemoveMainRenderContext: Exception deleting active context {ctx}: {ex.Message}");
                            activeRenderContexts.Remove(ctx);
                            failedCount++;
                        }
                    }
                    
                    OpenGLErrorHandler.LogDebug($"RemoveMainRenderContext: Active contexts deleted: {deletedCount} success, {failedCount} failed");
                }
                catch (Exception ex)
                {
                    OpenGLErrorHandler.LogError($"RemoveMainRenderContext: Error deleting active contexts: {ex.Message}");
                }
                
                // Delete main rendering context if it still exists
                if (MainRenderContext != IntPtr.Zero)
                {
                    try
                    {
                        bool ok = Wgl.wglDeleteContext(MainRenderContext);
                        if (ok)
                        {
                            OpenGLErrorHandler.LogDebug($"RemoveMainRenderContext: MainRenderContext {MainRenderContext} deleted successfully");
                        }
                        else
                        {
                            int lastError = Marshal.GetLastWin32Error();
                            OpenGLErrorHandler.LogWarning($"RemoveMainRenderContext: Failed to delete MainRenderContext (error {lastError})");
                        }
                    }
                    catch (Exception ex)
                    {
                        OpenGLErrorHandler.LogError($"RemoveMainRenderContext: Exception deleting MainRenderContext: {ex.Message}");
                    }
                    finally
                    {
                        MainRenderContext = IntPtr.Zero;
                    }
                }
                
                // Clear font cache
                try
                {
                    fonts = new Dictionary<string, FontDisplayList>();
                    OpenGLErrorHandler.LogDebug("RemoveMainRenderContext: Font cache cleared");
                }
                catch (Exception ex)
                {
                    OpenGLErrorHandler.LogError($"RemoveMainRenderContext: Error clearing font cache: {ex.Message}");
                }
                
                // Release device context if valid (sender should be Control or null)
                if (deviceContext != IntPtr.Zero)
                {
                    try
                    {
                        Control ctrl = sender as Control;
                        if (ctrl != null && ctrl.Handle != IntPtr.Zero)
                        {
                            bool ok = User.ReleaseDC(ctrl.Handle, deviceContext);
                            if (ok)
                            {
                                OpenGLErrorHandler.LogDebug("RemoveMainRenderContext: Device context released successfully");
                            }
                            else
                            {
                                int lastError = Marshal.GetLastWin32Error();
                                OpenGLErrorHandler.LogWarning($"RemoveMainRenderContext: Failed to release device context (error {lastError})");
                            }
                        }
                        else
                        {
                            OpenGLErrorHandler.LogWarning("RemoveMainRenderContext: Device context not released (invalid sender or control handle)");
                        }
                    }
                    catch (Exception ex)
                    {
                        OpenGLErrorHandler.LogError($"RemoveMainRenderContext: Exception releasing device context: {ex.Message}");
                    }
                    finally
                    {
                        deviceContext = IntPtr.Zero;
                    }
                }
                
                // Unload OpenGL library
                try
                {
                    IntPtr mh = Kernel.GetModuleHandle("opengl32.dll");
                    if (mh != IntPtr.Zero)
                    {
                        bool ok = Kernel.FreeLibrary(mh);
                        if (ok)
                        {
                            OpenGLErrorHandler.LogDebug("RemoveMainRenderContext: OpenGL library unloaded successfully");
                        }
                        else
                        {
                            int lastError = Marshal.GetLastWin32Error();
                            OpenGLErrorHandler.LogWarning($"RemoveMainRenderContext: Failed to unload OpenGL library (error {lastError})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    OpenGLErrorHandler.LogError($"RemoveMainRenderContext: Exception unloading OpenGL library: {ex.Message}");
                }
                
                OpenGLErrorHandler.LogDebug("RemoveMainRenderContext: Cleanup completed successfully");
            }
            catch (Exception ex)
            {
                OpenGLErrorHandler.LogError($"RemoveMainRenderContext: Unexpected error during cleanup: {ex.Message}");
            }
        }

        // Mit HashCode und Equals hat es folgende Bewandnis:
        // Der HashCode und Equals darf sich nicht ändern, solange das Objekt als Key für ein Dictionary verwendet
        // wird. Deshalb geht der Trick mit paintSurfaces und paintEdges für den HashCode und Equals nicht.
        public override int GetHashCode()
        {
            return renderContext.GetHashCode();
        }
        public override bool Equals(object obj)
        {
            PaintToOpenGL other = obj as PaintToOpenGL;
            if (other == null) return false;
            return renderContext == other.renderContext;
        }

        private bool disposed = false;
		void OnDisposed(object sender, EventArgs e)
        {
	        if (disposed)
		        return; // Prevent double-cleanup

	        disposed = true;

			if (MainRenderContext != IntPtr.Zero)
            {
                // OpenGlList.FreeLists();
                // kommt aus dem falschen thread, darf hier nicht ausgelöst werden
            }
            //Dispose of rendering context, MainRenderContext wird bei ApplicationExtit gelöscht
            if (renderContext == MainRenderContext)
            {   //MainRenderContext  darf nie gelöscht werden
                renderContext = IntPtr.Zero;
                //Dispose of device context
                //if (deviceContext != IntPtr.Zero)
                //{
                //    if (sender is Control) User.ReleaseDC((sender as Control).Handle, deviceContext);
                //    deviceContext = IntPtr.Zero;
                //}
            }
            else if (renderContext != IntPtr.Zero)
            {
                //if (renderContext == MainRenderContext)
                //{
                //    MainRenderContext = IntPtr.Zero;
                //    FontCache.GlobalFontCache.Clear();
                //}
                // bool ok = Wgl.wglMakeCurrent(deviceContext, renderContext);
                // xxx if (ok) Wgl.wglDeleteContext(renderContext);
                lock (ContextsToDelete)
                {
                    ContextsToDelete.Add(renderContext);
                }
                renderContext = IntPtr.Zero;

                // Device Context wird im Destruktor freigegeben

                //Dispose of device context
                //if (deviceContext != IntPtr.Zero)
                //{
                //    if (sender is Control) User.ReleaseDC((sender as Control).Handle, deviceContext);
                //    deviceContext = IntPtr.Zero;
                //}
            }
        }
        #endregion
        #region IPaintTo3D implementation
        bool IPaintTo3D.PaintSurfaces
        {
            get { return paintSurfaces; }
        }
        bool IPaintTo3D.PaintEdges
        {
            get { return paintEdges; }
        }
        bool IPaintTo3D.PaintSurfaceEdges
        {
            get { return paintSurfaceEdges; }
            set { paintSurfaceEdges = value; }
        }
        bool IPaintTo3D.UseLineWidth
        {
            get { return useLineWidth; }
            set { useLineWidth = value; }
        }
        double IPaintTo3D.PixelToWorld
        {
            get
            {
                return pixelToWorld;
            }
        }
        double IPaintTo3D.Precision
        {
            get { return precision; }
            set { precision = value; }
        }
        bool IPaintTo3D.SelectMode
        {
            get { return selectMode; }
            set { selectMode = value; }
        }
        Color IPaintTo3D.SelectColor
        {
            get { return selectColor; }
            set { selectColor = value; }
        }
        bool IPaintTo3D.DelayText
        {
            get { return delayText; }
            set { delayText = value; }
        }
        bool IPaintTo3D.DelayAll
        {
            get { return delayAll; }
            set { delayAll = value; }
        }
        bool IPaintTo3D.TriangulateText
        {
            get { return triangulateText; }
            set { triangulateText = value; }
        }
        bool IPaintTo3D.DontRecalcTriangulation
        {
            get
            {
                return dontRecalcTriangulation;
            }
            set
            {
                dontRecalcTriangulation = value;
            }
        }
        PaintCapabilities IPaintTo3D.Capabilities
        {
            get
            {
                return PaintCapabilities.Standard | PaintCapabilities.ZoomIndependentDisplayList;
            }
        }
        Dictionary<Bitmap, IPaintTo3DList> icons;
        static Dictionary<Bitmap, IPaintTo3DList> bitmaps = new Dictionary<Bitmap, IPaintTo3DList>();
        static Dictionary<Bitmap, uint> textures = new Dictionary<Bitmap, uint>();
        
        /// <summary>
        /// Unregisters a texture from the resource manager and deletes it from OpenGL
        /// </summary>
        /// <param name="bitmap">The bitmap associated with the texture</param>
        /// <returns>True if texture was unregistered, false if it wasn't found</returns>
        private static bool UnregisterAndDeleteTexture(Bitmap bitmap)
        {
            uint texName;
            if (textures.TryGetValue(bitmap, out texName))
            {
                try
                {
                    OpenGLResourceManager.UnregisterTexture(texName);
                    Gl.glDeleteTextures(1, ref texName);
                    OpenGLErrorHandler.LogDebug($"Texture deleted: {texName}");
                    textures.Remove(bitmap);
                    return true;
                }
                catch (Exception e)
                {
                    OpenGLErrorHandler.LogError($"Error deleting texture {texName}: {e.Message}");
                    return false;
                }
            }
            return false;
        }
        
        /// <summary>
        /// Gets the current texture cache statistics
        /// </summary>
        /// <returns>Number of textures currently cached</returns>
        private static int GetCachedTextureCount()
        {
            return textures.Count;
        }
        
        /// <summary>
        /// Cleans up unused textures from the cache when it grows too large.
        /// Useful for preventing memory leaks when many different bitmaps are loaded.
        /// </summary>
        /// <param name="maxCachedTextures">Maximum number of textures to keep (default: 100)</param>
        private static void CleanupTextureCache(int maxCachedTextures = 100)
        {
            if (textures.Count > maxCachedTextures)
            {
                int toRemove = textures.Count - maxCachedTextures;
                var bitmapsToClean = new List<Bitmap>(textures.Keys);
                
                // Remove oldest bitmaps first (first N items)
                for (int i = 0; i < Math.Min(toRemove, bitmapsToClean.Count); i++)
                {
                    if (UnregisterAndDeleteTexture(bitmapsToClean[i]))
                    {
                        OpenGLErrorHandler.LogDebug($"Cleaned texture {i + 1}/{toRemove} from cache");
                    }
                }
            }
        }
        
        /// <summary>
        /// Gets rendering pipeline statistics including texture and display list counts
        /// </summary>
        /// <returns>A formatted string with resource statistics</returns>
        public static string GetRenderingStatistics()
        {
            var stats = OpenGLResourceManager.GetStatistics();
            var resourceInfo = new StringBuilder();
            
            resourceInfo.AppendLine("=== OpenGL Rendering Pipeline Statistics ===");
            resourceInfo.AppendLine($"Display Lists: {stats.ActiveDisplayLists} active");
            resourceInfo.AppendLine($"  Created: {stats.TotalDisplayListsCreated}, Deleted: {stats.TotalDisplayListsDeleted}");
            resourceInfo.AppendLine($"Textures: {stats.ActiveTextures} active");
            resourceInfo.AppendLine($"  Created: {stats.TotalTexturesCreated}, Deleted: {stats.TotalTexturesDeleted}");
            resourceInfo.AppendLine($"Est. Memory: {stats.EstimatedMemoryUsageBytes / (1024 * 1024)} MB");
            resourceInfo.AppendLine($"Cached Bitmaps: {GetCachedTextureCount()}");
            resourceInfo.AppendLine($"Modern OpenGL: {(ModernOpenGLIntegration.SupportsShaders ? "Enabled" : "Disabled")}");
            
            return resourceInfo.ToString();
        }
        
        /// <summary>
        /// Initializes OpenGL rendering for a Windows Forms control
        /// </summary>
        /// <param name="ctrl">The control to render into</param>
        /// <exception cref="PaintToOpenGLException">Thrown if the control handle is not created or OpenGL initialization fails</exception>
        /// <remarks>
        /// This method:
        /// - Creates an OpenGL rendering context for the control
        /// - Sets up display list sharing across multiple contexts
        /// - Configures pixel format and rendering parameters
        /// - Registers for cleanup when the control is destroyed
        /// 
        /// The control must have its handle created before calling this method.
        /// </remarks>
        internal void Init(Control ctrl)
        {

            //Setup the control's styles -- implemented in the control itself
            //Make sure the handle for this control has been created
            if (ctrl.Handle == IntPtr.Zero)
            {
                throw new PaintToOpenGLException("CreateContexts: The control's window handle has not been created.");
            }

            controlHandle = ctrl.Handle;
            Init(User.GetDC(ctrl.Handle), ctrl.ClientSize.Width, ctrl.ClientSize.Height, false);

            // Register for cleanup when control is destroyed
            // Must happen synchronously on same thread due to OpenGL context requirements
            ctrl.HandleDestroyed += new EventHandler(OnDisposed);

            Gl.glPixelStorei(Gl.GL_UNPACK_ALIGNMENT, 1);
            Gl.glPixelStorei(Gl.GL_PACK_ALIGNMENT, 1);
            //byte[, ,] pixels = new byte[64, 64, 3];
            //for (int i = 0; i < 64; ++i)
            //{
            //    for (int j = 0; j < 64; ++j)
            //    {
            //        byte c;
            //        if ((j & 0x08) != (i & 0x08)) c = 255;
            //        else c = 0;
            //        pixels[i, j, 0] = c;
            //        pixels[i, j, 1] = c;
            //        pixels[i, j, 2] = c;
            //    }
            //}
            //CheckError();
            //Gl.glTexImage2D(Gl.GL_TEXTURE_2D, 0, 3, 64, 64, 0, Gl.GL_RGB, Gl.GL_UNSIGNED_BYTE, pixels);
            //Gl.glTexParameterf(Gl.GL_TEXTURE_2D, Gl.GL_TEXTURE_WRAP_S, Gl.GL_CLAMP);
            //Gl.glTexParameterf(Gl.GL_TEXTURE_2D, Gl.GL_TEXTURE_WRAP_T, Gl.GL_CLAMP);
            //Gl.glTexParameterf(Gl.GL_TEXTURE_2D, Gl.GL_TEXTURE_MAG_FILTER, Gl.GL_NEAREST);
            //Gl.glTexParameterf(Gl.GL_TEXTURE_2D, Gl.GL_TEXTURE_MIN_FILTER, Gl.GL_NEAREST);
            //Gl.glTexEnvf(Gl.GL_TEXTURE_ENV, Gl.GL_TEXTURE_ENV_MODE, Gl.GL_DECAL);
            CheckError();
        }
        internal void Disconnect(Control ctrl)
        {
            if (ctrl != null)
            {
                // OnDisposed(null, null); darf hier nicht stehen, da sonst das View wechseln nicht klappt
                // ctrl.Disposed -= new EventHandler(OnDisposed); hängt nicht mehr an Disposed
                // kann man ihn gleich wegmachen, hier sind wir synchron, warum sollte das nicht gehen?
                if (renderContext != IntPtr.Zero && renderContext != MainRenderContext)
                {
                    // Wgl.wglGetCurrentContext();
                    // bool ok = Wgl.wglMakeCurrent(deviceContext, renderContext);
                    // if (ok) 
                    // xxx ok = Wgl.wglDeleteContext(renderContext);
                    // man darf ihn hier nicht löschen, da er noch als Context für die SharedLists gebraucht wird
                    lock (ContextsToDelete)
                    {
                        ContextsToDelete.Add(renderContext);
                    }
                    renderContext = IntPtr.Zero;
                    // Device Context wird im Destruktor freigegeben

                    //Dispose of device context
                    //if (deviceContext != IntPtr.Zero)
                    //{
                    //    User.ReleaseDC(ctrl.Handle, deviceContext);
                    //    deviceContext = IntPtr.Zero;
                    //}

                    ctrl.HandleDestroyed -= new EventHandler(OnDisposed); // ist ja schon entfernt

                }

                // CheckError(); // kein OpenGL
            }
        }

        private void CheckError(bool dontDebug = false)
        {
#if DEBUG_OPENGL
            if (MainThread != Thread.CurrentThread)
            {
                OpenGLErrorHandler.LogWarning("OpenGL call from different thread. Some implementations only support single-threaded applications");
            }
#endif
            // CRITICAL: Drain entire OpenGL error queue, not just one error
            // glGetError() returns one error per call, must loop until GL_NO_ERROR
            int error;
            int errorCount = 0;
            const int MAX_ERROR_COUNT = 100; // Safety limit to prevent infinite loops
            
            while ((error = Gl.glGetError()) != Gl.GL_NO_ERROR && errorCount < MAX_ERROR_COUNT)
            {
                string errorMsg = OpenGLErrorHandler.GetErrorString(error);
                OpenGLErrorHandler.LogError($"OpenGL Error {error:X}: {errorMsg}");
                errorCount++;
                
                // Special handling for out-of-memory: close any open list
                if (error == Gl.GL_OUT_OF_MEMORY)
                {
                    currentList = null; // Close any open list
                }
            }
            
            // Log warning if error queue was full (potential overflow)
            if (errorCount >= MAX_ERROR_COUNT)
            {
                OpenGLErrorHandler.LogError("OpenGL error queue overflow detected - possible resource leak or driver issue");
            }
        }
        void IPaintTo3D.Dispose()
        {
            // Clean up modern OpenGL resources first
            try
            {
                modernRenderingPath?.Cleanup();
                modernOpenGL?.Cleanup();
            }
            catch (Exception ex)
            {
                OpenGLErrorHandler.LogError($"Error cleaning up modern OpenGL resources: {ex.Message}");
            }
            
            if (graphics != null)
            {
                graphics.ReleaseHdc(deviceContext);
                graphics.Dispose();
                graphics = null;
            }
            if (renderContext != IntPtr.Zero && renderContext != MainRenderContext)
            {
                ContextsToDelete.Add(renderContext);
                renderContext = IntPtr.Zero;
            }
            
            // Clean up any textures associated with this rendering context
            if (isBitmap)
            {
                // For bitmap rendering contexts, we can clean up local textures
                // Note: shared textures remain for other contexts
                try
                {
                    var texturesToDelete = new List<Bitmap>(textures.Keys);
                    foreach (var bitmap in texturesToDelete)
                    {
                        UnregisterAndDeleteTexture(bitmap);
                    }
                    OpenGLErrorHandler.LogDebug($"Cleaned up {GetCachedTextureCount()} textures on dispose");
                }
                catch (Exception e)
                {
                    OpenGLErrorHandler.LogError($"Exception cleaning up textures in Dispose: {e.Message}");
                }
            }
            
            CheckError();
        }
        void IPaintTo3D.PushState()
        {
            state s;
            s.blending = Gl.glIsEnabled(Gl.GL_BLEND) != 0;
            s.useZBuffer = Gl.glIsEnabled(Gl.GL_DEPTH_TEST) != 0;
            stateStack.Push(s);
        }
        void IPaintTo3D.PopState()
        {
            state s = stateStack.Pop();
            if (s.blending) Gl.glEnable(Gl.GL_BLEND);
            else Gl.glDisable(Gl.GL_BLEND);
            if (s.useZBuffer) Gl.glEnable(Gl.GL_DEPTH_TEST);
            else Gl.glDisable(Gl.GL_DEPTH_TEST);
        }
        void IPaintTo3D.MakeCurrent()
        {
            // Context Thread Safety: Only switch context if necessary (expensive operation)
            // Verify current context matches what we expect
            IntPtr currentContext = Wgl.wglGetCurrentContext();
            
            if (currentContext != renderContext)
            {
                // Context needs to be switched
                if (!Wgl.wglMakeCurrent(deviceContext, renderContext))
                {
                    int lastError = Marshal.GetLastWin32Error();
                    OpenGLErrorHandler.LogError($"Failed to make context current (error {lastError}). Continuing anyway - some operations may still work");
                    // Continue anyway - some operations may still work
                }
            }
        }
        void IPaintTo3D.Resize(int width, int height)
        {
            if (Wgl.wglGetCurrentContext() != renderContext) (this as IPaintTo3D).MakeCurrent();
            clientwidth = width;
            clientheight = height;
            CheckError();
        }
        void IPaintTo3D.Clear(Color background)
        {
            if (Wgl.wglGetCurrentContext() != renderContext) (this as IPaintTo3D).MakeCurrent();
            backgroundColor = background;
            Gl.glViewport(0, 0, clientwidth, clientheight);
            Gl.glClearColor(background.R / 255.0f, background.G / 255.0f, background.B / 255.0f, 1.0f);
            Gl.glClear(Gl.GL_COLOR_BUFFER_BIT | Gl.GL_DEPTH_BUFFER_BIT);
            CheckError();
        }
        void IPaintTo3D.AvoidColor(Color color)
        {
            backgroundColor = color;
        }
        void IPaintTo3D.SetProjection(Projection projection, BoundingCube boundingCube)
        {
            // System.Diagnostics.Trace.WriteLine("SetProjection: " + boundingCube.ToString());
            if (Wgl.wglGetCurrentContext() != renderContext) (this as IPaintTo3D).MakeCurrent();
            Gl.glViewport(0, 0, clientwidth, clientheight);
            Gl.glMatrixMode(Gl.GL_MODELVIEW);
            Gl.glLoadIdentity();
            Gl.glMatrixMode(Gl.GL_PROJECTION);
            Gl.glLoadIdentity();
            double[,] mm;
            mm = projection.GetOpenGLProjection(0, clientwidth, 0, clientheight, boundingCube);
            double[] pmat = new double[16];
            // ACHTUNG: Matrix ist vertauscht!!!
            pmat[0] = mm[0, 0];
            pmat[1] = mm[1, 0];
            pmat[2] = mm[2, 0];
            pmat[3] = mm[3, 0];
            pmat[4] = mm[0, 1];
            pmat[5] = mm[1, 1];
            pmat[6] = mm[2, 1];
            pmat[7] = mm[3, 1];
            pmat[8] = mm[0, 2];
            pmat[9] = mm[1, 2];
            pmat[10] = mm[2, 2];
            pmat[11] = mm[3, 2];
            pmat[12] = mm[0, 3];
            pmat[13] = mm[1, 3];
            pmat[14] = mm[2, 3];
            pmat[15] = mm[3, 3];

            GeoVector v = projection.Direction;
            projectionDirection = projection.Direction;
            isPerspective = projection.IsPerspective;
            v = projection.InverseProjection * new GeoVector(0.5, 0.3, -1.0);
            pixelToWorld = projection.DeviceToWorldFactor;
            Gl.glEnable(Gl.GL_DEPTH_TEST); // das verdeckt oder nicht
                                           //Gl.glLightModelfv(Gl.GL_LIGHT_MODEL_AMBIENT, new float[] { 0.2f, 0.2f, 0.2f, 1.0f });
            CheckError();
            Gl.glLightModeli(Gl.GL_LIGHT_MODEL_TWO_SIDE, Gl.GL_TRUE);
            CheckError();
            Gl.glEnable(Gl.GL_LIGHTING);
            //Gl.glEnable(Gl.GL_LIGHT_MODEL_AMBIENT);
            Gl.glLightfv(Gl.GL_LIGHT0, Gl.GL_POSITION, new float[] { (float)v.x, (float)v.y, (float)v.z, 0.0f }); // letzte Komponente 0: richtungslicht!
                                                                                                                  // Gl.glLightfv(Gl.GL_LIGHT0, Gl.GL_POSITION, new float[] { 0.1f, 0.1f, 1.0f, 0.0f }); // letzte Komponente 0: richtungslicht!
            Gl.glLightfv(Gl.GL_LIGHT0, Gl.GL_AMBIENT, new float[] { 0.2f, 0.2f, 0.2f, 1.0f });
            Gl.glLightfv(Gl.GL_LIGHT0, Gl.GL_DIFFUSE, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
            Gl.glLightfv(Gl.GL_LIGHT0, Gl.GL_SPECULAR, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });

            //Gl.glLightfv(Gl.GL_LIGHT1, Gl.GL_POSITION, new float[] { -1.0f, 1.0f, 1.0f, 0.0f });
            //Gl.glLightfv(Gl.GL_LIGHT1, Gl.GL_AMBIENT, new float[] { 0.3f, 0.3f, 0.3f, 1.0f });
            //Gl.glLightfv(Gl.GL_LIGHT1, Gl.GL_DIFFUSE, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
            //Gl.glLightfv(Gl.GL_LIGHT1, Gl.GL_SPECULAR, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
            //Gl.glLightfv(Gl.GL_LIGHT1, Gl.GL_SPOT_DIRECTION, new float[] { 0.0f, 0.0f, -1.0f });
            Gl.glEnable(Gl.GL_LIGHT0);
            //Gl.glEnable(Gl.GL_LIGHT1);
            Gl.glLoadMatrixd(pmat);

            Gl.glMatrixMode(Gl.GL_MODELVIEW);
            Gl.glLoadIdentity();
            Gl.glEnable(Gl.GL_AUTO_NORMAL);
            Gl.glEnable(Gl.GL_NORMALIZE);
            Gl.glBlendFunc(Gl.GL_ONE, Gl.GL_ZERO);
            Gl.glColorMaterial(Gl.GL_FRONT_AND_BACK, Gl.GL_AMBIENT_AND_DIFFUSE);
            Gl.glEnable(Gl.GL_COLOR_MATERIAL);
            useLineWidth = projection.UseLineWidth;
            if (projection.UseLineWidth) Gl.glEnable(Gl.GL_LINE_SMOOTH);
            else Gl.glDisable(Gl.GL_LINE_SMOOTH);
            float[] lwminmax = new float[2];
            Gl.glGetFloatv(Gl.GL_LINE_WIDTH_RANGE, lwminmax);
            lineWidthMin = lwminmax[0];
            lineWidthMax = lwminmax[1];
            Gl.glLineWidth(1.0f);
            CheckError();

            // OpenGlCustomize.InvokeSetProjection(this.renderContext, this, projection, boundingCube);
            // DEBUG: (dieser test diente zum Feststellen, wieviele Listen der OpenGL Treiber zulässt
            // zuerst waren das auf meinem rechner nur ca 9000 (opengl32.dll Version 5.x), nach Installation von
            // ATI Catalyst Controlcenter machen auch 1000000 kein Problem mehr
            //IPaintTo3D p3d = this;
            //List<IPaintTo3DList> list = new List<IPaintTo3DList>();
            //for (int i = 0; i < 1000000; ++i)
            //{
            //    p3d.OpenList();
            //    p3d.Polyline(new GeoPoint[] { GeoPoint.Origin, GeoPoint.Origin + GeoVector.XAxis, GeoPoint.Origin + GeoVector.YAxis });
            //    list.Add(p3d.CloseList());
            //}
        }
        void IPaintTo3D.SetColor(Color color, int lockColor)
        {
            if (!colorOverride)
            {
                if (color.R == backgroundColor.R && color.G == backgroundColor.G && color.B == backgroundColor.B)
                {
                    if (color.R + color.G + color.B < 3 * 128)
                        Gl.glColor4ub(255, 255, 255, color.A);
                    else
                        Gl.glColor4ub(0, 0, 0, color.A);
                }
                else
                {
                    Gl.glColor4ub(color.R, color.G, color.B, color.A);
                }
            }
            if (lockColor == 1) colorOverride = true;
            else if (lockColor == -1) colorOverride = false;
            CheckError();
        }
        void IPaintTo3D.SetLineWidth(LineWidth lineWidth)
        {
            if (!useLineWidth) return;
            if (lineWidth == null || lineWidth.Width == 0.0)
            {
                Gl.glDisable(Gl.GL_LINE_SMOOTH);
                Gl.glLineWidth(1.0f);
            }
            else
            {
                Gl.glEnable(Gl.GL_LINE_SMOOTH);
                float gllineWidth = (float)(lineWidth.Width * lineWidthFactor);
                if (gllineWidth < lineWidthMin)
                {
                    Gl.glLineWidth(lineWidthMin);
                }
                else if (gllineWidth > lineWidthMax)
                {
                    Gl.glLineWidth(lineWidthMax);
                }
                else
                {
                    Gl.glLineWidth((float)gllineWidth);
                }
            }
            CheckError();
        }
        void IPaintTo3D.SetLinePattern(LinePattern pattern)
        {
            bool badPattern = false;
            if (pattern != null && pattern.Pattern.Length > 0)
            {
                badPattern = true;
                for (int i = 0; i < pattern.Pattern.Length; i++)
                {
                    if (pattern.Pattern[i] != 0.0)
                    {
                        badPattern = false;
                        break;
                    }
                }
            }
            if (pattern == null || pattern.Pattern.Length == 0 || pattern.Pattern.Length > 8 || badPattern)
            {
                Gl.glDisable(Gl.GL_LINE_STIPPLE);
            }
            else
            {   // einfache nicht gecachete Patternberechnung
                int[] pat = new int[pattern.Pattern.Length];
                double sum = 0.0;
                for (int i = 0; i < pattern.Pattern.Length; ++i)
                {
                    sum += pattern.Pattern[i];
                }
                double f = 16.0 / sum;
                int isum = 0;
                for (int i = 0; i < pattern.Pattern.Length; ++i)
                {
                    pat[i] = (int)Math.Round(pattern.Pattern[i] * f);
                    if (pat[i] == 0) pat[i] = 1;
                    isum += pat[i];
                }
                while (isum > 16)
                {
                    for (int i = 0; i < pat.Length; ++i)
                    {
                        if (pat[i] > 2)
                        {
                            --pat[i];
                            --isum;
                            if (isum <= 16) break;
                        }
                    }
                }
                while (isum < 16)
                {
                    for (int i = 0; i < pat.Length; ++i)
                    {
                        ++pat[i];
                        ++isum;
                        if (isum >= 16) break;
                    }
                }
                int ppp = 0;
                for (int i = 0; i < pat.Length; ++i)
                {
                    for (int j = 0; j < pat[i]; ++j)
                    {
                        ppp = ppp << 1;
                        if ((i & 1) == 0) ++ppp;
                    }
                }
                Gl.glLineStipple(1, (short)ppp);
                Gl.glEnable(Gl.GL_LINE_STIPPLE);            // wird auch mit null aufgerufen
            }
            CheckError();
        }
        void IPaintTo3D.Polyline(GeoPoint[] points)
        {
#if DEBUG
            for (int i = 0; i < points.Length; ++i)
            {
                if (points[i].z < -10) { }
            }
#endif
            if (currentList != null) currentList.SetHasContents();
            Gl.glDisable(Gl.GL_LIGHTING);
            Gl.glBegin(Gl.GL_LINE_STRIP);
            for (int i = 0; i < points.Length; ++i)
            {
                Gl.glVertex3d(points[i].x, points[i].y, points[i].z);
            }
            Gl.glEnd();
            CheckError();
        }
        void IPaintTo3D.FilledPolyline(GeoPoint[] points)
        {
            if (currentList != null) currentList.SetHasContents();
            Gl.glEnable(Gl.GL_LIGHTING);
            Gl.glBegin(Gl.GL_POLYGON);
            for (int i = 0; i < points.Length; ++i)
            {
                Gl.glVertex3d(points[i].x, points[i].y, points[i].z);
            }
            Gl.glEnd();
            CheckError();
        }
        void IPaintTo3D.Points(GeoPoint[] points, float size, PointSymbol pointSymbol)
        {
            if (pointSymbol == PointSymbol.Dot)
            {
                if (currentList != null) currentList.SetHasContents();
                Gl.glDisable(Gl.GL_LIGHTING);
                Gl.glBegin(Gl.GL_POINTS);
                for (int i = 0; i < points.Length; ++i)
                {
                    Gl.glVertex3d(points[i].x, points[i].y, points[i].z);
                }
                Gl.glEnd();
            }
            else
            {
                Bitmap bmp = null;
                if ((pointSymbol & PointSymbol.Select) != 0)
                {
                    bmp = BitmapList[12];
                    if (bmp != null)
                    {
                        for (int i = 0; i < points.Length; ++i) (this as IPaintTo3D).DisplayIcon(points[i], bmp);
                    }
                    return; // nur das volle Quadrat anzeigen, sonst nichts
                }

                int offset = 0;
                if ((this as IPaintTo3D).UseLineWidth) offset = 6; // so wird gesteuert dass bei nur dünn die dünnen Punkte und bei
                                                                   // mit Linienstärke ggf. die dicken Punkte angezeigt werden (Forderung PFOCAD)
                switch ((PointSymbol)((int)pointSymbol & 0x07))
                {
                    case PointSymbol.Empty:
                        bmp = null;
                        break;
                    case PointSymbol.Dot:
                        {
                            bmp = BitmapList[0 + offset];
                        }
                        break;
                    case PointSymbol.Plus:
                        {
                            bmp = BitmapList[1 + offset];
                        }
                        break;
                    case PointSymbol.Cross:
                        {
                            bmp = BitmapList[2 + offset];
                        }
                        break;
                    case PointSymbol.Line:
                        {
                            bmp = BitmapList[3 + offset];
                        }
                        break;
                }
                if (bmp != null)
                {
                    for (int i = 0; i < points.Length; ++i) (this as IPaintTo3D).DisplayIcon(points[i], bmp);
                }
                bmp = null;
                if ((pointSymbol & PointSymbol.Circle) != 0)
                {
                    bmp = BitmapList[5 + offset];
                }
                if ((pointSymbol & PointSymbol.Square) != 0)
                {
                    bmp = BitmapList[4 + offset];
                }
                if (bmp != null)
                {
                    for (int i = 0; i < points.Length; ++i) (this as IPaintTo3D).DisplayIcon(points[i], bmp);
                }

            }
            CheckError();
        }
        void IPaintTo3D.Triangle(GeoPoint[] vertex, GeoVector[] normals, int[] indextriples)
        {
            debugNumTriangles += indextriples.Length / 3;
            if (currentList != null) currentList.SetHasContents();
            
            // Try modern rendering path first if available
            if (useModernRendering && modernRenderingPath != null)
            {
                if (modernRenderingPath.TryRenderTriangleMesh(vertex, normals, indextriples))
                {
                    CheckError();
                    return; // Successfully rendered with modern path
                }
            }
            
            // Fall back to legacy rendering
            Gl.glEnable(Gl.GL_LIGHTING);
            float[] mat_ambient = { 0.5f, 0.5f, 0.5f, 1.0f };
            float[] mat_specular = { 1.0f, 1.0f, 1.0f, 1.0f };
            float[] low_shininess = { 5.0f };
            Gl.glMaterialfv(Gl.GL_FRONT_AND_BACK, Gl.GL_AMBIENT_AND_DIFFUSE, mat_ambient);
            Gl.glMaterialfv(Gl.GL_FRONT_AND_BACK, Gl.GL_SPECULAR, mat_specular);
            Gl.glMaterialfv(Gl.GL_FRONT_AND_BACK, Gl.GL_SHININESS, low_shininess);

            //(this as IPaintTo3D).PushState();
            //Gl.glEnable(Gl.GL_BLEND);
            //Gl.glBlendFunc(Gl.GL_SRC_ALPHA, Gl.GL_ONE_MINUS_SRC_ALPHA);

            Gl.glBegin(Gl.GL_TRIANGLES);
            for (int i = 0; i < indextriples.Length; i += 3)
            {
                // die Reihenfolge der drei Punkte und die Normalenvektoren müssen zusammenpassen,
                // warum ist unklar, aber so gehts:
                GeoPoint v1 = vertex[indextriples[i]];
                GeoPoint v2 = vertex[indextriples[i + 1]];
                GeoPoint v3 = vertex[indextriples[i + 2]];
                GeoVector n0 = (v1 - v2) ^ (v3 - v2);
                GeoVector n1 = normals[indextriples[i]];
                GeoVector n2 = normals[indextriples[i + 1]];
                GeoVector n3 = normals[indextriples[i + 2]];
                if (n0 * n1 < 0)
                {
                    Gl.glNormal3d(n1.x, n1.y, n1.z);
                    Gl.glVertex3d(v1.x, v1.y, v1.z);
                    Gl.glNormal3d(n3.x, n3.y, n3.z);
                    Gl.glVertex3d(v3.x, v3.y, v3.z);
                    Gl.glNormal3d(n2.x, n2.y, n2.z);
                    Gl.glVertex3d(v2.x, v2.y, v2.z);
                }
                else
                {
                    Gl.glNormal3d(n1.x, n1.y, n1.z);
                    Gl.glVertex3d(v1.x, v1.y, v1.z);
                    Gl.glNormal3d(n2.x, n2.y, n2.z);
                    Gl.glVertex3d(v2.x, v2.y, v2.z);
                    Gl.glNormal3d(n3.x, n3.y, n3.z);
                    Gl.glVertex3d(v3.x, v3.y, v3.z);
                }
            }
            Gl.glEnd();
            //(this as IPaintTo3D).PopState();
            CheckError();
        }
        void IPaintTo3D.PrepareIcon(Bitmap icon)
        {   // Für ein Icon wird eine kleine DisplayList gemacht, in der großen stürzt es oft ab
            if (currentList != null) throw new ApplicationException("PrepareIcon called with display list open");
            if (!icons.ContainsKey(icon))
            {
                int r = (icon.Width + 7) / 8; // Anzahl der Bytes pro Zeile
                int n = r * icon.Height; // Anzahl der Bytes
                byte[] oglbitmap = new byte[n]; // stackalloc entzieht das Array der Garbage Collection
                                                // Bitmap in ein byte array umwandeln, nur alpha berücksichtigen
                for (int i = 0; i < icon.Height; ++i)
                {
                    for (int j = 0; j < icon.Width; ++j)
                    {
                        Color clr = icon.GetPixel(j, i);
                        if (clr.A != 0)
                        {
                            int index = i * r + j / 8;
                            byte toAdd = (byte)(0x80 >> (j % 8));
                            oglbitmap[index] |= toAdd;
                        }
                    }
                }
                (this as IPaintTo3D).OpenList("icon"); // zwischen open und close keine GarbageCollection sonst stimmt die Adresse von oglbitmap nicht mehr
                currentList.hasContents = true;
                Gl.glRasterPos3d(0.0, 0.0, 0.0);
                Gl.glPixelStorei(Gl.GL_PACK_SWAP_BYTES, 0);
                Gl.glPixelStorei(Gl.GL_PACK_LSB_FIRST, 0);
                Gl.glPixelStorei(Gl.GL_PACK_ROW_LENGTH, 0);
                Gl.glPixelStorei(Gl.GL_PACK_SKIP_ROWS, 0);
                Gl.glPixelStorei(Gl.GL_PACK_SKIP_PIXELS, 0);
                Gl.glPixelStorei(Gl.GL_PACK_ALIGNMENT, 1);
                Gl.glPixelStorei(Gl.GL_UNPACK_SWAP_BYTES, 0);
                Gl.glPixelStorei(Gl.GL_UNPACK_LSB_FIRST, 0);
                Gl.glPixelStorei(Gl.GL_UNPACK_ROW_LENGTH, 0);
                Gl.glPixelStorei(Gl.GL_UNPACK_SKIP_ROWS, 0);
                Gl.glPixelStorei(Gl.GL_UNPACK_SKIP_PIXELS, 0);
                Gl.glPixelStorei(Gl.GL_UNPACK_ALIGNMENT, 1);
                Gl.glBitmap(icon.Width, icon.Height, icon.Width / 2, icon.Height / 2, 0.0f, 0.0f, oglbitmap);
                // vermutlich merkt sich Gl.glBitmap nur die Adresse und CloseList holt sich dann den Inhalt
                // dazwischen darf keine GarbageCollection laufen, was hier vermutlich mit stackalloc ausgeschlossen ist
                icons[icon] = (this as IPaintTo3D).CloseList();
            }
        }
        void IPaintTo3D.PrepareBitmap(Bitmap bitmap, int xoffset, int yoffset)
        {
            if (currentList != null) throw new ApplicationException("PrepareBitmap called with display list open");
            if (!bitmaps.ContainsKey(bitmap))
            {
                int[] pixels = new int[bitmap.Width * bitmap.Height];
                for (int i = 0; i < bitmap.Height; ++i)
                {
                    for (int j = 0; j < bitmap.Width; ++j)
                    {
                        Color clr = bitmap.GetPixel(j, bitmap.Height - i - 1); // auf den Kopf stellen, Bitmap y geht nach unten
                        pixels[i * bitmap.Width + j] = (clr.R << 24) + (clr.G << 16) + (clr.B << 8) + clr.A;
                        // pixels[i * bitmap.Width + j] = (clr.R << 16) + (clr.G << 8) + clr.B;
                    }
                }
                (this as IPaintTo3D).OpenList("bitmap"); // zwischen open und close keine GarbageCollection sonst stimmt die Adresse von oglbitmap nicht mehr
                currentList.hasContents = true;
                Gl.glRasterPos3d(0.0, 0.0, 0.0);
                // ich finde keine Möglichkeit die Position auf z.B. die Mitte oder einen beliebigen Punkt des
                // Bitmaps zu setzen. Außer: In Notes zu glBitmap steht:
                // Notes
                // To set a valid raster position outside the viewport, first set a valid raster position inside the viewport, 
                // then call glBitmap with NULL as the bitmap parameter and with xmove and ymove set to the offsets of the 
                // new raster position. This technique is useful when panning an image around the viewport. 
                Gl.glBitmap(0, 0, 0, 0, -xoffset, -yoffset, null);
                Gl.glPixelStorei(Gl.GL_PACK_SWAP_BYTES, 0);
                Gl.glPixelStorei(Gl.GL_PACK_LSB_FIRST, 0);
                Gl.glPixelStorei(Gl.GL_PACK_ROW_LENGTH, 0);
                Gl.glPixelStorei(Gl.GL_PACK_SKIP_ROWS, 0);
                Gl.glPixelStorei(Gl.GL_PACK_SKIP_PIXELS, 0);
                Gl.glPixelStorei(Gl.GL_PACK_ALIGNMENT, 4);
                Gl.glPixelStorei(Gl.GL_UNPACK_SWAP_BYTES, 0);
                Gl.glPixelStorei(Gl.GL_UNPACK_LSB_FIRST, 0);
                Gl.glPixelStorei(Gl.GL_UNPACK_ROW_LENGTH, 0);
                Gl.glPixelStorei(Gl.GL_UNPACK_SKIP_ROWS, 0);
                Gl.glPixelStorei(Gl.GL_UNPACK_SKIP_PIXELS, 0);
                Gl.glPixelStorei(Gl.GL_UNPACK_ALIGNMENT, 4);
                Gl.glDrawPixels(bitmap.Width, bitmap.Height, Gl.GL_RGBA, Gl.GL_UNSIGNED_INT_8_8_8_8, pixels);
                // Gl.glDrawPixels(bitmap.Width, bitmap.Height, Gl.GL_RGB, Gl.GL_UNSIGNED_INT, pixels);
                bitmaps[bitmap] = (this as IPaintTo3D).CloseList();
                int maxTextureSize;
                Gl.glGetIntegerv(Gl.GL_MAX_TEXTURE_SIZE, out maxTextureSize);
            }
        }
        void IPaintTo3D.PrepareText(string fontName, string textString, FontStyle fontStyle)
        {
            // siehe http://www.opengl.org/resources/features/fontsurvey/ für andere Font Methoden
            // dieses hier ist zu Resourcenträchtig
            FontDisplayList fdl = GetFontDisplayList(fontName);
            for (int i = 0; i < textString.Length; ++i)
            {
                fdl.AssertCharacter(textString[i]);
            }
            CheckError();
        }
        int dbgNumChar;
        void IPaintTo3D.PreparePointSymbol(PointSymbol symbol)
        {
            int offset = 0;
            if ((this as IPaintTo3D).UseLineWidth) offset = 6; // so wird gesteuert dass bei nur dünn die dünnen Punkte und bei
            // mit Linienstärke ggf. die dicken Punkte angezeigt werden (Forderung PFOCAD)
            Bitmap bmp = null;
            switch ((PointSymbol)((int)symbol & 0x07))
            {
                case PointSymbol.Empty:
                    bmp = null;
                    break;
                case PointSymbol.Dot:
                    {
                        bmp = BitmapList[0 + offset];
                    }
                    break;
                case PointSymbol.Plus:
                    {
                        bmp = BitmapList[1 + offset];
                    }
                    break;
                case PointSymbol.Cross:
                    {
                        bmp = BitmapList[2 + offset];
                    }
                    break;
                case PointSymbol.Line:
                    {
                        bmp = BitmapList[3 + offset];
                    }
                    break;
            }
            if (bmp != null)
            {
                (this as IPaintTo3D).PrepareIcon(bmp);
            }
            bmp = null;
            if ((symbol & PointSymbol.Circle) != 0)
            {
                bmp = BitmapList[5 + offset];
            }
            if ((symbol & PointSymbol.Square) != 0)
            {
                bmp = BitmapList[4 + offset];
            }
            if ((symbol & PointSymbol.Select) != 0)
            {
                bmp = BitmapList[12];
            }
            if (bmp != null)
            {
                (this as IPaintTo3D).PrepareIcon(bmp);
            }
        }
        void IPaintTo3D.PrepareBitmap(Bitmap bitmap)
        {
            if (bitmap == null)
            {
                OpenGLErrorHandler.LogError("PrepareBitmap called with null bitmap");
                return;
            }
            
            if (!textures.ContainsKey(bitmap))
            {
                try
                {
                    Gl.glPixelStorei(Gl.GL_UNPACK_ALIGNMENT, 1);
                    Gl.glPixelStorei(Gl.GL_PACK_ALIGNMENT, 1);
                    uint texName;
                    Gl.glGenTextures(1, out texName);
                    
                    if (texName == 0)
                    {
                        OpenGLErrorHandler.LogError("Failed to generate texture name");
                        CheckError();
                        return;
                    }
                    
                    Gl.glBindTexture(Gl.GL_TEXTURE_2D, texName);
                    Gl.glTexParameteri(Gl.GL_TEXTURE_2D, Gl.GL_TEXTURE_WRAP_S, Gl.GL_REPEAT);
                    Gl.glTexParameteri(Gl.GL_TEXTURE_2D, Gl.GL_TEXTURE_WRAP_T, Gl.GL_REPEAT);
                    Gl.glTexParameteri(Gl.GL_TEXTURE_2D, Gl.GL_TEXTURE_MAG_FILTER, Gl.GL_NEAREST);
                    Gl.glTexParameteri(Gl.GL_TEXTURE_2D, Gl.GL_TEXTURE_MIN_FILTER, Gl.GL_NEAREST);
                    
                    byte[] pixels;
                    bool alpha = Settings.GlobalSettings.GetBoolValue("OpenGLAlpha", true);
                    int bytesPerPixel = alpha ? 4 : 3;
                    
                    if (alpha)
                    {
                        pixels = new byte[bitmap.Width * bitmap.Height * 4];
                        for (int i = 0; i < bitmap.Height; ++i)
                        {
                            for (int j = 0; j < bitmap.Width; ++j)
                            {
                                Color clr = bitmap.GetPixel(j, bitmap.Height - i - 1);
                                pixels[(i * bitmap.Width + j) * 4 + 0] = clr.R;
                                pixels[(i * bitmap.Width + j) * 4 + 1] = clr.G;
                                pixels[(i * bitmap.Width + j) * 4 + 2] = clr.B;
                                pixels[(i * bitmap.Width + j) * 4 + 3] = clr.A;
                            }
                        }
                        Gl.glEnable(Gl.GL_ALPHA_TEST);
                        Gl.glAlphaFunc(Gl.GL_GREATER, 0.5f);
                        Gl.glTexImage2D(Gl.GL_TEXTURE_2D, 0, Gl.GL_RGBA, bitmap.Width, bitmap.Height, 0, Gl.GL_RGBA, Gl.GL_UNSIGNED_BYTE, pixels);
                    }
                    else
                    {
                        pixels = new byte[bitmap.Width * bitmap.Height * 3];
                        for (int i = 0; i < bitmap.Height; ++i)
                        {
                            for (int j = 0; j < bitmap.Width; ++j)
                            {
                                Color clr = bitmap.GetPixel(j, bitmap.Height - i - 1);
                                pixels[(i * bitmap.Width + j) * 3 + 0] = clr.R;
                                pixels[(i * bitmap.Width + j) * 3 + 1] = clr.G;
                                pixels[(i * bitmap.Width + j) * 3 + 2] = clr.B;
                            }
                        }
                        Gl.glTexImage2D(Gl.GL_TEXTURE_2D, 0, Gl.GL_RGB, bitmap.Width, bitmap.Height, 0, Gl.GL_RGB, Gl.GL_UNSIGNED_BYTE, pixels);
                    }
                    
                    textures[bitmap] = texName;
                    
                    // Register texture with resource manager for tracking and leak detection
                    OpenGLResourceManager.RegisterTexture(texName, $"Bitmap_{bitmap.Width}x{bitmap.Height}_{texName}", bitmap.Width, bitmap.Height, bytesPerPixel);
                    OpenGLErrorHandler.LogDebug($"Texture created: {texName} ({bitmap.Width}x{bitmap.Height}, {bytesPerPixel} BPP)");
                    
                    CheckError();
                }
                catch (Exception e)
                {
                    OpenGLErrorHandler.LogError($"Exception in PrepareBitmap: {e.Message}");
                    CheckError();
                }
            }
        }
        void IPaintTo3D.RectangularBitmap(Bitmap bitmap, GeoPoint location, GeoVector directionWidth, GeoVector directionHeight)
        {
            uint texName;
            if (textures.TryGetValue(bitmap, out texName))
            {
                if (currentList != null) currentList.SetHasContents();
                // Gl.glEnable(Gl.GL_LIGHTING);
                Gl.glEnable(Gl.GL_TEXTURE_2D);
                Gl.glTexEnvf(Gl.GL_TEXTURE_ENV, Gl.GL_TEXTURE_ENV_MODE, Gl.GL_REPLACE);
                Gl.glColor4ub(255, 255, 255, 255);
                // Gl.glHint(Gl.GL_PERSPECTIVE_CORRECTION_HINT, Gl.GL_NICEST);
                Gl.glBindTexture(Gl.GL_TEXTURE_2D, texName);
                bool blend = Gl.glIsEnabled(Gl.GL_BLEND) != 0;
                Gl.glEnable(Gl.GL_BLEND); // damit der Alpha Kanal berücksichtigt wird
                Gl.glEnable(Gl.GL_ALPHA_TEST); // damit der Alpha Kanal berücksichtigt wird
                Gl.glAlphaFunc(Gl.GL_GREATER, 0.5f); // Sollte eigentlich immer so eingestellt sein
                GeoPoint p0 = location;
                GeoPoint p1 = location + directionWidth;
                GeoPoint p2 = location + directionWidth + directionHeight;
                GeoPoint p3 = location + directionHeight;
                Gl.glBegin(Gl.GL_QUADS);
                Gl.glTexCoord2d(0.0, 0.0); Gl.glVertex3d(p0.x, p0.y, p0.z);
                Gl.glTexCoord2d(0.0, 1.0); Gl.glVertex3d(p3.x, p3.y, p3.z);
                Gl.glTexCoord2d(1.0, 1.0); Gl.glVertex3d(p2.x, p2.y, p2.z);
                Gl.glTexCoord2d(1.0, 0.0); Gl.glVertex3d(p1.x, p1.y, p1.z);
                Gl.glEnd();
                Gl.glDisable(Gl.GL_TEXTURE_2D);
                Gl.glDisable(Gl.GL_ALPHA_TEST); // eingeführt wg. Hintergrund in PFOCad
                if (!blend) Gl.glDisable(Gl.GL_BLEND); // wieder zurückstellen
                CheckError();
            }
        }
        void IPaintTo3D.Text(GeoVector lineDirection, GeoVector glyphDirection, GeoPoint location, string fontName, string textString, FontStyle fontStyle, Text.AlignMode alignment, Text.LineAlignMode lineAlignment)
        {
            if (currentList != null) currentList.SetHasContents();
            if (textString.Length == 0) return;
            GeoVector normal = lineDirection ^ glyphDirection;
            FontDisplayList fdl = GetFontDisplayList(fontName);
            if (alignment != Text.AlignMode.Baseline || lineAlignment != Text.LineAlignMode.Left)
            {
                // hier location modifizieren gemäß alignment
                float dx = 0.0f;
                float dy = 0.0f;
                float yoffset = 0.0f;
                // es schein so zu sein: blackbox ist die Größe des Zeichens, also nur der Geometrie, unabhängig von der Lage
                // gm.gmfptGlyphOrigin.Y is die Oberkante, wenn also blackbox>gm.gmfptGlyphOrigin.Y, dann geht es unter die Linie
                // platziert wird immer auf der Basislinie. Wieviel maximal unter der Basislinie und über der Basislinie
                // werden kann, steht nirgends geschrieben, das gibt es nur für die einzelnen Zeichen
                // wenn man also unten oder oben platzieren will, dann ist man auf Schätzungen angewiesen
                // der Zeilenabstand scheint 1 zu sein, als machen wir hier grob: +0.2, wenn Bottom, -0.7 wenn Top
                // die Mitte wäre dann -0.25, man könnte aber auch bei der Mitte die echte Mitte des Strings nehmen
                for (int i = 0; i < textString.Length; ++i)
                {
                    CharacterDisplayList cdl;
                    if (fdl.TryGetValue(textString[i], out cdl))
                    {
                        Gdi.GLYPHMETRICSFLOAT gm = cdl.glyphmetrics;
                        dx += gm.gmfCellIncX;
                        dy += gm.gmfBlackBoxY;
                        yoffset = gm.gmfptGlyphOrigin.Y;
                    }
                }
                dy /= textString.Length;
                switch (alignment)
                {   // der Text kommt wenn unverändert angegeben an der Baseline
                    case Text.AlignMode.Baseline: break;
                    case Text.AlignMode.Bottom:
                        location = location + 0.2 * glyphDirection;
                        break;
                    case Text.AlignMode.Center:
                        location = location - 0.25 * glyphDirection;
                        break;
                    case Text.AlignMode.Top:
                        location = location - 0.7 * glyphDirection;
                        break;
                }
                switch (lineAlignment)
                {
                    case Text.LineAlignMode.Left: break;
                    case Text.LineAlignMode.Center:
                        location = location - (dx / 2) * lineDirection;
                        break;
                    case Text.LineAlignMode.Right:
                        location = location - (dx) * lineDirection;
                        break;
                }
            }
            Gl.glMatrixMode(Gl.GL_MODELVIEW); // ModelView Matrix ist und bleibt immer Identität
            Gl.glPushMatrix();
            // ACHTUNG: Matrix ist vertauscht!!!
            double[] pmat = new double[16];
            pmat[0] = lineDirection.x;
            pmat[1] = lineDirection.y;
            pmat[2] = lineDirection.z;
            pmat[3] = 0;
            pmat[4] = glyphDirection.x;
            pmat[5] = glyphDirection.y;
            pmat[6] = glyphDirection.z;
            pmat[7] = 0;
            pmat[8] = normal.x;
            pmat[9] = normal.y;
            pmat[10] = normal.z;
            pmat[11] = 0;
            pmat[12] = location.x;
            pmat[13] = location.y;
            pmat[14] = location.z;
            pmat[15] = 1;
            Gl.glLoadMatrixd(pmat);
            for (int i = 0; i < textString.Length; ++i)
            {
                CharacterDisplayList cdl;
                if (fdl.TryGetValue(textString[i], out cdl))
                {
                    Gl.glCallList(cdl.displaylist.ListNumber);
                    ++dbgNumChar;
                }
            }

            Gl.glPopMatrix();
            CheckError();
        }
        void IPaintTo3D.Nurbs(GeoPoint[] poles, double[] weights, double[] knots, int degree)
        {   // sollte nicht verwendet werden
            //if (currentList != null) currentList.SetHasContents();
            //Gl.glDisable(Gl.GL_LIGHTING);
            //Glu.gluBeginCurve(nurbsRenderer);
            //float[] fknots = new float[knots.Length];
            //for (int i = 0; i < knots.Length; ++i)
            //{
            //    fknots[i] = (float)knots[i];
            //}
            //float[] controlpoints = new float[poles.Length * 4];
            //    for (int i = 0; i < poles.Length; ++i)
            //    {
            //        controlpoints[i * 4 + 0] = (float)(poles[i].x * weights[i]);
            //        controlpoints[i * 4 + 1] = (float)(poles[i].y * weights[i]);
            //        controlpoints[i * 4 + 2] = (float)(poles[i].z * weights[i]);
            //        controlpoints[i * 4 + 3] = (float)weights[i];
            //    }
            //Glu.gluNurbsCurve(nurbsRenderer, fknots.Length, fknots, 4, controlpoints, degree + 1, Gl.GL_MAP1_VERTEX_4);
            //Glu.gluEndCurve(nurbsRenderer);
            //CheckError();
        }
        void IPaintTo3D.Line2D(int sx, int sy, int ex, int ey)
        {
            Gl.glMatrixMode(Gl.GL_PROJECTION); // ModelView Matrix ist und bleibt immer Identität
            Gl.glPushMatrix();
            Gl.glLoadIdentity();
            // die Windows-Koordinaten gehen von links unten nach rechts oben...
            Glu.gluOrtho2D(0, clientwidth, clientheight, 0);
            Gl.glDisable(Gl.GL_LIGHTING);
            Gl.glDisable(Gl.GL_LINE_SMOOTH);
            Gl.glLineWidth(1.0f);
            Gl.glBegin(Gl.GL_LINE_STRIP);
            Gl.glVertex2i(sx, sy);
            Gl.glVertex2i(ex, ey);
            Gl.glEnd();
            Gl.glPopMatrix();
            if (useLineWidth) Gl.glEnable(Gl.GL_LINE_SMOOTH);
            CheckError();
        }
        void IPaintTo3D.FillRect2D(PointF p1, PointF p2)
        {
            Gl.glMatrixMode(Gl.GL_PROJECTION); // ModelView Matrix ist und bleibt immer Identität
            Gl.glPushMatrix();
            Gl.glLoadIdentity();
            // die Windows-Koordinaten gehen von links unten nach rechts oben...
            Glu.gluOrtho2D(0, clientwidth, clientheight, 0);
            Gl.glDisable(Gl.GL_LIGHTING);
            Gl.glBegin(Gl.GL_POLYGON);
            Gl.glVertex2f(p1.X, p1.Y);
            Gl.glVertex2f(p1.X, p2.Y);
            Gl.glVertex2f(p2.X, p2.Y);
            Gl.glVertex2f(p2.X, p1.Y);
            Gl.glEnd();
            Gl.glPopMatrix();
            CheckError();
        }
        void IPaintTo3D.Point2D(int x, int y)
        {
            Gl.glMatrixMode(Gl.GL_PROJECTION); // ModelView Matrix ist und bleibt immer Identität
            Gl.glPushMatrix();
            Gl.glLoadIdentity();
            // die Windows-Koordinaten gehen von links unten nach rechts oben...
            Glu.gluOrtho2D(0, clientwidth, clientheight, 0);
            Gl.glDisable(Gl.GL_LIGHTING);
            Gl.glDisable(Gl.GL_LINE_SMOOTH);
            Gl.glBegin(Gl.GL_POINTS);
            Gl.glVertex2i(x, y);
            Gl.glEnd();
            Gl.glPopMatrix();
            if (useLineWidth) Gl.glEnable(Gl.GL_LINE_SMOOTH);
            CheckError();
        }
        void IPaintTo3D.Line2D(PointF p1, PointF p2)
        {
            Gl.glMatrixMode(Gl.GL_PROJECTION); // ModelView Matrix ist und bleibt immer Identität
            Gl.glPushMatrix();
            Gl.glLoadIdentity();
            // die Windows-Koordinaten gehen von links unten nach rechts oben...
            Glu.gluOrtho2D(0, clientwidth, clientheight, 0);
            Gl.glDisable(Gl.GL_LIGHTING);
            Gl.glDisable(Gl.GL_LINE_SMOOTH);
            Gl.glLineWidth(1.0f);
            Gl.glBegin(Gl.GL_LINE_STRIP);
            Gl.glVertex2f(p1.X, p1.Y);
            Gl.glVertex2f(p2.X, p2.Y);
            Gl.glEnd();
            Gl.glPopMatrix();
            if (useLineWidth) Gl.glEnable(Gl.GL_LINE_SMOOTH);
            CheckError();
        }
        void IPaintTo3D.DisplayIcon(GeoPoint p, Bitmap icon)
        {
            if (currentList != null) currentList.SetHasContents();
            if (icons.ContainsKey(icon))
            {
                Gl.glMatrixMode(Gl.GL_MODELVIEW); // ModelView Matrix ist und bleibt immer Identität (nein! bei BlockRef nicht!)
                Gl.glPushMatrix();
                // ACHTUNG: Matrix ist vertauscht!!!
                double[] pmat = new double[16];
                pmat[0] = 1.0;
                pmat[1] = 0.0;
                pmat[2] = 0.0;
                pmat[3] = 0;
                pmat[4] = 0.0;
                pmat[5] = 1.0;
                pmat[6] = 0.0;
                pmat[7] = 0;
                pmat[8] = 0.0;
                pmat[9] = 0.0;
                pmat[10] = 1.0;
                pmat[11] = 0;
                pmat[12] = p.x;
                pmat[13] = p.y;
                pmat[14] = p.z;
                pmat[15] = 1;
                // Gl.glDisable(Gl.GL_LINE_SMOOTH);
                Gl.glDisable(Gl.GL_LIGHTING);
                // Gl.glDisable(Gl.GL_BLEND);
                // Gl.glLoadMatrixd(pmat); // damit ging der Punkt über das Clipboard nicht, denn das zu verschiebende Objekt ist ein BlockRef
                // und der verändert die Matrix, es war also nicht die Eiheitsmatrix, wie oben geschrieben
                Gl.glMultMatrixd(pmat);
                Gl.glCallList((icons[icon] as OpenGlList).ListNumber);
                Gl.glPopMatrix();
            }
            CheckError();
        }
        void IPaintTo3D.DisplayBitmap(GeoPoint p, Bitmap bitmap)
        {
            if (currentList != null) currentList.SetHasContents();
            IPaintTo3DList list;
            if (bitmaps.TryGetValue(bitmap, out list))
            {
                Gl.glMatrixMode(Gl.GL_MODELVIEW); // ModelView Matrix ist und bleibt immer Identität
                Gl.glPushMatrix();
                // ACHTUNG: Matrix ist vertauscht!!!
                double[] pmat = new double[16];
                pmat[0] = 1.0;
                pmat[1] = 0.0;
                pmat[2] = 0.0;
                pmat[3] = 0;
                pmat[4] = 0.0;
                pmat[5] = 1.0;
                pmat[6] = 0.0;
                pmat[7] = 0;
                pmat[8] = 0.0;
                pmat[9] = 0.0;
                pmat[10] = 1.0;
                pmat[11] = 0;
                pmat[12] = p.x;
                pmat[13] = p.y;
                pmat[14] = p.z;
                pmat[15] = 1;
                // Gl.glDisable(Gl.GL_LINE_SMOOTH);
                Gl.glDisable(Gl.GL_LIGHTING);
                bool blend = Gl.glIsEnabled(Gl.GL_BLEND) != 0;
                Gl.glEnable(Gl.GL_BLEND); // damit der Alpha Kanal berücksichtigt wird
                Gl.glEnable(Gl.GL_ALPHA_TEST); // damit der Alpha Kanal berücksichtigt wird
                Gl.glAlphaFunc(Gl.GL_GREATER, 0.5f); // Sollte eigentlich immer so eingestellt sein
                Gl.glLoadMatrixd(pmat);
                Gl.glCallList((list as OpenGlList).ListNumber);
                if (!blend) Gl.glDisable(Gl.GL_BLEND); // wieder zurückstellen
                Gl.glPopMatrix();
            }
            CheckError();
        }
        void IPaintTo3D.List(IPaintTo3DList paintThisList)
        {
            if (paintThisList == null) return;
            if (currentList != null) currentList.SetHasContents();
            if ((paintThisList as OpenGlList).ListNumber != 0) Gl.glCallList((paintThisList as OpenGlList).ListNumber);
            //System.Diagnostics.Trace.WriteLine("display list: " + (paintThisList as OpenGlList).ListNumber.ToString());
            CheckError();
        }
        private void PaintListWithOffset(IPaintTo3DList paintThisList, int offsetX, int offsetY)
        {
            Gl.glMatrixMode(Gl.GL_PROJECTION);
            // leider macht die glTranslated funktion ein Matrix in der zuerst die verschiebung und dann die 
            // alte projektion stattfinden
            double[] current = new double[16];
            Gl.glGetDoublev(Gl.GL_PROJECTION_MATRIX, current);
            double[] trans = new double[16];
            trans[0] = 1.0;
            trans[1] = 0.0;
            trans[2] = 0.0;
            trans[3] = 0.0;
            trans[4] = 0.0;
            trans[5] = 1.0;
            trans[6] = 0.0;
            trans[7] = 0.0;
            trans[8] = 0.0;
            trans[9] = 0.0;
            trans[10] = 1.0;
            trans[11] = 0.0;
            trans[12] = (double)offsetX / (double)clientwidth; // da die Ausgabe sich auf einen Bereich von 0 bis 1 bezieht
            trans[13] = (double)offsetY / (double)clientheight;
            trans[14] = 0.0;
            trans[15] = 1.0;
            Gl.glPushMatrix();
            Gl.glLoadMatrixd(trans); // dies ist die gewünschte reihenfolge
            Gl.glMultMatrixd(current);
            // Gl.glTranslated(offsetX * pixelToWorld, offsetY * pixelToWorld, 0.0);
            Gl.glCallList((paintThisList as OpenGlList).ListNumber);
            Gl.glMatrixMode(Gl.GL_PROJECTION);
            Gl.glPopMatrix();
            CheckError();
        }
        void IPaintTo3D.SelectedList(IPaintTo3DList paintThisList, int wobbleRadius)
        {
            if (paintThisList == null) return;
            if (wobbleRadius <= 0)
            {
                (this as IPaintTo3D).PushState();
                Gl.glMatrixMode(Gl.GL_MODELVIEW);
                Gl.glLoadIdentity();
                Gl.glTranslated(-2 * precision * projectionDirection.x, -2 * precision * projectionDirection.y, -2 * precision * projectionDirection.z);

                //Gl.glEnable(Gl.GL_TEXTURE_2D); 
                if (wobbleRadius == -1)
                {   // we must find a different solution, clearing the depth buffer makes all subsequent calls to normal 
                    // depth drawing overwrite existing drawing
                    // Gl.glClear(Gl.GL_DEPTH_BUFFER_BIT); // Select findet über den Objekten statt, alte ZBuffer Inhalte werden gelöscht
                }
                Gl.glEnable(Gl.GL_DEPTH_TEST);

                Gl.glEnable(Gl.GL_BLEND); // damit der Alpha Kanal berücksichtigt wird
                Gl.glEnable(Gl.GL_ALPHA_TEST); // damit der Alpha Kanal berücksichtigt wird
                                               // Gl.glTexCoord2d(0.0, 0.0);
                Gl.glColor4ub(selectColor.R, selectColor.G, selectColor.B, selectColor.A); // transparent mit Farbe geht hier nicht
                Gl.glCallList((paintThisList as OpenGlList).ListNumber);

                Gl.glMatrixMode(Gl.GL_MODELVIEW);
                Gl.glLoadIdentity();

                //Gl.glDisable(Gl.GL_TEXTURE_2D);
                // Gl.glEnable(Gl.GL_DEPTH_TEST);
                (this as IPaintTo3D).PopState();
                CheckError();
            }
            else
            {
                Gl.glDisable(Gl.GL_DEPTH_TEST);
                Gl.glClearStencil(0x0);
                Gl.glEnable(Gl.GL_STENCIL_TEST);
                Gl.glClear(Gl.GL_STENCIL_BUFFER_BIT);
                Gl.glStencilFunc(Gl.GL_ALWAYS, 0x1, 0x1);
                Gl.glStencilOp(Gl.GL_REPLACE, Gl.GL_REPLACE, Gl.GL_REPLACE);
                Gl.glColorMask(false, false, false, false);
                Gl.glCallList((paintThisList as OpenGlList).ListNumber);

                Gl.glStencilFunc(Gl.GL_NOTEQUAL, 0x1, 0x1);
                Gl.glStencilOp(Gl.GL_KEEP, Gl.GL_KEEP, Gl.GL_KEEP);
                Gl.glColor4ub(selectColor.R, selectColor.G, selectColor.B, selectColor.A);
                Gl.glColorMask(true, true, true, true);

                int a = wobbleRadius, b = wobbleRadius;
                int a2 = a * a, b2 = b * b, fa2 = 4 * a2;
                int x, y, sigma;
                for (x = 0, y = b, sigma = 2 * b2 + a2 * (1 - 2 * b); b2 * x <= a2 * y; x++)
                {
                    PaintListWithOffset(paintThisList, +x, +y);
                    PaintListWithOffset(paintThisList, -x, +y);
                    PaintListWithOffset(paintThisList, +x, -y);
                    PaintListWithOffset(paintThisList, -x, -y);
                    if (sigma >= 0)
                    {
                        sigma += fa2 * (1 - y);
                        y--;
                    }
                    sigma += b2 * (4 * x + 6);
                }
                a2 = a * a;
                b2 = b * b;
                int fb2 = 4 * b2;
                for (x = a, y = 0, sigma = 2 * a2 + b2 * (1 - 2 * a); a2 * y <= b2 * x; y++)
                {
                    PaintListWithOffset(paintThisList, +x, +y);
                    PaintListWithOffset(paintThisList, -x, +y);
                    PaintListWithOffset(paintThisList, +x, -y);
                    PaintListWithOffset(paintThisList, -x, -y);
                    if (sigma >= 0)
                    {
                        sigma += fb2 * (1 - x);
                        x--;
                    }
                    sigma += a2 * (4 * y + 6);
                }
                Gl.glDisable(Gl.GL_STENCIL_TEST);
                Gl.glEnable(Gl.GL_DEPTH_TEST);
                CheckError();
            }
        }
        //void IPaintTo3D.SelectedList(IPaintTo3DList paintThisList, int wobbleRadius)
        //{
        //    if (paintThisList == null) return;
        //    // zeichnet die Liste zuerst in den Stencil Buffer, was danach als verbotene Fläche verwendet wird
        //    // Dann wird die gleiche Liste mit der Selectfarbe in den normalen RGB Buffer gezeichnet, wobei das
        //    // Objekt selbst durch den Stencil Buffer nicht überzeichnet werden kann
        //    Gl.glDisable(Gl.GL_DEPTH_TEST);
        //    Gl.glClearStencil(0x0);
        //    Gl.glEnable(Gl.GL_STENCIL_TEST); 
        //    Gl.glClear(Gl.GL_STENCIL_BUFFER_BIT);
        //    Gl.glStencilFunc(Gl.GL_ALWAYS, 0x1, 0x1);
        //    Gl.glStencilOp(Gl.GL_REPLACE, Gl.GL_REPLACE, Gl.GL_REPLACE);
        //    Gl.glColorMask(false,false,false,false);
        //    Gl.glCallList((paintThisList as OpenGlList).ListNumber);

        //    Gl.glStencilFunc(Gl.GL_NOTEQUAL, 0x1, 0x1);
        //    Gl.glStencilOp(Gl.GL_KEEP, Gl.GL_KEEP, Gl.GL_KEEP);
        //    Gl.glColor4ub(selectColor.R, selectColor.G, selectColor.B, selectColor.A);
        //    Gl.glColorMask(true, true, true, true);

        //    int a = wobbleRadius, b = wobbleRadius;
        //    int a2 = a * a, b2 = b * b, fa2 = 4 * a2;
        //    int x, y, sigma;
        //    for (x = 0, y = b, sigma = 2 * b2 + a2 * (1 - 2 * b); b2 * x <= a2 * y; x++)
        //    {
        //        PaintListWithOffset(paintThisList, +x, +y);
        //        PaintListWithOffset(paintThisList, -x, +y);
        //        PaintListWithOffset(paintThisList, +x, -y);
        //        PaintListWithOffset(paintThisList, -x, -y);
        //        if (sigma >= 0)
        //        {
        //            sigma += fa2 * (1 - y);
        //            y--;
        //        }
        //        sigma += b2 * (4 * x + 6);
        //    }
        //    a2 = a * a;
        //    b2 = b * b;
        //    int fb2 = 4 * b2;
        //    for (x = a, y = 0, sigma = 2 * a2 + b2 * (1 - 2 * a); a2 * y <= b2 * x; y++)
        //    {
        //        PaintListWithOffset(paintThisList, +x, +y);
        //        PaintListWithOffset(paintThisList, -x, +y);
        //        PaintListWithOffset(paintThisList, +x, -y);
        //        PaintListWithOffset(paintThisList, -x, -y);
        //        if (sigma >= 0)
        //        {
        //            sigma += fb2 * (1 - x);
        //            x--;
        //        }
        //        sigma += a2 * (4 * y + 6);
        //    }
        //    Gl.glDisable(Gl.GL_STENCIL_TEST);
        //    Gl.glEnable(Gl.GL_DEPTH_TEST);
        //    CheckError();
        //}
        void IPaintTo3D.OpenList(string name)
        {
            if (currentList != null) throw new PaintToOpenGLException("IPaintTo3DList: nested lists not allowed");
            currentList = new OpenGlList(name);
            currentList.Open();
            //System.Diagnostics.Trace.WriteLine("open list: " + currentList.ListNumber.ToString());
            CheckError();
        }
        IPaintTo3DList IPaintTo3D.CloseList()
        {
            if (currentList != null) currentList.Close();
            OpenGlList res = currentList;
            currentList = null;
            CheckError();
            
            // CRITICAL: Call FreeLists() to ensure the error queue is drained
            // This prevents error accumulation when closing lists
            OpenGlList.FreeLists();
            
            //System.Diagnostics.Trace.WriteLine("close list: " + res.ListNumber.ToString());
            if (res != null && res.HasContents()) return res;
            else
            {
                if (res != null) res.Delete();
                return null;
            }
        }
        IPaintTo3DList IPaintTo3D.MakeList(List<IPaintTo3DList> sublists)
        {
            StringBuilder name = new StringBuilder("_");
            foreach (IPaintTo3DList sub in sublists)
            {
                if (sub != null)
                {
                    if (sub.Name != null) name.Append(sub.Name + "_");
                }
            }
            OpenGlList res = new OpenGlList(name.ToString());
            res.Open();
            foreach (IPaintTo3DList sub in sublists)
            {
                if (sub != null)
                {
                    Gl.glCallList((sub as OpenGlList).ListNumber);
                    res.hasContents = true;
                }
            }
            res.Close();
            (res as IPaintTo3DList).containedSubLists = sublists;
            if (!res.hasContents) res.Delete();
            CheckError();
            //System.Diagnostics.Trace.WriteLine("make list: " + res.ListNumber.ToString());
            return res;
        }
        void IPaintTo3D.OpenPath()
        {
            throw new NotSupportedException("OpenGL does not support paths");
        }
        void IPaintTo3D.ClosePath(Color color)
        {
            throw new NotSupportedException("OpenGL does not support paths");
        }
        void IPaintTo3D.CloseFigure()
        {
            throw new NotSupportedException("OpenGL does not support paths");
        }
        void IPaintTo3D.Arc(GeoPoint center, GeoVector majorAxis, GeoVector minorAxis, double startParameter, double sweepParameter)
        {
            throw new NotSupportedException("OpenGL does not support arcs");
        }
        void IPaintTo3D.FreeUnusedLists()
        {
            OpenGlList.FreeLists();
        }
        void IPaintTo3D.UseZBuffer(bool use)
        {
            if (use)
            {
                Gl.glEnable(Gl.GL_DEPTH_TEST);
            }
            else
            {
                Gl.glDisable(Gl.GL_DEPTH_TEST);
            }
            CheckError();
        }
        void IPaintTo3D.FinishPaint()
        {
            try
            {
                // Buffer Swap Synchronization: Ensure all GPU operations complete before swap
                Gl.glFlush();           // Flush queued OpenGL commands
                Gl.glFinish();          // Wait for all GPU operations to complete (synchronous)
                
                // Swap buffers with error checking
                int swapResult = Gdi.SwapBuffersFast(deviceContext);
                if (swapResult == 0)
                {
                    int lastError = Marshal.GetLastWin32Error();
                    OpenGLErrorHandler.LogError($"SwapBuffers failed (error {lastError}). Frame may not display correctly.");
                }
            }
            catch (Exception e)
            {
                if (e is ThreadAbortException) throw e;
                OpenGLErrorHandler.LogError($"Exception in FinishPaint: {e.Message}");
            }
            CheckError();
        }
        class MoveFacesBehindEdgesOffset : IDisposable
        {
            PaintToOpenGL paintTo;
            public MoveFacesBehindEdgesOffset(PaintToOpenGL paintTo)
            {
                this.paintTo = paintTo;
                Gl.glMatrixMode(Gl.GL_MODELVIEW);
                Gl.glPushMatrix();
                Gl.glTranslated(paintTo.precision * paintTo.projectionDirection.x, paintTo.precision * paintTo.projectionDirection.y, paintTo.precision * paintTo.projectionDirection.z);
            }
            #region IDisposable Members
            void IDisposable.Dispose()
            {
                Gl.glMatrixMode(Gl.GL_MODELVIEW);
                Gl.glPopMatrix();
            }
            #endregion
        }
        IDisposable IPaintTo3D.FacesBehindEdgesOffset
        {
            get
            {
                return new MoveFacesBehindEdgesOffset(this);
            }
        }

        bool IPaintTo3D.IsBitmap => isBitmap;

        void IPaintTo3D.PaintFaces(PaintTo3D.PaintMode paintMode)
        {
            if (paintMode == PaintTo3D.PaintMode.FacesOnly)
            {   // Faces um die Genauigkeit nach hinten verschieben
                if (isPerspective)
                {
                    Gl.glMatrixMode(Gl.GL_PROJECTION);
                    double[] pmat = new double[16];
                    Gl.glGetDoublev(Gl.GL_PROJECTION_MATRIX, pmat);
                    double[,] mat = new double[4, 4];
                    mat[0, 0] = pmat[0];
                    mat[1, 0] = pmat[1];
                    mat[2, 0] = pmat[2];
                    mat[3, 0] = pmat[3];
                    mat[0, 1] = pmat[4];
                    mat[1, 1] = pmat[5];
                    mat[2, 1] = pmat[6];
                    mat[3, 1] = pmat[7];
                    mat[0, 2] = pmat[8];
                    mat[1, 2] = pmat[9];
                    mat[2, 2] = pmat[10];
                    mat[3, 2] = pmat[11];
                    mat[0, 3] = pmat[12];
                    mat[1, 3] = pmat[13];
                    mat[2, 3] = pmat[14];
                    mat[3, 3] = pmat[15];
                    Matrix m0 = DenseMatrix.OfArray(mat);
                    double det = m0.Determinant();
                    Matrix m1 = (Matrix)m0.Inverse();
                    Matrix trans = DenseMatrix.OfArray(new double[,] {
                    { 1, 0, 0, 0 },
                    { 0, 1, 0, 0 },
                    { 0, 0, 1, 0.001 }, // verschiebung in z un 1/1000
                    { 0, 0, 0, 1 } });
                    Matrix comp = (Matrix)(m1 * trans * m0);
                    pmat[0] = comp[0, 0];
                    pmat[1] = comp[1, 0];
                    pmat[2] = comp[2, 0];
                    pmat[3] = comp[3, 0];
                    pmat[4] = comp[0, 1];
                    pmat[5] = comp[1, 1];
                    pmat[6] = comp[2, 1];
                    pmat[7] = comp[3, 1];
                    pmat[8] = comp[0, 2];
                    pmat[9] = comp[1, 2];
                    pmat[10] = comp[2, 2];
                    pmat[11] = comp[3, 2];
                    pmat[12] = comp[0, 3];
                    pmat[13] = comp[1, 3];
                    pmat[14] = comp[2, 3];
                    pmat[15] = comp[3, 3];

                    Gl.glMatrixMode(Gl.GL_MODELVIEW);
                    Gl.glLoadMatrixd(pmat);
                }
                else
                {
                    Gl.glMatrixMode(Gl.GL_MODELVIEW);
                    Gl.glLoadIdentity();
                    Gl.glTranslated(2 * precision * projectionDirection.x, 2 * precision * projectionDirection.y, 2 * precision * projectionDirection.z);
                }
                paintSurfaces = true;
                paintEdges = false;
            }
            else if (paintMode == PaintTo3D.PaintMode.CurvesOnly)
            {   // alles andere genau am Ort darstellen
                Gl.glMatrixMode(Gl.GL_MODELVIEW);
                Gl.glLoadIdentity();
                paintSurfaces = false;
                paintEdges = true;
            }
            else
            {
                Gl.glMatrixMode(Gl.GL_MODELVIEW);
                Gl.glLoadIdentity();
                paintSurfaces = true;
                paintEdges = true;
            }
            CheckError();
        }
        void IPaintTo3D.Blending(bool on)
        {
            if (on)
            {
                Gl.glEnable(Gl.GL_BLEND);
                Gl.glBlendFunc(Gl.GL_SRC_ALPHA, Gl.GL_ONE_MINUS_SRC_ALPHA);
            }
            else
            {
                Gl.glDisable(Gl.GL_BLEND);
            }
        }
        void IPaintTo3D.PushMultModOp(ModOp mm)
        {
            Gl.glMatrixMode(Gl.GL_MODELVIEW);
            Gl.glPushMatrix();
            double[] pmat = new double[16];
            // ACHTUNG: Matrix ist vertauscht!!!
            pmat[0] = mm[0, 0];
            pmat[1] = mm[1, 0];
            pmat[2] = mm[2, 0];
            pmat[3] = 0.0;
            pmat[4] = mm[0, 1];
            pmat[5] = mm[1, 1];
            pmat[6] = mm[2, 1];
            pmat[7] = 0.0;
            pmat[8] = mm[0, 2];
            pmat[9] = mm[1, 2];
            pmat[10] = mm[2, 2];
            pmat[11] = 0.0;
            pmat[12] = mm[0, 3];
            pmat[13] = mm[1, 3];
            pmat[14] = mm[2, 3];
            pmat[15] = 1.0;
            Gl.glMultMatrixd(pmat);
        }
        void IPaintTo3D.PopModOp()
        {
            Gl.glMatrixMode(Gl.GL_MODELVIEW);
            Gl.glPopMatrix();
        }
        void IPaintTo3D.SetClip(Rectangle clipRectangle)
        {
            if (clipRectangle.IsEmpty)
            {
                Gl.glClearStencil(0x0);
                Gl.glClear(Gl.GL_STENCIL_BUFFER_BIT);
                Gl.glDisable(Gl.GL_STENCIL_TEST);
                Gl.glStencilFunc(Gl.GL_ALWAYS, 0x1, 0x1);
            }
            else
            {
                (this as IPaintTo3D).PushState(); // GL_DEPTH_TEST und GL_BLEND beibehalten
                Gl.glDisable(Gl.GL_DEPTH_TEST);
                Gl.glDisable(Gl.GL_BLEND);
                Gl.glClearStencil(0x0);
                Gl.glEnable(Gl.GL_STENCIL_TEST);
                Gl.glClear(Gl.GL_STENCIL_BUFFER_BIT);
                Gl.glStencilFunc(Gl.GL_ALWAYS, 0x1, 0x1);
                Gl.glStencilOp(Gl.GL_REPLACE, Gl.GL_REPLACE, Gl.GL_REPLACE);
                Gl.glColorMask(false, false, false, false);

                (this as IPaintTo3D).FillRect2D(new PointF((float)clipRectangle.Left, (float)clipRectangle.Bottom), new PointF((float)clipRectangle.Right, (float)clipRectangle.Top));

                Gl.glStencilFunc(Gl.GL_EQUAL, 0x1, 0x1);
                Gl.glStencilOp(Gl.GL_KEEP, Gl.GL_KEEP, Gl.GL_KEEP);
                Gl.glColorMask(true, true, true, true);
                (this as IPaintTo3D).PopState();
            }
        }
        #endregion
        
        /// <summary>
        /// Renders a collection of geometric objects to a bitmap using OpenGL
        /// </summary>
        /// <param name="list">The list of geometric objects to render</param>
        /// <param name="viewDirection">The direction from which to view the objects (e.g., (0,0,-1) for top view)</param>
        /// <param name="width">Width of the resulting bitmap in pixels</param>
        /// <param name="height">Height of the resulting bitmap in pixels</param>
        /// <param name="extent">Optional bounding cube defining the extents. If null, computed from objects</param>
        /// <returns>A bitmap containing the rendered image</returns>
        /// <remarks>
        /// This method:
        /// - Creates a temporary OpenGL context for off-screen rendering
        /// - Automatically fits the objects into the view with 10% padding
        /// - Cleans up all resources after rendering
        /// - Flips the bitmap vertically to correct for OpenGL coordinate system
        /// 
        /// The resulting bitmap has a white background. Objects are rendered with
        /// hardware acceleration if available, falling back to software rendering otherwise.
        /// </remarks>
        public static Bitmap PaintToBitmap(GeoObjectList list, GeoVector viewDirection, int width, int height, BoundingCube? extent = null)
        {
            Bitmap bmp = new Bitmap(width, height);
            Graphics gr = Graphics.FromImage(bmp);
            IntPtr dc = gr.GetHdc();
            BoundingCube bc = extent ?? list.GetExtent();
            PaintToOpenGL paintTo3D = new PaintToOpenGL(bc.Size / Math.Max(width, height));
            paintTo3D.Init(dc, width, height, true);
            IPaintTo3D ipaintTo3D = paintTo3D;
            ipaintTo3D.MakeCurrent();
            ipaintTo3D.Clear(Color.White);
            ipaintTo3D.AvoidColor(Color.White);

            Projection projection = new Projection(Projection.StandardProjection.FromTop);
            if (Precision.SameDirection(viewDirection, GeoVector.ZAxis, false)) projection.SetDirection(viewDirection, GeoVector.YAxis, bc);
            else projection.SetDirection(viewDirection, GeoVector.ZAxis, bc);
            projection.Precision = bc.Size * 1e-3;

            BoundingRect ext = bc.GetExtent(projection); //  list.GetExtent(projection, true, false);
            ext = ext * 1.1; // inflate by 10 percent 
            projection.SetPlacement(new Rectangle(0, 0, bmp.Width, bmp.Height), ext);

            ipaintTo3D.SetProjection(projection, bc);
            foreach (IGeoObject go in list)
            {
                go.PrePaintTo3D(ipaintTo3D);
            }
            foreach (IGeoObject go in list)
            {
                go.PaintTo3D(ipaintTo3D);
            }

            gr.ReleaseHdc(dc);
            bmp.RotateFlip(RotateFlipType.RotateNoneFlipY);
            ipaintTo3D.Dispose();
            gr.Dispose();
            return bmp;
        }
    }

    /// <summary>
    /// Represents an OpenGL display list for efficient rendering of compiled geometry
    /// </summary>
    /// <remarks>
    /// OpenGL display lists provide optimized rendering by compiling a sequence of OpenGL
    /// commands into a single callable unit. This class manages the lifecycle of display lists,
    /// including creation, tracking, and cleanup.
    /// 
    /// Key Features:
    /// - Automatic resource tracking and cleanup via finalizer
    /// - Deferred deletion to avoid threading issues
    /// - Global list management for shared contexts
    /// - Integration with OpenGLResourceManager for leak detection
    /// 
    /// Thread Safety: Display list operations must occur on the OpenGL context thread.
    /// </remarks>
    internal class OpenGlList : IPaintTo3DList
    {
	    private static readonly List<int> toDelete = new List<int>();
	    private static readonly Dictionary<int, string> openLists = new Dictionary<int, string>();
        private readonly object deletedLock = new object();
        public bool hasContents, isDeleted;
        public OpenGlList(string name = null)
        {
            FreeLists();
            ListNumber = Gl.glGenLists(1); // make a single list
            if (name != null) this.name = name;
            else this.name = "NoName_" + ListNumber.ToString();
            openLists[ListNumber] = this.name;
            
            // Register with resource manager for tracking
            OpenGLResourceManager.RegisterDisplayList(ListNumber, this.name, "OpenGlList.Constructor");
        }
        ~OpenGlList()
        {
            lock (toDelete)
            {
                if (!isDeleted) toDelete.Add(ListNumber);
            }
        }
		public static void FreeLists()
		{
			lock (toDelete)
			{
				if (toDelete.Count <= 0)
				{
					return;
				}

				for (int i = 0; i < toDelete.Count; ++i)
				{
					openLists.Remove(toDelete[i]);

					// Unregister from resource manager
					OpenGLResourceManager.UnregisterDisplayList(toDelete[i]);

					try
					{
						Gl.glDeleteLists(toDelete[i], 1);
						OpenGLErrorHandler.CheckError($"Deleting display list {toDelete[i]}");
					}
					catch (Exception e)
					{
						if (e is ThreadAbortException) throw (e);
						OpenGLErrorHandler.LogError($"Exception while deleting display list {toDelete[i]}: {e.Message}");
					}
				}
				toDelete.Clear();

				// CRITICAL: Drain error queue after all deletions to prevent overflow
				int errorDrainCount = 0;
				while (Gl.glGetError() != Gl.GL_NO_ERROR && errorDrainCount < 50)
				{
					errorDrainCount++;
				}
			}
		}
        /// <summary>
        /// Frees all currently open display lists and clears tracking dictionaries.
        /// Called during application shutdown to ensure complete cleanup.
        /// </summary>
        /// <remarks>
        /// CRITICAL: This method must be called with a valid OpenGL context.
        /// Iterates through ALL open lists and deletes them, then drains the error queue.
        /// Must synchronize on static collections to prevent race conditions with Delete().
        /// </remarks>
        public static void FreeAllOpenLists()
        {
            try
            {
                int deletedCount = 0;
                int failedCount = 0;
                
                // CRITICAL: Synchronize on toDelete lock to prevent race conditions with Delete() method
                // Create a snapshot of lists to delete (avoid modification during iteration)
                List<int> listsToDelete;
                lock (toDelete)
                {
                    listsToDelete = new List<int>(openLists.Keys);
                }
                
                foreach (int listNum in listsToDelete)
                {
                    try
                    {
                        Gl.glDeleteLists(listNum, 1);
                        
                        // Check for immediate errors from deletion
                        int error = Gl.glGetError();
                        if (error != Gl.GL_NO_ERROR)
                        {
                            string errorMsg = OpenGLErrorHandler.GetErrorString(error);
                            OpenGLErrorHandler.LogWarning($"FreeAllOpenLists: Error deleting list {listNum}: {errorMsg}");
                            failedCount++;
                        }
                        else
                        {
                            deletedCount++;
                        }
                        
                        // CRITICAL: Synchronize when modifying shared collections
                        lock (toDelete)
                        {
	                        openLists.Remove(listNum);
	                        // Also remove from pending deletion queue if present
                            if (toDelete.Contains(listNum)) toDelete.Remove(listNum);
                        }
                        
                        OpenGLResourceManager.UnregisterDisplayList(listNum);
                    }
                    catch (Exception ex)
                    {
                        OpenGLErrorHandler.LogError($"FreeAllOpenLists: Exception deleting list {listNum}: {ex.Message}");
                        lock (toDelete)
                        {
	                        openLists.Remove(listNum);
	                        if (toDelete.Contains(listNum)) toDelete.Remove(listNum);
                        }
                        failedCount++;
                    }
                }
                
                // Final error queue drain to ensure clean state
                int finalDrainCount = 0;
                while (Gl.glGetError() != Gl.GL_NO_ERROR && finalDrainCount < 50)
                {
                    finalDrainCount++;
                }
                
                OpenGLErrorHandler.LogDebug($"FreeAllOpenLists: Deleted {deletedCount} lists, {failedCount} failed, error queue drained {finalDrainCount} times");
            }
            catch (Exception ex)
            {
                OpenGLErrorHandler.LogError($"FreeAllOpenLists: Unexpected exception: {ex.Message}");
                // Ensure clean state even on exception
                lock (toDelete)
                {
                    openLists.Clear();
                    toDelete.Clear();
                }
            }
        }
        public int ListNumber { get; }
        public void SetHasContents()
        {
            hasContents = true;
        }
        public bool HasContents()
        {
            return hasContents;
        }
        public void Open()
        {
            // CRITICAL: Clear error queue before opening list to ensure clean state
            // This prevents accumulated errors from affecting the new list creation
            int errorClearCount = 0;
            while (Gl.glGetError() != Gl.GL_NO_ERROR && errorClearCount < 50)
            {
                errorClearCount++;
            }
            
            Gl.glNewList(ListNumber, Gl.GL_COMPILE);
            
            // Verify list was created successfully
            int error = Gl.glGetError();
            if (error != Gl.GL_NO_ERROR)
            {
                OpenGLErrorHandler.LogError($"Failed to open display list {ListNumber}: {OpenGLErrorHandler.GetErrorString(error)}");
            }
        }
        /// <summary>
        /// Closes the current display list compilation and verifies success.
        /// CRITICAL: Must be called after Open() and before rendering from this list.
        /// </summary>
        /// <remarks>
        /// Closing a list ends the compilation mode. The list is now ready for execution.
        /// Any errors during compilation are logged for diagnostics.
        /// </remarks>
        public void Close()
        {
            try
            {
                Gl.glEndList();
                
                // Verify list closed successfully
                int error = Gl.glGetError();
                if (error != Gl.GL_NO_ERROR)
                {
                    string errorMsg = OpenGLErrorHandler.GetErrorString(error);
                    OpenGLErrorHandler.LogError($"Close: Failed to close display list {ListNumber}: {errorMsg}");
                }
            }
            catch (Exception ex)
            {
                OpenGLErrorHandler.LogError($"Close: Exception closing list {ListNumber}: {ex.Message}");
            }
        }
        /// <summary>
        /// Immediately deletes this display list from GPU memory.
        /// CRITICAL: Must be called with valid OpenGL context.
        /// Once deleted, this list cannot be used for rendering.
        /// </summary>
        /// <remarks>
        /// Uses double-checked locking to prevent multiple deletions.
        /// Properly synchronizes access to shared static collections (openLists, toDelete).
        /// Unregisters from resource manager for tracking.
        /// Checks for errors from the delete operation.
        /// </remarks>
        public void Delete()
        {
            // Double-checked locking: fast path for already-deleted lists
            // ReSharper disable once InconsistentlySynchronizedField
            if (isDeleted)
                return;
            
            lock (deletedLock)
            {
                // Check again after acquiring lock
                if (isDeleted)
                    return;

                isDeleted = true;
            }
            
            // CRITICAL: Synchronize access to static collections to prevent race conditions
            lock (toDelete)  // Use toDelete lock for consistency with other static access
            {
                // Remove from tracking (even if delete fails later)
                if (!openLists.ContainsKey(ListNumber))
                {
                    OpenGLErrorHandler.LogDebug($"Delete: List {ListNumber} was not in openLists (may have already been removed)");
                }
                else
                {
                    openLists.Remove(ListNumber);
                }
                
                // Also remove from pending deletion queue if it was marked for deferred deletion
                if (toDelete.Contains(ListNumber))
                {
                    toDelete.Remove(ListNumber);
                }
            }
            
            // Unregister from resource manager
            try
            {
                OpenGLResourceManager.UnregisterDisplayList(ListNumber);
            }
            catch (Exception e)
            {
                OpenGLErrorHandler.LogError($"Delete: Error unregistering display list {ListNumber}: {e.Message}");
            }
            
            // Delete from GPU
            try
            {
                Gl.glDeleteLists(ListNumber, 1);
                
                // Check for deletion errors
                int error = Gl.glGetError();
                if (error != Gl.GL_NO_ERROR)
                {
                    string errorMsg = OpenGLErrorHandler.GetErrorString(error);
                    OpenGLErrorHandler.LogWarning($"Delete: Error deleting OpenGL list {ListNumber}: {errorMsg}");
                }
                else
                {
                    OpenGLErrorHandler.LogDebug($"Delete: OpenGL list {ListNumber} deleted successfully");
                }
            }
            catch (Exception e)
            {
                OpenGLErrorHandler.LogError($"Delete: Exception deleting OpenGL list {ListNumber}: {e.Message}");
            }
        }
        #region IPaintTo3DList Members
        private string name;
        
        // Thread-safe dictionary utilities for static collections
        private static bool TryRemove<TKey, TValue>(Dictionary<TKey, TValue> dict, TKey key, out TValue value)
        {
            value = default(TValue);
            if (dict.TryGetValue(key, out value))
            {
                dict.Remove(key);
                return true;
            }
            return false;
        }
        string IPaintTo3DList.Name
        {
            get => name;
            set => name = value;
        }
        
        /// <summary>
        /// Gets diagnostic information about all display lists.
        /// Useful for memory monitoring and leak detection.
        /// </summary>
        /// <returns>Dictionary with list numbers and their names</returns>
        public static Dictionary<int, string> GetListStatistics()
        {
            // Return a snapshot to avoid synchronization issues
            lock (openLists)
            {
                return new Dictionary<int, string>(openLists);
            }
        }
        
        /// <summary>
        /// Gets count of open lists (pending deletion).
        /// </summary>
        /// <returns>Number of lists marked for deletion</returns>
        public static int GetPendingDeletionCount()
        {
            lock (toDelete)
            {
                return toDelete.Count;
            }
        }
        
        /// <summary>
        /// Gets total count of actively tracked lists.
        /// </summary>
        /// <returns>Number of open lists</returns>
        public static int GetOpenListCount()
        {
            lock (openLists)
            {
                return openLists.Count;
            }
        }
        private List<IPaintTo3DList> keepAlive;
        
        /// <summary>
        /// Stores references to sublists that are compiled into this composite list.
        /// </summary>
        /// <remarks>
        /// CRITICAL DESIGN PATTERN: Lifetime Management of Composite Display Lists
        /// 
        /// Problem:
        /// When multiple OpenGL display lists are composed into a single parent list via glCallList,
        /// OpenGL stores only the list numbers (integers). It maintains NO references to the actual
        /// OpenGlList objects. This means the garbage collector could collect the sublist objects
        /// while the parent list still references them by number, causing:
        /// - Corruption of tracking dictionaries (openLists, toDelete)
        /// - Double-deletion errors
        /// - Orphaned GPU resources
        /// - Inconsistent cleanup timing
        /// 
        /// Solution: keepAlive Pattern
        /// This property setter stores references to all compiled sublists in the parent list object.
        /// By keeping the sublists alive as long as the parent list exists:
        /// 1. Sublists are not garbage collected until parent is deleted
        /// 2. Tracking dictionaries remain consistent
        /// 3. Deletion order is deterministic: parent → sublists
        /// 4. Resource cleanup is correct and complete
        /// 
        /// Lifecycle:
        /// - MakeList() creates parent: new OpenGlList("composite")
        /// - Calls glCallList() for each sublist (creates reference by number)
        /// - Sets containedSubLists = [sub1, sub2, ...] (stores object references)
        /// - When parent is deleted: Delete() → FreeLists() processes both parent and sublists
        /// - Sublists cleaned up after parent, maintaining consistency
        /// 
        /// Why NOT delete sublists immediately in Dispose():
        /// - Sublists must outlive parent list execution in GPU
        /// - Deferred deletion (FreeLists) respects GPU execution timing
        /// - OpenGL state may reference sublist numbers until context cleanup
        /// - Delete order matters for proper error checking and resource release
        /// 
        /// Example: Creating a block with multiple geometry sublists
        /// ```
        /// OpenGlList parent = new OpenGlList("block");
        /// parent.Open();
        /// foreach (sublist in geometryLists)
        /// {
        ///     glCallList(sublist.ListNumber);  // OpenGL records the number
        /// }
        /// parent.Close();
        /// parent.containedSubLists = geometryLists;  // Store references for lifetime management
        /// 
        /// // Later during cleanup:
        /// // - parent.Delete() executed
        /// // - Sublists still reachable via keepAlive
        /// // - FreeLists() cleans up both in correct order
        /// ```
        /// </remarks>
        List<IPaintTo3DList> IPaintTo3DList.containedSubLists
        {
            set => keepAlive = value;
        }
        /// <summary>
        /// Disposes this display list. Calls Delete() to free GPU resources.
        /// CRITICAL: keepAlive sublists are intentionally NOT deleted.
        /// They are managed separately to ensure proper cleanup order.
        /// </summary>
        /// <remarks>
        /// The keepAlive list holds references to sublists to prevent garbage collection
        /// while this list is alive. Deleting sublists here would break rendering of
        /// composite lists (those created with MakeList). Sublists are cleaned up when
        /// the parent list is deleted via FreeLists().
        /// </remarks>
        public void Dispose()
        {
            try
            {
                Delete();
                
                // IMPORTANT: Do NOT dispose keepAlive lists here!
                // These are owned by the parent list and will be cleaned up
                // through the FreeLists() mechanism to ensure proper ordering.
                
                if (keepAlive != null)
                {
                    OpenGLErrorHandler.LogDebug($"Dispose: List {ListNumber} disposing with {keepAlive.Count} sublists (not deleted - managed by parent)");
                }
            }
            catch (Exception ex)
            {
                OpenGLErrorHandler.LogError($"Dispose: Exception in Dispose for list {ListNumber}: {ex.Message}");
            }
        }
        #endregion
    }
}
