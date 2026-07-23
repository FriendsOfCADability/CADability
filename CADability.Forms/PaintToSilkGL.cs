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

        // One WGL context shared by all views (and off-screen renderers): the categorized
        // display lists live on the Model and are painted by every view, so their VAOs/VBOs
        // (and the font/texture caches) must be valid in each of them. A WGL context can be
        // made current against any DC with the same pixel format. Replacement for the old
        // MainRenderContext + wglShareLists mechanism; lives until the process ends.
        private static IntPtr sharedHglrc = IntPtr.Zero;
        private static GL sharedGl;

        private void InitSharedContext(IntPtr windowHandle)
        {
            hdc = WglContext.PrepareDC(windowHandle);
            if (sharedHglrc == IntPtr.Zero)
            {
                sharedHglrc = WglContext.CreateContext(hdc);
                WglContext.MakeCurrent(hdc, sharedHglrc);
                sharedGl = WglContext.CreateSilkGL();
            }
            hglrc = sharedHglrc;
            WglContext.MakeCurrent(hdc, hglrc);
            gl = sharedGl;
        }

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
        private uint textureProgram;

        private int texLoc_projection;
        private int texLoc_modelview;
        private int texLoc_color;

        // bitmap → GL texture caches (uploaded by the Prepare* methods, deleted in Dispose)
        private readonly Dictionary<Bitmap, uint> textures = new();                       // RectangularBitmap (Picture objects)
        private readonly Dictionary<Bitmap, (uint tex, int xoff, int yoff)> sprites = new(); // DisplayBitmap (unscaled icons with anchor)
        private readonly Dictionary<Bitmap, uint> iconMasks = new();                      // DisplayIcon (mask in current color)

        // point symbol bitmaps: 0-5 thin (dot, plus, cross, line, square, circle),
        // 6-11 thin or bold depending on the PointSymbolsBold setting, 12 filled square (select)
        private static List<Bitmap> bitmapList = null;
        internal static List<Bitmap> BitmapList
        {
            get
            {
                if (bitmapList == null)
                {
                    bitmapList = new List<Bitmap>();
                    Bitmap bmp = BitmapTable.GetBitmap("PointSymbols.bmp");
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

        private int surfLoc_projection;
        private int surfLoc_modelview;
        private int surfLoc_normal_matrix;
        private int surfLoc_color;
        private int surfLoc_light_pos;
        private int surfLoc_view_dir;

        private int edgeLoc_projection;
        private int edgeLoc_modelview;
        private int edgeLoc_color;
        private int edgeLoc_pattern;
        private int edgeLoc_patternCount;
        private int edgeLoc_patternTotal;
        private int edgeLoc_distScale;

        // current dash pattern as pixel segment lengths (alternating on/off, starting on);
        // null = solid. Like the old glLineStipple path, patterns repeat over 16 pixels.
        private float[] currentPatternPx;

        // GL column-major float[16] matrices
        private float[] projectionMatrix = Identity4();
        private float[] modelviewMatrix  = Identity4();
        private Stack<float[]> modopStack = new();

        private float lightX, lightY, lightZ;
        private float viewDirX = 0f, viewDirY = 0f, viewDirZ = 1f; // toward the viewer, world space

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
            InitSharedContext(hwnd);
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
                InitSharedContext(hwnd);

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
            surfLoc_view_dir      = gl.GetUniformLocation(surfaceProgram, "u_view_dir");

            edgeLoc_projection   = gl.GetUniformLocation(edgeProgram, "u_projection");
            edgeLoc_modelview    = gl.GetUniformLocation(edgeProgram, "u_modelview");
            edgeLoc_color        = gl.GetUniformLocation(edgeProgram, "u_color");
            edgeLoc_pattern      = gl.GetUniformLocation(edgeProgram, "u_pattern");
            edgeLoc_patternCount = gl.GetUniformLocation(edgeProgram, "u_patternCount");
            edgeLoc_patternTotal = gl.GetUniformLocation(edgeProgram, "u_patternTotal");
            edgeLoc_distScale    = gl.GetUniformLocation(edgeProgram, "u_distScale");

            textureProgram = LinkProgram(
                CompileShader(ShaderType.VertexShader,   ReadShader("texture.vert")),
                CompileShader(ShaderType.FragmentShader, ReadShader("texture.frag")));

            texLoc_projection = gl.GetUniformLocation(textureProgram, "u_projection");
            texLoc_modelview  = gl.GetUniformLocation(textureProgram, "u_modelview");
            texLoc_color      = gl.GetUniformLocation(textureProgram, "u_color");
            gl.UseProgram(textureProgram);
            gl.Uniform1(gl.GetUniformLocation(textureProgram, "u_texture"), 0); // sampler on texture unit 0
            gl.UseProgram(0);
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
            gl.Uniform3(surfLoc_view_dir, viewDirX, viewDirY, viewDirZ);
        }

        private void UploadEdgeUniforms(float[] proj, float[] mv)
        {
            gl.UniformMatrix4(edgeLoc_projection, 1, false, ref proj[0]);
            gl.UniformMatrix4(edgeLoc_modelview,  1, false, ref mv[0]);
            Color c = selectMode ? selectColor : currentColor;
            gl.Uniform4(edgeLoc_color, c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
        }

        // must be called with the edge program in use; pattern == null means solid
        private void UploadEdgePattern(float[] pattern)
        {
            if (pattern == null || pattern.Length == 0)
            {
                gl.Uniform1(edgeLoc_patternCount, 0);
                return;
            }
            float total = 0f;
            for (int i = 0; i < pattern.Length; ++i) total += pattern[i];
            gl.Uniform1(edgeLoc_patternCount, pattern.Length);
            gl.Uniform1(edgeLoc_patternTotal, total);
            gl.Uniform1(edgeLoc_pattern, (uint)pattern.Length, ref pattern[0]);
            // the pattern is defined in screen pixels, the distance attribute in world units
            gl.Uniform1(edgeLoc_distScale, pixelToWorld > 0.0 ? (float)(1.0 / pixelToWorld) : 1f);
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

        private static void AddEdgeBatch(List<(int startVertex, Color color, float[] pattern)> batches,
            int vertexCount, Color color, float[] pattern)
        {
            if (batches.Count > 0)
            {
                var last = batches[batches.Count - 1];
                if (last.color == color && ReferenceEquals(last.pattern, pattern)) return;
            }
            batches.Add((vertexCount, color, pattern));
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

        // verts: 4 floats per vertex (position xyz + cumulative polyline distance)
        private unsafe void DrawEdge(float[] verts, int vertCount, PrimitiveType mode,
            float[] proj = null, float[] mv = null, bool applyPattern = false)
        {
            if (vertCount == 0) return;
            proj ??= projectionMatrix;
            mv   ??= modelviewMatrix;
            gl.BindVertexArray(streamVao);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, streamVbo);
            gl.BufferData<float>(BufferTargetARB.ArrayBuffer,
                (ReadOnlySpan<float>)verts.AsSpan(0, vertCount * 4), BufferUsageARB.StreamDraw);
            gl.VertexAttribPointer(0, 3, GLEnum.Float, false, (uint)(4 * sizeof(float)), (void*)0);
            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(1, 1, GLEnum.Float, false, (uint)(4 * sizeof(float)), (void*)(3 * sizeof(float)));
            gl.EnableVertexAttribArray(1);
            gl.UseProgram(edgeProgram);
            UploadEdgeUniforms(proj, mv);
            UploadEdgePattern(applyPattern ? currentPatternPx : null);
            gl.DrawArrays(mode, 0, (uint)vertCount);
            gl.BindVertexArray(0);
        }

        /// <summary>
        /// Uploads a bitmap as an RGBA texture. Rows are flipped so that texcoord v=0 is the
        /// bottom row (GL convention, same as the old renderer). With <paramref name="asMask"/>
        /// the RGB channels are forced to white, keeping only the alpha channel, so the shader
        /// can tint the sprite with the current color (glBitmap replacement for icons).
        /// </summary>
        private uint UploadTexture(Bitmap bmp, bool asMask)
        {
            int w = bmp.Width, h = bmp.Height;
            using var tmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(tmp))
            {
                g.DrawImage(bmp, new Rectangle(0, 0, w, h));
            }
            tmp.RotateFlip(RotateFlipType.RotateNoneFlipY);
            var data = tmp.LockBits(new Rectangle(0, 0, w, h),
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            byte[] pixels = new byte[w * h * 4];
            try
            {
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            }
            finally
            {
                tmp.UnlockBits(data);
            }
            if (asMask)
            {
                for (int i = 0; i < pixels.Length; i += 4)
                {
                    pixels[i] = pixels[i + 1] = pixels[i + 2] = 255; // BGR → white, alpha stays
                }
            }
            uint tex = gl.GenTexture();
            gl.ActiveTexture(TextureUnit.Texture0);
            gl.BindTexture(TextureTarget.Texture2D, tex);
            gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
            gl.TexImage2D<byte>(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)w, (uint)h, 0,
                PixelFormat.Bgra, PixelType.UnsignedByte, (ReadOnlySpan<byte>)pixels.AsSpan());
            gl.BindTexture(TextureTarget.Texture2D, 0);
            return tex;
        }

        // 6 vertices (2 triangles), 5 floats each (xyz + uv), same corner order as the old
        // GL_QUADS: p0=location(0,0), p3=+dirHeight(0,1), p2=+both(1,1), p1=+dirWidth(1,0)
        private static float[] BuildQuadVerts(GeoPoint location, GeoVector dirWidth, GeoVector dirHeight)
        {
            GeoPoint p0 = location;
            GeoPoint p1 = location + dirWidth;
            GeoPoint p2 = location + dirWidth + dirHeight;
            GeoPoint p3 = location + dirHeight;
            return new float[]
            {
                (float)p0.x, (float)p0.y, (float)p0.z, 0f, 0f,
                (float)p3.x, (float)p3.y, (float)p3.z, 0f, 1f,
                (float)p2.x, (float)p2.y, (float)p2.z, 1f, 1f,
                (float)p0.x, (float)p0.y, (float)p0.z, 0f, 0f,
                (float)p2.x, (float)p2.y, (float)p2.z, 1f, 1f,
                (float)p1.x, (float)p1.y, (float)p1.z, 1f, 0f,
            };
        }

        private unsafe void DrawTexturedVerts(uint tex, float[] verts, Color color, float[] proj, float[] mv)
        {
            if (tex == 0) return;
            gl.Enable(EnableCap.Blend); // the alpha channel must always be respected for bitmaps
            gl.BindVertexArray(streamVao);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, streamVbo);
            gl.BufferData<float>(BufferTargetARB.ArrayBuffer,
                (ReadOnlySpan<float>)verts.AsSpan(), BufferUsageARB.StreamDraw);
            gl.VertexAttribPointer(0, 3, GLEnum.Float, false, (uint)(5 * sizeof(float)), (void*)0);
            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(1, 2, GLEnum.Float, false, (uint)(5 * sizeof(float)), (void*)(3 * sizeof(float)));
            gl.EnableVertexAttribArray(1);
            gl.UseProgram(textureProgram);
            gl.UniformMatrix4(texLoc_projection, 1, false, ref proj[0]);
            gl.UniformMatrix4(texLoc_modelview,  1, false, ref mv[0]);
            gl.Uniform4(texLoc_color, color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
            gl.ActiveTexture(TextureUnit.Texture0);
            gl.BindTexture(TextureTarget.Texture2D, tex);
            gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)(verts.Length / 5));
            gl.BindTexture(TextureTarget.Texture2D, 0);
            gl.BindVertexArray(0);
            if (!blending) gl.Disable(EnableCap.Blend);
        }

        /// <summary>
        /// Draws a screen-aligned sprite: the world position is projected with the current
        /// matrices, the quad is built in normalized device coordinates with a fixed pixel
        /// size (replacement for the glRasterPos/glBitmap/glDrawPixels raster operations).
        /// </summary>
        private void DrawSpriteQuad(uint tex, double x, double y, double z, int w, int h,
            float ax, float ay, Color color)
        {
            if (tex == 0) return;
            float[] m = Mul4(projectionMatrix, modelviewMatrix);
            float cx = (float)(m[0] * x + m[4] * y + m[8]  * z + m[12]);
            float cy = (float)(m[1] * x + m[5] * y + m[9]  * z + m[13]);
            float cz = (float)(m[2] * x + m[6] * y + m[10] * z + m[14]);
            float cw = (float)(m[3] * x + m[7] * y + m[11] * z + m[15]);
            if (cw <= 0f) return; // behind the camera
            float ndcX = cx / cw, ndcY = cy / cw, ndcZ = cz / cw;
            float sx = 2f / viewWidth, sy = 2f / viewHeight;
            float left   = ndcX - ax * sx;
            float bottom = ndcY - ay * sy;
            float right  = left + w * sx;
            float top    = bottom + h * sy;
            float[] verts = new float[]
            {
                left,  bottom, ndcZ, 0f, 0f,
                right, bottom, ndcZ, 1f, 0f,
                right, top,    ndcZ, 1f, 1f,
                left,  bottom, ndcZ, 0f, 0f,
                right, top,    ndcZ, 1f, 1f,
                left,  top,    ndcZ, 0f, 1f,
            };
            float[] iden = Identity4();
            DrawTexturedVerts(tex, verts, color, iden, iden);
        }

        // -------------------------------------------------------------------------
        // IPaintTo3D properties
        // -------------------------------------------------------------------------

        // flatTextMode: text glyphs are tessellated via Face.PaintTo3D, which skips emission
        // when PaintSurfaces is false. Glyph display lists can be created while recording a
        // curve-phase list (e.g. dimension text) — without this they would be cached empty.
        public bool PaintSurfaces => paintSurfaces || flatTextMode;
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
            if (gl != null) PaintToSilkGLList.FreeDeleted(gl); // reclaim buffers of collected lists
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
                AddEdgeBatch(currentList.EdgeBatches,     currentList.EdgeVertexCount,    color, currentPatternPx);
            }
        }

        public void AvoidColor(Color color) { avoidColor = color; }

        public void SetLineWidth(LineWidth lineWidth)
        {
            gl?.LineWidth(lineWidth == null ? 1f : Math.Max(1f, (float)lineWidth.Width));
        }

        public void SetLinePattern(LinePattern pattern)
        {
            double[] p = pattern?.Pattern;
            float[] px = null;
            if (p != null && p.Length > 0 && p.Length <= 8)
            {
                double sum = 0.0;
                for (int i = 0; i < p.Length; ++i) sum += p[i];
                if (sum > 0.0)
                {
                    // like the old glLineStipple path: only the proportions are used and the
                    // pattern repeats over 16 screen pixels; every segment is at least 1 px
                    px = new float[p.Length];
                    for (int i = 0; i < p.Length; ++i)
                    {
                        px[i] = Math.Max(1f, (float)(p[i] * 16.0 / sum));
                    }
                }
            }
            currentPatternPx = px;
            if (inList)
            {
                AddEdgeBatch(currentList.EdgeBatches, currentList.EdgeVertexCount, currentColor, px);
            }
        }

        public void Polyline(GeoPoint[] points)
        {
            if (points == null || points.Length < 2) return;

            if (inList)
            {
                // Polylines in a list are stored as line segments; the cumulative distance
                // continues over the whole polyline so dash patterns don't restart per segment
                double dist = 0.0;
                for (int i = 0; i + 1 < points.Length; i++)
                {
                    double segLen = SegmentLength(points[i], points[i + 1]);
                    listEdgeBuf.Add((float)points[i].x);
                    listEdgeBuf.Add((float)points[i].y);
                    listEdgeBuf.Add((float)points[i].z);
                    listEdgeBuf.Add((float)dist);
                    listEdgeBuf.Add((float)points[i + 1].x);
                    listEdgeBuf.Add((float)points[i + 1].y);
                    listEdgeBuf.Add((float)points[i + 1].z);
                    listEdgeBuf.Add((float)(dist + segLen));
                    dist += segLen;
                    currentList.EdgeVertexCount += 2;
                }
                return;
            }

            var v = new float[(points.Length - 1) * 8];
            double d = 0.0;
            for (int i = 0; i + 1 < points.Length; i++)
            {
                double segLen = SegmentLength(points[i], points[i + 1]);
                int o = i * 8;
                v[o+0] = (float)points[i].x;     v[o+1] = (float)points[i].y;     v[o+2] = (float)points[i].z;     v[o+3] = (float)d;
                v[o+4] = (float)points[i+1].x;   v[o+5] = (float)points[i+1].y;   v[o+6] = (float)points[i+1].z;   v[o+7] = (float)(d + segLen);
                d += segLen;
            }
            DrawEdge(v, (points.Length - 1) * 2, PrimitiveType.Lines, null, null, true);
        }

        private static double SegmentLength(GeoPoint a, GeoPoint b)
        {
            double dx = b.x - a.x, dy = b.y - a.y, dz = b.z - a.z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public void FilledPolyline(GeoPoint[] points) { /* deprecated */ }

        public void Points(GeoPoint[] points, float size, PointSymbol pointSymbol)
        {
            // point symbols are drawn as screen-aligned icon sprites in the current color,
            // like the old renderer (which routed everything through DisplayIcon/glBitmap)
            if (points == null || points.Length == 0) return;
            List<Bitmap> bl = BitmapList;
            Bitmap bmp;
            if ((pointSymbol & PointSymbol.Select) != 0)
            {
                bmp = bl[12];
                for (int i = 0; i < points.Length; ++i) DisplayIcon(points[i], bmp);
                return; // only show the full square, nothing else
            }
            int offset = useLineWidth ? 6 : 0; // thin or (optionally) bold symbol variants
            bmp = null;
            switch ((PointSymbol)((int)pointSymbol & 0x07))
            {
                case PointSymbol.Empty: bmp = null; break;
                case PointSymbol.Dot:   bmp = bl[0 + offset]; break;
                case PointSymbol.Plus:  bmp = bl[1 + offset]; break;
                case PointSymbol.Cross: bmp = bl[2 + offset]; break;
                case PointSymbol.Line:  bmp = bl[3 + offset]; break;
            }
            if (bmp != null)
            {
                for (int i = 0; i < points.Length; ++i) DisplayIcon(points[i], bmp);
            }
            bmp = null;
            if ((pointSymbol & PointSymbol.Circle) != 0) bmp = bl[5 + offset];
            if ((pointSymbol & PointSymbol.Square) != 0) bmp = bl[4 + offset];
            if (bmp != null)
            {
                for (int i = 0; i < points.Length; ++i) DisplayIcon(points[i], bmp);
            }
        }

        public void Triangle(GeoPoint[] vertex, GeoVector[] normals, int[] indextriples)
        {
            if (vertex == null || indextriples == null || indextriples.Length == 0) return;
            int vc = indextriples.Length;

            if (inList)
            {
                // one growth step instead of repeated doubling: big faces add millions of floats
                int needed = listSurfBuf.Count + vc * 6;
                if (listSurfBuf.Capacity < needed) listSurfBuf.Capacity = needed;
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

        public void PreparePointSymbol(PointSymbol pointSymbol)
        {
            List<Bitmap> bl = BitmapList;
            int offset = useLineWidth ? 6 : 0;
            Bitmap bmp = null;
            switch ((PointSymbol)((int)pointSymbol & 0x07))
            {
                case PointSymbol.Empty: bmp = null; break;
                case PointSymbol.Dot:   bmp = bl[0 + offset]; break;
                case PointSymbol.Plus:  bmp = bl[1 + offset]; break;
                case PointSymbol.Cross: bmp = bl[2 + offset]; break;
                case PointSymbol.Line:  bmp = bl[3 + offset]; break;
            }
            if (bmp != null) PrepareIcon(bmp);
            bmp = null;
            if ((pointSymbol & PointSymbol.Circle) != 0) bmp = bl[5 + offset];
            if ((pointSymbol & PointSymbol.Square) != 0) bmp = bl[4 + offset];
            if ((pointSymbol & PointSymbol.Select) != 0) bmp = bl[12];
            if (bmp != null) PrepareIcon(bmp);
        }

        public void PrepareIcon(Bitmap icon)
        {
            if (gl == null || icon == null) return;
            if (!iconMasks.ContainsKey(icon)) iconMasks[icon] = UploadTexture(icon, true);
        }

        public void PrepareBitmap(Bitmap bitmap, int xoffset, int yoffset)
        {
            if (gl == null || bitmap == null) return;
            if (!sprites.ContainsKey(bitmap)) sprites[bitmap] = (UploadTexture(bitmap, false), xoffset, yoffset);
        }

        public void PrepareBitmap(Bitmap bitmap)
        {
            if (gl == null || bitmap == null) return;
            if (!textures.ContainsKey(bitmap)) textures[bitmap] = UploadTexture(bitmap, false);
        }

        public void RectangularBitmap(Bitmap bitmap, GeoPoint location,
            GeoVector directionWidth, GeoVector directionHeight)
        {
            if (gl == null || bitmap == null) return;
            PrepareBitmap(bitmap); // no-op when already uploaded
            uint tex = textures[bitmap];
            float[] verts = BuildQuadVerts(location, directionWidth, directionHeight);
            if (inList)
            {
                (currentList.TexQuads ??= new()).Add(new PaintToSilkGLList.TexQuad { Texture = tex, Verts = verts });
                return;
            }
            // the image replaces the current color entirely (old GL_TEXTURE_ENV GL_REPLACE)
            DrawTexturedVerts(tex, verts, Color.White, projectionMatrix, modelviewMatrix);
        }

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
                UploadEdgePattern(null);
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
                    UploadEdgePattern(batches[i].pattern);
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

            // The whole list content replays unconditionally, like the old glCallList did:
            // the PaintFaces mode controls what core code emits while recording (and the
            // polygon offset), not what an already recorded list draws. Curve-phase lists
            // legitimately contain surface geometry — dimension text and filled arrow heads,
            // point symbol sprites — which must not be dropped at replay time.
            DrawListSurface(list);
            DrawListEdge(list);
            if (list.TexQuads != null)
                foreach (var q in list.TexQuads)
                    DrawTexturedVerts(q.Texture, q.Verts, Color.White, projectionMatrix, modelviewMatrix);
            if (list.Sprites != null)
                foreach (var s in list.Sprites)
                    DrawSpriteQuad(s.Texture, s.X, s.Y, s.Z, s.W, s.H, s.Ax, s.Ay,
                        s.Mask ? (selectMode ? selectColor : currentColor) : Color.White);

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
            if (paintThisList == null) return;
            if (wobbleRadius <= 0)
            {
                // Feedback highlight (e.g. the trim candidate, ActionFeedBack passes -1):
                // the old renderer drew the whole list once in the select color, translated
                // 2*precision toward the viewer so it wins the depth test over the object.
                bool fbSelect   = selectMode;
                bool fbSurfaces = paintSurfaces;
                bool fbEdges    = paintEdges;
                selectMode    = true;
                paintSurfaces = true;
                paintEdges    = true;
                float[] savedMV = modelviewMatrix;
                modelviewMatrix = (float[])savedMV.Clone();
                modelviewMatrix[12] += (float)(2.0 * precision) * viewDirX;
                modelviewMatrix[13] += (float)(2.0 * precision) * viewDirY;
                modelviewMatrix[14] += (float)(2.0 * precision) * viewDirZ;
                List(paintThisList);
                modelviewMatrix = savedMV;
                selectMode    = fbSelect;
                paintSurfaces = fbSurfaces;
                paintEdges    = fbEdges;
                return;
            }

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
            var v = new float[] { sx, sy, 0, 0, ex, ey, 0, 0 };
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
                p1.X, p1.Y, 0, 0,  p2.X, p1.Y, 0, 0,  p2.X, p2.Y, 0, 0,
                p1.X, p1.Y, 0, 0,  p2.X, p2.Y, 0, 0,  p1.X, p2.Y, 0, 0,
            };
            DrawEdge(v, 6, PrimitiveType.Triangles, ortho, iden);
        }

        public void Point2D(int x, int y) { /* deprecated */ }

        public void DisplayIcon(GeoPoint p, Bitmap icon)
        {
            if (gl == null || icon == null) return;
            PrepareIcon(icon);
            uint tex = iconMasks[icon];
            // centered on p, drawn in the current color (old glBitmap semantics)
            if (inList)
            {
                (currentList.Sprites ??= new()).Add(new PaintToSilkGLList.Sprite
                {
                    Texture = tex, X = (float)p.x, Y = (float)p.y, Z = (float)p.z,
                    W = icon.Width, H = icon.Height,
                    Ax = icon.Width / 2f, Ay = icon.Height / 2f, Mask = true,
                });
                return;
            }
            DrawSpriteQuad(tex, p.x, p.y, p.z, icon.Width, icon.Height,
                icon.Width / 2f, icon.Height / 2f, selectMode ? selectColor : currentColor);
        }

        public void DisplayBitmap(GeoPoint p, Bitmap bitmap)
        {
            if (gl == null || bitmap == null) return;
            if (!sprites.ContainsKey(bitmap)) PrepareBitmap(bitmap, 0, 0);
            (uint tex, int xoff, int yoff) = sprites[bitmap];
            if (inList)
            {
                (currentList.Sprites ??= new()).Add(new PaintToSilkGLList.Sprite
                {
                    Texture = tex, X = (float)p.x, Y = (float)p.y, Z = (float)p.z,
                    W = bitmap.Width, H = bitmap.Height,
                    Ax = xoff, Ay = yoff, Mask = false,
                });
                return;
            }
            DrawSpriteQuad(tex, p.x, p.y, p.z, bitmap.Width, bitmap.Height, xoff, yoff, Color.White);
        }

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

            // view direction toward the viewer (projection.Direction points into the scene)
            GeoVector d = projection.Direction;
            double dlen = Math.Sqrt(d.x * d.x + d.y * d.y + d.z * d.z);
            if (dlen > 0.0)
            {
                viewDirX = (float)(-d.x / dlen);
                viewDirY = (float)(-d.y / dlen);
                viewDirZ = (float)(-d.z / dlen);
            }

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
            // creating new lists is a good moment to delete the GPU buffers of collected ones
            // (same place as the old OpenGlList.FreeLists call)
            if (gl != null && !inList) PaintToSilkGLList.FreeDeleted(gl);
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

        public void FreeUnusedLists()
        {
            if (gl != null) PaintToSilkGLList.FreeDeleted(gl);
        }

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
            if (gl != null && hglrc != IntPtr.Zero)
            {
                try
                {
                    WglContext.MakeCurrent(hdc, hglrc); // GL deletes need the context current
                    if (surfaceProgram != 0) gl.DeleteProgram(surfaceProgram);
                    if (edgeProgram    != 0) gl.DeleteProgram(edgeProgram);
                    if (textureProgram != 0) gl.DeleteProgram(textureProgram);
                    foreach (uint tex in textures.Values) gl.DeleteTexture(tex);
                    foreach ((uint tex, _, _) in sprites.Values) gl.DeleteTexture(tex);
                    foreach (uint tex in iconMasks.Values) gl.DeleteTexture(tex);
                    if (streamVao != 0) gl.DeleteVertexArray(streamVao);
                    if (streamVbo != 0) gl.DeleteBuffer(streamVbo);
                    if (fbo        != 0) { gl.DeleteFramebuffer(fbo);         fbo = 0; }
                    if (fboColorRb != 0) { gl.DeleteRenderbuffer(fboColorRb); fboColorRb = 0; }
                    if (fboDepthRb != 0) { gl.DeleteRenderbuffer(fboDepthRb); fboDepthRb = 0; }
                }
                catch (Exception) { } // best effort — the window may already be destroyed
                textures.Clear(); sprites.Clear(); iconMasks.Clear();
                // gl is the process wide shared GL api object: it stays alive for other views
                gl = null;
            }
            if (hdc != IntPtr.Zero)
            {
                // the rendering context is shared with other views and lives until the process
                // ends; only the device context belongs to this window
                WglContext.ReleaseDeviceContext(hdc, hwnd);
                hdc   = IntPtr.Zero;
                hglrc = IntPtr.Zero;
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
