using System;
using System.Collections.Generic;

namespace CADability
{
	/// <summary>
	/// Modern VBO-based rendering system that replaces OpenGL display lists
	/// This provides the same interface but uses Vertex Buffer Objects for efficiency
	/// </summary>
	/// <remarks>
	/// OpenGL 2.1 supports both display lists and VBOs. VBOs are:
	/// - More efficient on modern GPUs
	/// - Faster to create and modify
	/// - Better memory locality
	/// - Can be used with shaders
	/// 
	/// This system maintains compatibility with the existing IPaintTo3DList interface
	/// while using VBOs internally.
	/// </remarks>
	[CLSCompliant(false)]
	public class VBODisplayListManager : IDisposable
    {
        private Dictionary<string, GLVertexBuffer> displayLists = new Dictionary<string, GLVertexBuffer>();
        private List<GLVertex> currentVertices = new List<GLVertex>();
        private List<uint> currentIndices = new List<uint>();
        private bool disposed = false;
        private string currentListName = null;

        /// <summary>
        /// Gets the number of active display lists
        /// </summary>
        public int DisplayListCount => displayLists.Count;

        /// <summary>
        /// Opens a new display list for recording vertices
        /// </summary>
        public void BeginDisplayList(string name)
        {
            if (currentListName != null)
            {
                throw new InvalidOperationException($"Display list '{currentListName}' is already open");
            }

            currentListName = name;
            currentVertices.Clear();
            currentIndices.Clear();

            OpenGLErrorHandler.LogDebug($"Display list '{name}' opened");
        }

        /// <summary>
        /// Closes the current display list and uploads its geometry to GPU
        /// </summary>
        /// <returns>The created vertex buffer</returns>
        public GLVertexBuffer EndDisplayList()
        {
            if (currentListName == null)
            {
                throw new InvalidOperationException("No display list is currently open");
            }

            if (currentVertices.Count == 0)
            {
                OpenGLErrorHandler.LogWarning($"Display list '{currentListName}' has no vertices");
                currentListName = null;
                return null;
            }

            GLVertexBuffer vbo = new GLVertexBuffer();
            vbo.SetVertexData(currentVertices.ToArray(), currentIndices.Count > 0 ? currentIndices.ToArray() : null);

            displayLists[currentListName] = vbo;

            OpenGLErrorHandler.LogDebug($"Display list '{currentListName}' closed: {currentVertices.Count} vertices, {currentIndices.Count} indices");

            currentListName = null;
            currentVertices.Clear();
            currentIndices.Clear();

            return vbo;
        }

        /// <summary>
        /// Adds vertices to the current display list
        /// </summary>
        public void AddPolyline(GeoPoint[] points, System.Drawing.Color color)
        {
            if (currentListName == null)
            {
                throw new InvalidOperationException("No display list is currently open");
            }

            uint baseIndex = (uint)currentVertices.Count;

            // Convert GeoPoint array to GLVertex array (line strip)
            float r = color.R / 255.0f;
            float g = color.G / 255.0f;
            float b = color.B / 255.0f;
            float a = color.A / 255.0f;

            for (int i = 0; i < points.Length; i++)
            {
                var vertex = new GLVertex(
                    (float)points[i].x,
                    (float)points[i].y,
                    (float)points[i].z,
                    0, 0, 1,
                    r, g, b, a,
                    0, 0
                );
                currentVertices.Add(vertex);

                // Add indices for line strip
                if (i > 0)
                {
                    currentIndices.Add(baseIndex + (uint)(i - 1));
                    currentIndices.Add(baseIndex + (uint)i);
                }
            }
        }

        /// <summary>
        /// Adds triangles to the current display list
        /// </summary>
        public void AddTriangles(GeoPoint[] vertices, GeoVector[] normals, int[] indexTriples, System.Drawing.Color color)
        {
            if (currentListName == null)
            {
                throw new InvalidOperationException("No display list is currently open");
            }

            uint baseIndex = (uint)currentVertices.Count;

            float r = color.R / 255.0f;
            float g = color.G / 255.0f;
            float b = color.B / 255.0f;
            float a = color.A / 255.0f;

            // Add all vertices
            for (int i = 0; i < vertices.Length; i++)
            {
                GeoVector normal = i < normals.Length ? normals[i] : GeoVector.ZAxis;
                var vertex = new GLVertex(
                    (float)vertices[i].x,
                    (float)vertices[i].y,
                    (float)vertices[i].z,
                    (float)normal.x,
                    (float)normal.y,
                    (float)normal.z,
                    r, g, b, a,
                    0, 0
                );
                currentVertices.Add(vertex);
            }

            // Add triangle indices
            for (int i = 0; i < indexTriples.Length; i++)
            {
                currentIndices.Add(baseIndex + (uint)indexTriples[i]);
            }
        }

        /// <summary>
        /// Gets a display list by name
        /// </summary>
        public GLVertexBuffer GetDisplayList(string name)
        {
            GLVertexBuffer vbo;
            if (displayLists.TryGetValue(name, out vbo))
            {
                return vbo;
            }
            return null;
        }

        /// <summary>
        /// Renders a display list
        /// </summary>
        public void RenderDisplayList(string name, uint primitiveMode = Gl.GL_TRIANGLES)
        {
            GLVertexBuffer vbo = GetDisplayList(name);
            if (vbo != null)
            {
                vbo.Draw(primitiveMode);
            }
        }

        /// <summary>
        /// Deletes a display list and frees GPU resources
        /// </summary>
        public void DeleteDisplayList(string name)
        {
            if (displayLists.TryGetValue(name, out GLVertexBuffer vbo))
            {
                vbo.Dispose();
                displayLists.Remove(name);
                OpenGLErrorHandler.LogDebug($"Display list '{name}' deleted");
            }
        }

        /// <summary>
        /// Gets total memory used by all display lists
        /// </summary>
        public long GetTotalMemoryUsage()
        {
            long total = 0;
            foreach (var vbo in displayLists.Values)
            {
                total += vbo.GetMemoryUsage();
            }
            return total;
        }

        /// <summary>
        /// Disposes all display lists
        /// </summary>
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            foreach (var vbo in displayLists.Values)
            {
                try
                {
                    vbo.Dispose();
                }
                catch (Exception ex)
                {
                    OpenGLErrorHandler.LogError($"Exception disposing display list: {ex.Message}");
                }
            }

            displayLists.Clear();
            currentVertices.Clear();
            currentIndices.Clear();

            OpenGLErrorHandler.LogDebug("VBO Display List Manager disposed");
        }

        /// <summary>
        /// Finalizer to ensure cleanup
        /// </summary>
        ~VBODisplayListManager()
        {
            try
            {
                Dispose();
            }
            catch { }
        }
    }
}
