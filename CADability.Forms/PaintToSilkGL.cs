using CADability;
using CADability.Attribute;
using CADability.GeoObject;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace CADability.Forms
{
    public class PaintToSilkGL : IPaintTo3D, IPaintTo3DFlatText
    {
        private GL gl;
        private IntPtr hwnd;
        private IntPtr hdc;
        private IntPtr hglrc;

        // Off-screen rendering (see Init(Bitmap)): a hidden window hosts the GL context,
        // rendering goes into a framebuffer object which is copied into targetBitmap
        private NativeWindow hiddenWindow;
        private Bitmap targetBitmap;
        private bool isBitmap;
        private uint fbo;
        private uint fboColorRb;
        private uint fboDepthRb;

        // text glyph triangles are recorded with zero normals; the surface shader renders
        // zero-normal geometry unlit, so text appears exactly in its plain color
        private bool flatTextMode;
        public bool FlatTextMode
        {
            get { return flatTextMode; }
            set { flatTextMode = value; }
        }

        private uint surfaceProgram;
        private uint edgeProgram;

        private int surfLoc_projection;
        private int surfLoc_modelview;
        private int surfLoc_normal_matrix;
        private int surfLoc_color;
        private int surfLoc_light_pos;

        private int edgeLoc_projection;
        private int edgeLoc_modelview;
        private int edgeLoc_color;

        // GL column-major float[16] matrices
        private float[] projectionMatrix = Identity4();
        private float[] modelviewMatrix  = Identity4();
        private Stack<float[]> modopStack = new();

        private float lightX, lightY, lightZ;

        private Color currentColor = Color.White;
        private int colorLock;
        private Color avoidColor = Color.Empty;

        private int viewWidth  = 800;
        private int viewHeight = 600;

        // List recording state
        private PaintToSilkGLList currentList;
        private List<float> listSurfBuf;
        private List<float> listEdgeBuf;
        private bool inList;
        private Stack<(PaintToSilkGLList list, List<float> surf, List<float> edge, float[] mv, bool selectMode)> listNestStack = new();

        // Transient streaming VAO/VBO for immediate draw
        private uint streamVao;
        private uint streamVbo;

        private struct RenderState { public bool ZBuffer; public bool Blend; }
        private Stack<RenderState> stateStack = new();
        private bool useZBuffer = true;
        private bool blending   = true;

        // IPaintTo3D backing fields
        private bool paintSurfaces   = true;
        private bool paintEdges      = true;
        private bool paintSurfaceEdges = true;
        private bool useLineWidth    = false;
        private bool selectMode      = false;
        private Color selectColor    = Color.Yellow;
        private bool triangulateText = true;
        private double precision;
        private double pixelToWorld  = 1.0;

        public PaintToSilkGL(double precision = 1e-6)
        {
            this.precision = precision;
        }

        internal void Init(Control ctrl)
        {
            hwnd = ctrl.Handle;
            (hdc, hglrc) = WglContext.Create(hwnd);
            WglContext.MakeCurrent(hdc, hglrc);
            gl = WglContext.CreateSilkGL();
            viewWidth  = Math.Max(1, ctrl.ClientSize.Width);
            viewHeight = Math.Max(1, ctrl.ClientSize.Height);

            CompileShaders();
            InitStreamBuffer();

            gl.Enable(EnableCap.DepthTest);
            gl.Enable(EnableCap.Blend);
            gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        }

        /// <summary>
        /// Initializes for off-screen rendering into <paramref name="target"/>. Modern GL
        /// contexts cannot render into a GDI bitmap DC (the old PFD_DRAW_TO_BITMAP path only
        /// worked with the software OpenGL 1.1 renderer), so a hidden window provides the WGL
        /// context and the scene is rendered into a framebuffer object of the bitmap's size.
        /// The FBO content is copied into the bitmap by <see cref="FinishPaint"/> and as a
        /// fallback by <see cref="Dispose"/>, because some callers (printing in LayoutView)
        /// dispose without calling FinishPaint.
        /// </summary>
        internal void Init(Bitmap target)
        {
            targetBitmap = target;
            isBitmap  = true;
            viewWidth  = Math.Max(1, target.Width);
            viewHeight = Math.Max(1, target.Height);
            try
            {
                hiddenWindow = new NativeWindow();
                hiddenWindow.CreateHandle(new CreateParams { Width = 1, Height = 1 }); // no WS_VISIBLE: never shown
                hwnd = hiddenWindow.Handle;
                (hdc, hglrc) = WglContext.Create(hwnd);
                WglContext.MakeCurrent(hdc, hglrc);
                gl = WglContext.CreateSilkGL();

                CompileShaders();
                InitStreamBuffer();
                CreateFramebuffer();

                gl.Enable(EnableCap.DepthTest);
                gl.Enable(EnableCap.Blend);
                gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                gl.Viewport(0, 0, (uint)viewWidth, (uint)viewHeight);
            }
            catch
            {
                Dispose(); // don't leak the hidden window or a half-initialized context
                throw;
            }
        }

        private void CreateFramebuffer()
        {
            fbo = gl.GenFramebuffer();
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);

            fboColorRb = gl.GenRenderbuffer();
            gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, fboColorRb);
            gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.Rgba8,
                (uint)viewWidth, (uint)viewHeight);
            gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                RenderbufferTarget.Renderbuffer, fboColorRb);

            fboDepthRb = gl.GenRenderbuffer();
            gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, fboDepthRb);
            gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.Depth24Stencil8,
                (uint)viewWidth, (uint)viewHeight);
            gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment,
                RenderbufferTarget.Renderbuffer, fboDepthRb);

            gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);

            GLEnum status = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != GLEnum.FramebufferComplete)
                throw new InvalidOperationException($"Framebuffer incomplete: {status}");
        }

        /// <summary>
        /// Reads the FBO content into <see cref="targetBitmap"/>. GL rows are bottom-up,
        /// bitmaps top-down, hence the vertical flip (same as the old PaintToBitmap).
        /// </summary>
        private unsafe void CopyFramebufferToBitmap()
        {
            if (gl == null || targetBitmap == null || fbo == 0) return;
            gl.Finish();
            int w = Math.Min(viewWidth,  targetBitmap.Width);
            int h = Math.Min(viewHeight, targetBitmap.Height);
            System.Drawing.Imaging.BitmapData data = targetBitmap.LockBits(
                new Rectangle(0, 0, w, h),
                System.Drawing.Imaging.ImageLockMode.WriteOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                // Format32bppArgb is BGRA in memory; a 32bpp stride is always width*4
                gl.ReadPixels(0, 0, (uint)w, (uint)h, PixelFormat.Bgra, PixelType.UnsignedByte,
                    (void*)data.Scan0);
            }
            finally
            {
                targetBitmap.UnlockBits(data);
            }
            targetBitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);
        }

        internal void Disconnect(Control ctrl)
        {
            Dispose();
        }

        // -------------------------------------------------------------------------
        // Shader compilation
        // -------------------------------------------------------------------------

        private string ReadShader(string name)
        {
            var asm = Assembly.GetExecutingAssembly();
            using var s = asm.GetManifestResourceStream($"CADability.Forms.Shaders.{name}");
            if (s == null) throw new InvalidOperationException($"Shader not found: {name}");
            return new StreamReader(s).ReadToEnd();
        }

        private uint CompileShader(ShaderType type, string src)
        {
            uint sh = gl.CreateShader(type);
            gl.ShaderSource(sh, src);
            gl.CompileShader(sh);
            gl.GetShader(sh, ShaderParameterName.CompileStatus, out int ok);
            if (ok == 0)
            {
                string log = gl.GetShaderInfoLog(sh);
                gl.DeleteShader(sh);
                throw new InvalidOperationException($"Shader compile: {log}");
            }
            return sh;
        }

        private uint LinkProgram(uint vert, uint frag)
        {
            uint prog = gl.CreateProgram();
            gl.AttachShader(prog, vert);
            gl.AttachShader(prog, frag);
            gl.LinkProgram(prog);
            gl.GetProgram(prog, ProgramPropertyARB.LinkStatus, out int ok);
            gl.DetachShader(prog, vert); gl.DeleteShader(vert);
            gl.DetachShader(prog, frag); gl.DeleteShader(frag);
            if (ok == 0)
            {
                string log = gl.GetProgramInfoLog(prog);
                gl.DeleteProgram(prog);
                throw new InvalidOperationException($"Program link: {log}");
            }
            return prog;
        }

        private void CompileShaders()
        {
            surfaceProgram = LinkProgram(
                CompileShader(ShaderType.VertexShader,   ReadShader("surface.vert")),
                CompileShader(ShaderType.FragmentShader, ReadShader("surface.frag")));

            edgeProgram = LinkProgram(
                CompileShader(ShaderType.VertexShader,   ReadShader("edge.vert")),
                CompileShader(ShaderType.FragmentShader, ReadShader("edge.frag")));

            surfLoc_projection    = gl.GetUniformLocation(surfaceProgram, "u_projection");
            surfLoc_modelview     = gl.GetUniformLocation(surfaceProgram, "u_modelview");
            surfLoc_normal_matrix = gl.GetUniformLocation(surfaceProgram, "u_normal_matrix");
            surfLoc_color         = gl.GetUniformLocation(surfaceProgram, "u_color");
            surfLoc_light_pos     = gl.GetUniformLocation(surfaceProgram, "u_light_pos");

            edgeLoc_projection = gl.GetUniformLocation(edgeProgram, "u_projection");
            edgeLoc_modelview  = gl.GetUniformLocation(edgeProgram, "u_modelview");
            edgeLoc_color      = gl.GetUniformLocation(edgeProgram, "u_color");
        }

        // -------------------------------------------------------------------------
        // Streaming VAO/VBO for per-frame geometry (not in display lists)
        // -------------------------------------------------------------------------

        private void InitStreamBuffer()
        {
            streamVao = gl.GenVertexArray();
            streamVbo = gl.GenBuffer();
        }

        // -------------------------------------------------------------------------
        // Uniform helpers
        // -------------------------------------------------------------------------

        private void UploadSurfaceUniforms(float[] proj, float[] mv)
        {
            gl.UniformMatrix4(surfLoc_projection, 1, false, ref proj[0]);
            gl.UniformMatrix4(surfLoc_modelview,  1, false, ref mv[0]);

            // Normal matrix = upper-left 3x3 of modelview (correct for orthogonal transforms)
            float[] nm = Mat3FromGL4(mv);
            gl.UniformMatrix3(surfLoc_normal_matrix, 1, false, ref nm[0]);

            Color c = selectMode ? selectColor : currentColor;
            gl.Uniform4(surfLoc_color, c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
            gl.Uniform3(surfLoc_light_pos, lightX, lightY, lightZ);
        }

        private void UploadEdgeUniforms(float[] proj, float[] mv)
        {
            gl.UniformMatrix4(edgeLoc_projection, 1, false, ref proj[0]);
            gl.UniformMatrix4(edgeLoc_modelview,  1, false, ref mv[0]);
            Color c = selectMode ? selectColor : currentColor;
            gl.Uniform4(edgeLoc_color, c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
        }

        // -------------------------------------------------------------------------
        // Matrix utilities
        // -------------------------------------------------------------------------

        private static float[] Identity4() => new float[]
        {
            1,0,0,0,   0,1,0,0,   0,0,1,0,   0,0,0,1
        };

        private static float[] Mat3FromGL4(float[] m4)
        {
            // Extract upper-left 3x3 from column-major float[16]
            return new float[]
            {
                m4[0], m4[1], m4[2],
                m4[4], m4[5], m4[6],
                m4[8], m4[9], m4[10],
            };
        }

        // column-major 4x4 from ModOp (row-major access mm[row,col])
        private static float[] ModOpToGL(ModOp mm)
        {
            return new float[]
            {
                (float)mm[0,0], (float)mm[1,0], (float)mm[2,0], 0,
                (float)mm[0,1], (float)mm[1,1], (float)mm[2,1], 0,
                (float)mm[0,2], (float)mm[1,2], (float)mm[2,2], 0,
                (float)mm[0,3], (float)mm[1,3], (float)mm[2,3], 1,
            };
        }

        // column-major matrix multiply: result = a * b
        private static float[] Mul4(float[] a, float[] b)
        {
            var r = new float[16];
            for (int col = 0; col < 4; col++)
            for (int row = 0; row < 4; row++)
            {
                float s = 0;
                for (int k = 0; k < 4; k++) s += a[k * 4 + row] * b[col * 4 + k];
                r[col * 4 + row] = s;
            }
            return r;
        }

        private static void AddColorBatch(List<(int startVertex, Color color)> batches, int vertexCount, Color color)
        {
            if (batches.Count > 0 && batches[batches.Count - 1].color == color) return;
            batches.Add((vertexCount, color));
        }

        private static float[] Ortho2D(float w, float h)
        {
            // left=0, right=w, bottom=h, top=0, near=-1, far=1  (WinForms y-down)
            float[] m = new float[16];
            m[0]  =  2f / w;
            m[5]  = -2f / h;
            m[10] = -1f;
            m[12] = -1f;
            m[13] =  1f;
            m[15] =  1f;
            return m;
        }

        // -------------------------------------------------------------------------
        // Internal draw helpers
        // -------------------------------------------------------------------------

        private unsafe void DrawSurface(float[] verts, int vertCount)
        {
            if (vertCount == 0) return;
            gl.BindVertexArray(streamVao);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, streamVbo);
            gl.BufferData<float>(BufferTargetARB.ArrayBuffer,
                (ReadOnlySpan<float>)verts.AsSpan(0, vertCount * 6), BufferUsageARB.StreamDraw);
            gl.VertexAttribPointer(0, 3, GLEnum.Float, false, (uint)(6 * sizeof(float)), (void*)0);
            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(1, 3, GLEnum.Float, false, (uint)(6 * sizeof(float)), (void*)(3 * sizeof(float)));
            gl.EnableVertexAttribArray(1);
            gl.UseProgram(surfaceProgram);
            UploadSurfaceUniforms(projectionMatrix, modelviewMatrix);
            gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)vertCount);
            gl.BindVertexArray(0);
        }

        private unsafe void DrawEdge(float[] verts, int vertCount, PrimitiveType mode,
            float[] proj = null, float[] mv = null)
        {
            if (vertCount == 0) return;
            proj ??= projectionMatrix;
            mv   ??= modelviewMatrix;
            gl.BindVertexArray(streamVao);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, streamVbo);
            gl.BufferData<float>(BufferTargetARB.ArrayBuffer,
                (ReadOnlySpan<float>)verts.AsSpan(0, vertCount * 3), BufferUsageARB.StreamDraw);
            gl.VertexAttribPointer(0, 3, GLEnum.Float, false, (uint)(3 * sizeof(float)), (void*)0);
            gl.EnableVertexAttribArray(0);
            gl.DisableVertexAttribArray(1);
            gl.UseProgram(edgeProgram);
            UploadEdgeUniforms(proj, mv);
            gl.DrawArrays(mode, 0, (uint)vertCount);
            gl.BindVertexArray(0);
        }

        // -------------------------------------------------------------------------
        // IPaintTo3D properties
        // -------------------------------------------------------------------------

        public bool PaintSurfaces => paintSurfaces;
        public bool PaintEdges    => paintEdges;
        public bool PaintSurfaceEdges { get => paintSurfaceEdges; set => paintSurfaceEdges = value; }
        public bool UseLineWidth      { get => useLineWidth;      set => useLineWidth = value; }
        public double Precision       { get => precision;         set => precision = value; }
        public double PixelToWorld    => pixelToWorld;
        public bool SelectMode        { get => selectMode;        set => selectMode = value; }
        public Color SelectColor      { get => selectColor;       set => selectColor = value; }
        public bool DelayText         { get; set; }
        public bool DelayAll          { get; set; }
        public bool TriangulateText   { get => triangulateText;   set => triangulateText = value; }
        public bool DontRecalcTriangulation { get; set; }
        public PaintCapabilities Capabilities => PaintCapabilities.ZoomIndependentDisplayList;
        public bool IsBitmap => isBitmap;
        public IDisposable FacesBehindEdgesOffset => new PolygonOffsetScope(gl);

        private sealed class PolygonOffsetScope : IDisposable
        {
            private readonly GL _gl;
            public PolygonOffsetScope(GL gl)
            {
                _gl = gl;
                _gl.Enable(EnableCap.PolygonOffsetFill);
                _gl.PolygonOffset(1f, 4f);
            }
            public void Dispose()
            {
                _gl.PolygonOffset(0, 0);
                _gl.Disable(EnableCap.PolygonOffsetFill);
            }
        }

        // -------------------------------------------------------------------------
        // IPaintTo3D methods
        // -------------------------------------------------------------------------

        public void MakeCurrent()
        {
            WglContext.MakeCurrent(hdc, hglrc);
            if (isBitmap && fbo != 0) gl?.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
        }

        public void SetColor(Color color, int lockColor = 0)
        {
            if (lockColor == -1) { colorLock = 0; return; }
            if (colorLock != 0 && lockColor == 0) return;
            if (lockColor == 1) colorLock = 1;
            if (color == avoidColor && avoidColor != Color.Empty)
                color = Color.FromArgb(color.A, (color.R + 20) % 256,
                    (color.G + 20) % 256, (color.B + 20) % 256);
            currentColor = color;
            if (inList)
            {
                AddColorBatch(currentList.SurfaceBatches, currentList.SurfaceVertexCount, color);
                AddColorBatch(currentList.EdgeBatches,    currentList.EdgeVertexCount,    color);
            }
        }

        public void AvoidColor(Color color) { avoidColor = color; }

        public void SetLineWidth(LineWidth lineWidth)
        {
            gl?.LineWidth(lineWidth == null ? 1f : Math.Max(1f, (float)lineWidth.Width));
        }

        public void SetLinePattern(LinePattern pattern) { /* dash via shader — phase 3 */ }

        public void Polyline(GeoPoint[] points)
        {
            if (points == null || points.Length < 2) return;

            if (inList)
            {
                // Polylines in a list are stored as line segments
                for (int i = 0; i + 1 < points.Length; i++)
                {
                    listEdgeBuf.Add((float)points[i].x);
                    listEdgeBuf.Add((float)points[i].y);
                    listEdgeBuf.Add((float)points[i].z);
                    listEdgeBuf.Add((float)points[i + 1].x);
                    listEdgeBuf.Add((float)points[i + 1].y);
                    listEdgeBuf.Add((float)points[i + 1].z);
                    currentList.EdgeVertexCount += 2;
                }
                return;
            }

            var v = new float[(points.Length - 1) * 6];
            for (int i = 0; i + 1 < points.Length; i++)
            {
                int o = i * 6;
                v[o+0] = (float)points[i].x;     v[o+1] = (float)points[i].y;     v[o+2] = (float)points[i].z;
                v[o+3] = (float)points[i+1].x;   v[o+4] = (float)points[i+1].y;   v[o+5] = (float)points[i+1].z;
            }
            DrawEdge(v, (points.Length - 1) * 2, PrimitiveType.Lines);
        }

        public void FilledPolyline(GeoPoint[] points) { /* deprecated */ }

        public void Points(GeoPoint[] points, float size, PointSymbol pointSymbol)
        {
            if (points == null || points.Length == 0) return;
            gl.PointSize(size);
            var v = new float[points.Length * 3];
            for (int i = 0; i < points.Length; i++)
            {
                v[i*3+0] = (float)points[i].x;
                v[i*3+1] = (float)points[i].y;
                v[i*3+2] = (float)points[i].z;
            }
            DrawEdge(v, points.Length, PrimitiveType.Points);
        }

        public void Triangle(GeoPoint[] vertex, GeoVector[] normals, int[] indextriples)
        {
            if (vertex == null || indextriples == null || indextriples.Length == 0) return;
            int vc = indextriples.Length;

            if (inList)
            {
                for (int i = 0; i < vc; i++)
                {
                    int idx = indextriples[i];
                    listSurfBuf.Add((float)vertex[idx].x);
                    listSurfBuf.Add((float)vertex[idx].y);
                    listSurfBuf.Add((float)vertex[idx].z);
                    if (flatTextMode)
                    {   // zero normal marks unlit geometry (text glyphs) for the surface shader
                        listSurfBuf.Add(0); listSurfBuf.Add(0); listSurfBuf.Add(0);
                    }
                    else if (normals != null && idx < normals.Length)
                    {
                        listSurfBuf.Add((float)normals[idx].x);
                        listSurfBuf.Add((float)normals[idx].y);
                        listSurfBuf.Add((float)normals[idx].z);
                    }
                    else { listSurfBuf.Add(0); listSurfBuf.Add(0); listSurfBuf.Add(1); }
                    currentList.SurfaceVertexCount++;
                }
                return;
            }

            var v = new float[vc * 6];
            for (int i = 0; i < vc; i++)
            {
                int idx = indextriples[i];
                v[i*6+0] = (float)vertex[idx].x;
                v[i*6+1] = (float)vertex[idx].y;
                v[i*6+2] = (float)vertex[idx].z;
                if (flatTextMode)
                {   // zero normal marks unlit geometry (text glyphs) for the surface shader
                    v[i*6+3]=0; v[i*6+4]=0; v[i*6+5]=0;
                }
                else if (normals != null && idx < normals.Length)
                {
                    v[i*6+3] = (float)normals[idx].x;
                    v[i*6+4] = (float)normals[idx].y;
                    v[i*6+5] = (float)normals[idx].z;
                }
                else { v[i*6+3]=0; v[i*6+4]=0; v[i*6+5]=1; }
            }
            DrawSurface(v, vc);
        }

        public void PrepareText(string fontName, string textString, FontStyle fontStyle) { }
        public void PreparePointSymbol(PointSymbol pointSymbol) { }
        public void PrepareIcon(Bitmap icon) { }
        public void PrepareBitmap(Bitmap bitmap, int xoffset, int yoffset) { }
        public void PrepareBitmap(Bitmap bitmap) { }

        public void RectangularBitmap(Bitmap bitmap, GeoPoint location,
            GeoVector directionWidth, GeoVector directionHeight) { /* phase 4 */ }

        public void Text(GeoVector lineDirection, GeoVector glyphDirection, GeoPoint location,
            string fontName, string textString, FontStyle fontStyle,
            CADability.GeoObject.Text.AlignMode alignment,
            CADability.GeoObject.Text.LineAlignMode lineAlignment)
        {
            // Direct text calls bypass the Text GeoObject (e.g. the x/y/z axis labels of the
            // coordinate cross in ModelView.PaintCoordCross), so nothing triangulates for us
            // here. Route the call through a transient Text object: its PrePaintTo3D/PaintTo3D
            // triangulate the glyphs via the font cache and feed them back through
            // List()/Triangle(), including the unlit FlatTextMode handling.
            if (string.IsNullOrEmpty(textString)) return;
            if (lineDirection.IsNullVector() || glyphDirection.IsNullVector()) return;

            GeoObject.Text text = GeoObject.Text.Construct();
            text.Font = fontName;
            text.TextString = textString;
            text.Location = location;
            text.SetDirections(lineDirection, glyphDirection);
            text.Alignment = alignment;
            text.LineAlignment = lineAlignment;
            text.Bold      = (fontStyle & FontStyle.Bold) != 0;
            text.Italic    = (fontStyle & FontStyle.Italic) != 0;
            text.Underline = (fontStyle & FontStyle.Underline) != 0;
            text.Strikeout = (fontStyle & FontStyle.Strikeout) != 0;

            bool savedTriangulate = triangulateText;
            bool savedSurfaces    = paintSurfaces;
            bool savedEdges       = paintEdges;
            triangulateText = true; // with TriangulateText == false Text.PaintTo3D would call back into this method
            // The glyphs replay via List(), which is gated by paintSurfaces/paintEdges. The coordinate
            // cross is painted right after the model's curve phase (PaintFaces(CurvesOnly), so
            // paintSurfaces is false there) — the old renderer drew text regardless of the paint mode,
            // so force the flags on for the duration of this call.
            paintSurfaces = true;
            paintEdges    = true;
            try
            {
                text.PrePaintTo3D(this); // creates the per-character display lists in the font cache
                text.PaintTo3D(this);    // replays them at the character positions
            }
            finally
            {
                triangulateText = savedTriangulate;
                paintSurfaces   = savedSurfaces;
                paintEdges      = savedEdges;
            }
        }

        private void DrawListSurface(PaintToSilkGLList list)
        {
            if (list.SurfaceVao == 0 || list.SurfaceVertexCount == 0) return;
            gl.BindVertexArray(list.SurfaceVao);
            gl.UseProgram(surfaceProgram);
            var batches = list.SurfaceBatches;
            if (batches == null || batches.Count == 0)
            {
                UploadSurfaceUniforms(projectionMatrix, modelviewMatrix);
                gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)list.SurfaceVertexCount);
            }
            else
            {
                Color savedColor = currentColor;
                for (int i = 0; i < batches.Count; i++)
                {
                    int first = batches[i].startVertex;
                    int end   = (i + 1 < batches.Count) ? batches[i + 1].startVertex : list.SurfaceVertexCount;
                    if (end <= first) continue;
                    currentColor = batches[i].color;
                    UploadSurfaceUniforms(projectionMatrix, modelviewMatrix);
                    gl.DrawArrays(PrimitiveType.Triangles, first, (uint)(end - first));
                }
                currentColor = savedColor;
            }
            gl.BindVertexArray(0);
        }

        private void DrawListEdge(PaintToSilkGLList list)
        {
            if (list.EdgeVao == 0 || list.EdgeVertexCount == 0) return;
            gl.BindVertexArray(list.EdgeVao);
            gl.UseProgram(edgeProgram);
            var batches = list.EdgeBatches;
            if (batches == null || batches.Count == 0)
            {
                UploadEdgeUniforms(projectionMatrix, modelviewMatrix);
                gl.DrawArrays(PrimitiveType.Lines, 0, (uint)list.EdgeVertexCount);
            }
            else
            {
                Color savedColor = currentColor;
                for (int i = 0; i < batches.Count; i++)
                {
                    int first = batches[i].startVertex;
                    int end   = (i + 1 < batches.Count) ? batches[i + 1].startVertex : list.EdgeVertexCount;
                    if (end <= first) continue;
                    currentColor = batches[i].color;
                    UploadEdgeUniforms(projectionMatrix, modelviewMatrix);
                    gl.DrawArrays(PrimitiveType.Lines, first, (uint)(end - first));
                }
                currentColor = savedColor;
            }
            gl.BindVertexArray(0);
        }

        public void List(IPaintTo3DList paintThisList)
        {
            if (paintThisList is not PaintToSilkGLList list) return;

            // Recording mode: capture as a sub-list call with the current modelview and color
            if (inList)
            {
                (currentList.SubListCalls ??= new()).Add(new PaintToSilkGLList.SubListCall
                {
                    List      = list,
                    ModelView = (float[])modelviewMatrix.Clone(),
                    Color     = currentColor,
                });
                return;
            }

            if (!list.IsGpuUploaded) list.UploadToGpu(gl);

            if (paintSurfaces) DrawListSurface(list);
            if (paintEdges)    DrawListEdge(list);

            // Replay static composite sub-lists (from MakeList)
            if (list.SubLists != null)
                foreach (var sub in list.SubLists) List(sub);

            // Replay dynamic sub-list calls recorded during OpenList (e.g. text characters)
            if (list.SubListCalls != null)
            {
                float[] savedMV    = modelviewMatrix;
                Color   savedColor = currentColor;
                foreach (var call in list.SubListCalls)
                {
                    modelviewMatrix = call.ModelView;
                    currentColor    = call.Color;
                    List(call.List);
                }
                modelviewMatrix = savedMV;
                currentColor    = savedColor;
            }
        }

        public void SelectedList(IPaintTo3DList paintThisList, int wobbleRadius)
        {
            float[] savedProj = projectionMatrix;

            // Draw all geometry in select color at every shifted position — yellow halo.
            // Surfaces must be included here because text glyphs are surface-only geometry
            // (TriangulateText=true); edge-only halo produces nothing visible for text.
            // The center draw (original colors) is rendered on top via the depth test.
            bool prevSurfaces = paintSurfaces;
            bool prevEdges    = paintEdges;
            bool prevSelect   = selectMode;
            paintSurfaces = true;
            paintEdges    = true;
            selectMode    = true;
            for (int dx = -wobbleRadius; dx <= wobbleRadius; dx++)
            for (int dy = -wobbleRadius; dy <= wobbleRadius; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                projectionMatrix = (float[])savedProj.Clone();
                projectionMatrix[12] += dx * 2f / viewWidth;
                projectionMatrix[13] += dy * 2f / viewHeight;
                List(paintThisList);
            }
            paintSurfaces = prevSurfaces;
            paintEdges    = prevEdges;
            selectMode    = false;

            // Draw at center with original colors. Use GL_LEQUAL so this pass overwrites
            // yellow pixels at the same depth (the shifted halo passes wrote the same Z
            // values; GL_LESS would reject equal-depth fragments, leaving all yellow).
            gl.DepthFunc(DepthFunction.Lequal);
            projectionMatrix = (float[])savedProj.Clone();
            List(paintThisList);
            projectionMatrix = savedProj;
            gl.DepthFunc(DepthFunction.Less);

            selectMode = prevSelect;
        }

        public void Nurbs(GeoPoint[] poles, double[] weights, double[] knots, int degree) { }

        public void Line2D(int sx, int sy, int ex, int ey)
        {
            float[] ortho = Ortho2D(viewWidth, viewHeight);
            float[] iden  = Identity4();
            var v = new float[] { sx, sy, 0, ex, ey, 0 };
            DrawEdge(v, 2, PrimitiveType.Lines, ortho, iden);
        }

        public void Line2D(PointF p1, PointF p2)
            => Line2D((int)p1.X, (int)p1.Y, (int)p2.X, (int)p2.Y);

        public void FillRect2D(PointF p1, PointF p2)
        {
            float[] ortho = Ortho2D(viewWidth, viewHeight);
            float[] iden  = Identity4();
            var v = new float[]
            {
                p1.X, p1.Y, 0,  p2.X, p1.Y, 0,  p2.X, p2.Y, 0,
                p1.X, p1.Y, 0,  p2.X, p2.Y, 0,  p1.X, p2.Y, 0,
            };
            DrawEdge(v, 6, PrimitiveType.Triangles, ortho, iden);
        }

        public void Point2D(int x, int y) { /* deprecated */ }

        public void DisplayIcon(GeoPoint p, Bitmap icon) { /* phase 4 */ }
        public void DisplayBitmap(GeoPoint p, Bitmap bitmap) { /* phase 4 */ }

        public void SetProjection(Projection projection, BoundingCube boundingCube)
        {
            if (gl == null) return;
            gl.Viewport(0, 0, (uint)viewWidth, (uint)viewHeight);

            // Obtain projection matrix in the same way as PaintToOpenGL
            double[,] mm = projection.GetOpenGLProjection(0, viewWidth, 0, viewHeight, boundingCube);
            projectionMatrix = new float[]
            {
                (float)mm[0,0], (float)mm[1,0], (float)mm[2,0], (float)mm[3,0],
                (float)mm[0,1], (float)mm[1,1], (float)mm[2,1], (float)mm[3,1],
                (float)mm[0,2], (float)mm[1,2], (float)mm[2,2], (float)mm[3,2],
                (float)mm[0,3], (float)mm[1,3], (float)mm[2,3], (float)mm[3,3],
            };

            modelviewMatrix = Identity4();

            // Light direction in world space (same formula as PaintToOpenGL)
            GeoVector v = projection.InverseProjection * new GeoVector(0.5, 0.3, -1.0);
            lightX = (float)v.x;
            lightY = (float)v.y;
            lightZ = (float)v.z;

            pixelToWorld = projection.DeviceToWorldFactor;
            useLineWidth = projection.UseLineWidth;
            if (useLineWidth) gl.Enable(EnableCap.LineSmooth);
            else              gl.Disable(EnableCap.LineSmooth);
        }

        public void Clear(Color background)
        {
            gl.ClearColor(background.R/255f, background.G/255f, background.B/255f, 1f);
            gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        }

        public void Resize(int width, int height)
        {
            viewWidth  = width;
            viewHeight = height;
            gl?.Viewport(0, 0, (uint)width, (uint)height);
        }

        public void OpenList(string name = null)
        {
            // Always record with selectMode=false so SubListCall colors capture the object's
            // own color, not the caller's selection color. selectMode only applies at replay time.
            listNestStack.Push((currentList, listSurfBuf, listEdgeBuf, modelviewMatrix, selectMode));
            selectMode  = false;
            currentList = new PaintToSilkGLList { Name = name };
            listSurfBuf = new List<float>();
            listEdgeBuf = new List<float>();
            currentList.SurfaceVertexCount = 0;
            currentList.EdgeVertexCount    = 0;
            inList = true;
        }

        public IPaintTo3DList CloseList()
        {
            var list = currentList;
            if (listSurfBuf.Count > 0) list.SurfaceVertices = listSurfBuf.ToArray();
            if (listEdgeBuf.Count > 0) list.EdgeVertices    = listEdgeBuf.ToArray();
            list.UploadToGpu(gl);

            if (listNestStack.Count > 0)
            {
                (currentList, listSurfBuf, listEdgeBuf, modelviewMatrix, selectMode) = listNestStack.Pop();
                inList = listNestStack.Count > 0 || currentList != null;
            }
            else
            {
                inList      = false;
                listSurfBuf = null;
                listEdgeBuf = null;
                currentList = null;
            }
            return list;
        }

        public IPaintTo3DList MakeList(List<IPaintTo3DList> sublists)
        {
            var list = new PaintToSilkGLList { Name = "composite" };
            list.containedSubLists = new List<IPaintTo3DList>(sublists);
            return list;
        }

        public void OpenPath() { }
        public void ClosePath(Color color) { /* paths arrive triangulated */ }
        public void CloseFigure() { }

        public void Arc(GeoPoint center, GeoVector majorAxis, GeoVector minorAxis,
            double startParameter, double sweepParameter)
        {
            int segs = Math.Max(8, (int)(Math.Abs(sweepParameter) / (Math.PI / 16)));
            var pts = new GeoPoint[segs + 1];
            for (int i = 0; i <= segs; i++)
            {
                double t = startParameter + sweepParameter * i / segs;
                pts[i] = center + Math.Cos(t) * majorAxis + Math.Sin(t) * minorAxis;
            }
            Polyline(pts);
        }

        public void FreeUnusedLists() { /* GC and Dispose handle GPU resources */ }

        public void UseZBuffer(bool use)
        {
            useZBuffer = use;
            if (use) gl?.Enable(EnableCap.DepthTest);
            else     gl?.Disable(EnableCap.DepthTest);
        }

        public void Blending(bool on)
        {
            blending = on;
            if (on) gl?.Enable(EnableCap.Blend);
            else    gl?.Disable(EnableCap.Blend);
        }

        public void FinishPaint()
        {
            if (isBitmap) CopyFramebufferToBitmap(); // off-screen: no swap chain, read back instead
            else WglContext.Present(hdc);
        }

        public void PaintFaces(PaintTo3D.PaintMode mode)
        {
            paintSurfaces = mode != PaintTo3D.PaintMode.CurvesOnly;
            paintEdges    = mode != PaintTo3D.PaintMode.FacesOnly;

            if (mode == PaintTo3D.PaintMode.FacesOnly)
            {
                gl?.Enable(EnableCap.PolygonOffsetFill);
                gl?.PolygonOffset(1f, 4f);
            }
            else
            {
                gl?.Disable(EnableCap.PolygonOffsetFill);
                gl?.PolygonOffset(0, 0);
            }
        }

        public void PushState()
        {
            stateStack.Push(new RenderState { ZBuffer = useZBuffer, Blend = blending });
        }

        public void PopState()
        {
            if (stateStack.Count == 0) return;
            var s = stateStack.Pop();
            UseZBuffer(s.ZBuffer);
            Blending(s.Blend);
        }

        public void PushMultModOp(ModOp mm)
        {
            modopStack.Push(modelviewMatrix);
            // new modelview = current * ModOp  (matches glMultMatrix semantics)
            modelviewMatrix = Mul4(modelviewMatrix, ModOpToGL(mm));
        }

        public void PopModOp()
        {
            if (modopStack.Count > 0) modelviewMatrix = modopStack.Pop();
        }

        public void SetClip(Rectangle clipRectangle)
        {
            if (clipRectangle.IsEmpty)
            {
                gl?.Disable(EnableCap.ScissorTest);
            }
            else
            {
                gl?.Enable(EnableCap.ScissorTest);
                // WinForms y=0 is at top; OpenGL scissor y=0 is at bottom
                gl?.Scissor(clipRectangle.X, viewHeight - clipRectangle.Bottom,
                    (uint)clipRectangle.Width, (uint)clipRectangle.Height);
            }
        }

        public void Dispose()
        {
            if (isBitmap && gl != null && hglrc != IntPtr.Zero)
            {
                // Printing (LayoutView) disposes without calling FinishPaint: make sure the
                // rendered image reaches the bitmap. Best effort — FinishPaint is the primary
                // copy point, so a failure here must not turn cleanup into a crash.
                try
                {
                    MakeCurrent();
                    CopyFramebufferToBitmap();
                }
                catch (Exception) { }
            }
            if (gl != null)
            {
                if (surfaceProgram != 0) gl.DeleteProgram(surfaceProgram);
                if (edgeProgram    != 0) gl.DeleteProgram(edgeProgram);
                if (streamVao != 0) gl.DeleteVertexArray(streamVao);
                if (streamVbo != 0) gl.DeleteBuffer(streamVbo);
                if (fbo        != 0) { gl.DeleteFramebuffer(fbo);         fbo = 0; }
                if (fboColorRb != 0) { gl.DeleteRenderbuffer(fboColorRb); fboColorRb = 0; }
                if (fboDepthRb != 0) { gl.DeleteRenderbuffer(fboDepthRb); fboDepthRb = 0; }
                gl.Dispose();
                gl = null;
            }
            if (hglrc != IntPtr.Zero)
            {
                WglContext.Delete(hdc, hwnd, hglrc);
                hglrc = IntPtr.Zero;
                hdc   = IntPtr.Zero;
            }
            if (hiddenWindow != null)
            {
                hiddenWindow.DestroyHandle();
                hiddenWindow = null;
                hwnd = IntPtr.Zero;
            }
            targetBitmap = null;
        }

        /// <summary>
        /// Renders a list of geo objects to a bitmap using off-screen FBO rendering
        /// (projection setup identical to the old PaintToOpenGL.PaintToBitmap).
        /// </summary>
        public static Bitmap PaintToBitmap(GeoObjectList list, GeoVector viewDirection,
            int width, int height, BoundingCube? extent = null)
        {
            BoundingCube bc = extent ?? list.GetExtent();
            Bitmap bmp = new Bitmap(width, height);
            PaintToSilkGL paintTo3D = new PaintToSilkGL(bc.Size / Math.Max(width, height));
            IPaintTo3D ipaintTo3D = paintTo3D;
            try
            {
                paintTo3D.Init(bmp);
                ipaintTo3D.MakeCurrent();
                ipaintTo3D.Clear(Color.White);
                ipaintTo3D.AvoidColor(Color.White);

                Projection projection = new Projection(Projection.StandardProjection.FromTop);
                if (CADability.Precision.SameDirection(viewDirection, GeoVector.ZAxis, false))
                    projection.SetDirection(viewDirection, GeoVector.YAxis, bc);
                else
                    projection.SetDirection(viewDirection, GeoVector.ZAxis, bc);
                projection.Precision = bc.Size * 1e-3;

                BoundingRect ext = bc.GetExtent(projection);
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
                ipaintTo3D.FinishPaint(); // copies the FBO into bmp
            }
            finally
            {
                ipaintTo3D.Dispose();
            }
            return bmp;
        }
    }
}
