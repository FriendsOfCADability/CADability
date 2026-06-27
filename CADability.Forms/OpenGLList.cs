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
        internal float[] EdgeVertices;      // vec3 position per vertex (3 floats)
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

        // per-entity color batches: each entry means "from startVertex, use this color"
        internal List<(int startVertex, Color color)> SurfaceBatches = new();
        internal List<(int startVertex, Color color)> EdgeBatches = new();

        internal bool IsGpuUploaded;
        internal GL Gl;

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
                gl.BufferData<float>(BufferTargetARB.ArrayBuffer, (ReadOnlySpan<float>)EdgeVertices.AsSpan(0, EdgeVertexCount * 3), BufferUsageARB.StaticDraw);
                gl.VertexAttribPointer(0, 3, GLEnum.Float, false, (uint)(3 * sizeof(float)), (void*)0);
                gl.EnableVertexAttribArray(0);
                gl.BindVertexArray(0);
            }

            IsGpuUploaded = true;
        }

        public void Dispose()
        {
            if (IsGpuUploaded && Gl != null)
            {
                if (SurfaceVao != 0) { Gl.DeleteVertexArray(SurfaceVao); SurfaceVao = 0; }
                if (SurfaceVbo != 0) { Gl.DeleteBuffer(SurfaceVbo); SurfaceVbo = 0; }
                if (EdgeVao != 0) { Gl.DeleteVertexArray(EdgeVao); EdgeVao = 0; }
                if (EdgeVbo != 0) { Gl.DeleteBuffer(EdgeVbo); EdgeVbo = 0; }
                IsGpuUploaded = false;
            }
        }
    }
}
