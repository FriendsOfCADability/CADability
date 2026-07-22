using CADability;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;

namespace CADability.Forms
{
    internal class PaintToSilkGLList : IPaintTo3DList
    {
        // surface geometry
        internal float[] SurfaceVertices;   // vec3 position + vec3 normal interleaved (6 floats per vertex)
        internal int SurfaceVertexCount;
        internal uint SurfaceVao;
        internal uint SurfaceVbo;

        // edge geometry
        internal float[] EdgeVertices;      // vec3 position + cumulative polyline distance (4 floats per vertex)
        internal int EdgeVertexCount;
        internal uint EdgeVao;
        internal uint EdgeVbo;

        // 2D overlay lines (screen-space)
        internal List<(int sx, int sy, int ex, int ey)> Lines2D = new();

        // sub-lists to replay (from MakeList / containedSubLists)
        internal List<IPaintTo3DList> SubLists;

        // sub-list calls recorded during OpenList/CloseList (e.g. text characters)
        internal struct SubListCall
        {
            internal PaintToSilkGLList List;
            internal float[] ModelView;
            internal Color Color;
        }
        internal List<SubListCall> SubListCalls;

        // textured quads in world/model coordinates (RectangularBitmap, i.e. Picture objects)
        internal struct TexQuad
        {
            internal uint Texture;
            internal float[] Verts; // 6 vertices, 5 floats each: position xyz + texcoord uv
        }
        internal List<TexQuad> TexQuads;

        // screen-aligned sprites (DisplayIcon/DisplayBitmap); projected at replay time
        internal struct Sprite
        {
            internal uint Texture;
            internal float X, Y, Z;   // world position
            internal int W, H;        // size in pixels
            internal float Ax, Ay;    // anchor in pixels from the lower left corner
            internal bool Mask;       // true: white mask drawn in the current color (icons)
        }
        internal List<Sprite> Sprites;

        // per-entity color batches: each entry means "from startVertex, use this color"
        internal List<(int startVertex, Color color)> SurfaceBatches = new();
        // edge batches additionally carry the dash pattern (pixel segment lengths, null = solid)
        internal List<(int startVertex, Color color, float[] pattern)> EdgeBatches = new();

        internal bool IsGpuUploaded;
        internal GL Gl;
        private bool disposed;
        private long gpuBytes; // registered with GC.AddMemoryPressure while the buffers live

        // GPU resources of garbage collected lists. GL calls are not possible on the finalizer
        // thread, so the ids are queued here and deleted by FreeDeleted on the render thread
        // (same pattern as the old OpenGlList.FreeLists).
        private static readonly List<uint> vaosToDelete = new();
        private static readonly List<uint> vbosToDelete = new();

        ~PaintToSilkGLList()
        {
            if (IsGpuUploaded && !disposed)
            {
                lock (vaosToDelete)
                {
                    if (SurfaceVao != 0) vaosToDelete.Add(SurfaceVao);
                    if (EdgeVao    != 0) vaosToDelete.Add(EdgeVao);
                    if (SurfaceVbo != 0) vbosToDelete.Add(SurfaceVbo);
                    if (EdgeVbo    != 0) vbosToDelete.Add(EdgeVbo);
                }
                if (gpuBytes > 0) GC.RemoveMemoryPressure(gpuBytes);
            }
        }

        internal static void FreeDeleted(GL gl)
        {
            lock (vaosToDelete)
            {
                for (int i = 0; i < vaosToDelete.Count; i++) gl.DeleteVertexArray(vaosToDelete[i]);
                for (int i = 0; i < vbosToDelete.Count; i++) gl.DeleteBuffer(vbosToDelete[i]);
                vaosToDelete.Clear();
                vbosToDelete.Clear();
            }
        }

        public string Name { get; set; }

        public List<IPaintTo3DList> containedSubLists
        {
            set => SubLists = value;
        }

        internal unsafe void UploadToGpu(GL gl)
        {
            Gl = gl;

            if (SurfaceVertices != null && SurfaceVertexCount > 0)
            {
                SurfaceVao = gl.GenVertexArray();
                SurfaceVbo = gl.GenBuffer();
                gl.BindVertexArray(SurfaceVao);
                gl.BindBuffer(BufferTargetARB.ArrayBuffer, SurfaceVbo);
                gl.BufferData<float>(BufferTargetARB.ArrayBuffer, (ReadOnlySpan<float>)SurfaceVertices.AsSpan(0, SurfaceVertexCount * 6), BufferUsageARB.StaticDraw);
                gl.VertexAttribPointer(0, 3, GLEnum.Float, false, (uint)(6 * sizeof(float)), (void*)0);
                gl.EnableVertexAttribArray(0);
                gl.VertexAttribPointer(1, 3, GLEnum.Float, false, (uint)(6 * sizeof(float)), (void*)(3 * sizeof(float)));
                gl.EnableVertexAttribArray(1);
                gl.BindVertexArray(0);
            }

            if (EdgeVertices != null && EdgeVertexCount > 0)
            {
                EdgeVao = gl.GenVertexArray();
                EdgeVbo = gl.GenBuffer();
                gl.BindVertexArray(EdgeVao);
                gl.BindBuffer(BufferTargetARB.ArrayBuffer, EdgeVbo);
                gl.BufferData<float>(BufferTargetARB.ArrayBuffer, (ReadOnlySpan<float>)EdgeVertices.AsSpan(0, EdgeVertexCount * 4), BufferUsageARB.StaticDraw);
                gl.VertexAttribPointer(0, 3, GLEnum.Float, false, (uint)(4 * sizeof(float)), (void*)0);
                gl.EnableVertexAttribArray(0);
                gl.VertexAttribPointer(1, 1, GLEnum.Float, false, (uint)(4 * sizeof(float)), (void*)(3 * sizeof(float)));
                gl.EnableVertexAttribArray(1);
                gl.BindVertexArray(0);
            }

            // The vertex data now lives on the GPU; keeping the arrays alive as well would
            // roughly double the memory footprint of every display list. The GPU size is
            // registered as memory pressure: display lists are re-created on every zoom step
            // (unscaled objects, precision changes) and their buffers are only reclaimed via
            // the finalizer — without pressure the GC has no reason to ever run, and the
            // driver memory grows unbounded (observed with a zoomed sphere).
            gpuBytes = 0;
            if (SurfaceVertexCount > 0) gpuBytes += (long)SurfaceVertexCount * 6 * sizeof(float);
            if (EdgeVertexCount    > 0) gpuBytes += (long)EdgeVertexCount    * 4 * sizeof(float);
            if (gpuBytes > 0) GC.AddMemoryPressure(gpuBytes);
            SurfaceVertices = null;
            EdgeVertices    = null;

            IsGpuUploaded = true;
        }

        public void Dispose()
        {
            disposed = true;
            GC.SuppressFinalize(this);
            if (IsGpuUploaded && Gl != null)
            {
                if (SurfaceVao != 0) { Gl.DeleteVertexArray(SurfaceVao); SurfaceVao = 0; }
                if (SurfaceVbo != 0) { Gl.DeleteBuffer(SurfaceVbo); SurfaceVbo = 0; }
                if (EdgeVao != 0) { Gl.DeleteVertexArray(EdgeVao); EdgeVao = 0; }
                if (EdgeVbo != 0) { Gl.DeleteBuffer(EdgeVbo); EdgeVbo = 0; }
                IsGpuUploaded = false;
                if (gpuBytes > 0) { GC.RemoveMemoryPressure(gpuBytes); gpuBytes = 0; }
            }
        }
    }
}
