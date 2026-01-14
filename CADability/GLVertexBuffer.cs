using System;
using System.Runtime.InteropServices;

namespace CADability
{
	/// <summary>
	/// Represents vertex buffer data for modern OpenGL rendering
	/// </summary>
	/// <remarks>
	/// This struct packs position, normal, color, and texture coordinates
	/// into a single vertex buffer format for efficient GPU memory usage.
	/// </remarks>
	[StructLayout(LayoutKind.Sequential)]
	public struct GLVertex
	{
		public float x, y, z;              // Position (12 bytes)
		public float nx, ny, nz;           // Normal (12 bytes)
		public float r, g, b, a;           // Color RGBA (16 bytes)
		public float u, v;                 // TexCoord (8 bytes)
										   // Total: 48 bytes per vertex

		public GLVertex(float px, float py, float pz,
					   float nx = 0, float ny = 1, float nz = 0,
					   float r = 1, float g = 1, float b = 1, float a = 1,
					   float u = 0, float v = 0)
		{
			this.x = px; this.y = py; this.z = pz;
			this.nx = nx; this.ny = ny; this.nz = nz;
			this.r = r; this.g = g; this.b = b; this.a = a;
			this.u = u; this.v = v;
		}

		public static int SizeInBytes => Marshal.SizeOf(typeof(GLVertex));
	}

	/// <summary>
	/// Manages Vertex Buffer Objects for OpenGL 2.1+ rendering
	/// </summary>
	/// <remarks>
	/// VBOs provide GPU-resident geometry data, enabling efficient rendering
	/// compared to immediate-mode (glVertex3f) or display lists.
	/// </remarks>
	[CLSCompliant(false)]
	public class GLVertexBuffer : IDisposable
	{
		private uint vboId = 0;
		private uint iboId = 0;  // Index Buffer Object
		private int vertexCount = 0;
		private int indexCount = 0;
		private bool disposed = false;
		private GLVertex[] vertices = null;
		private uint[] indices = null;

		/// <summary>
		/// Gets the VBO ID
		/// </summary>
		public uint VboId => vboId;

		/// <summary>
		/// Gets the IBO (index buffer) ID
		/// </summary>
		public uint IboId => iboId;

		/// <summary>
		/// Gets the number of vertices in this buffer
		/// </summary>
		public int VertexCount => vertexCount;

		/// <summary>
		/// Gets the number of indices in this buffer
		/// </summary>
		public int IndexCount => indexCount;

		/// <summary>
		/// Creates a new empty vertex buffer
		/// </summary>
		public GLVertexBuffer()
		{
			// Generate VBO
			uint[] vbos = new uint[1];
			Gl.glGenBuffers(1, vbos);
			vboId = vbos[0];

			if (vboId == 0)
			{
				throw new InvalidOperationException("Failed to create vertex buffer object");
			}

			// Generate IBO
			uint[] ibos = new uint[1];
			Gl.glGenBuffers(1, ibos);
			iboId = ibos[0];

			if (iboId == 0)
			{
				Gl.glDeleteBuffers(1, new uint[] { vboId });
				throw new InvalidOperationException("Failed to create index buffer object");
			}

			OpenGLErrorHandler.LogDebug($"VBO {vboId} and IBO {iboId} created");
		}

		/// <summary>
		/// Uploads vertex data to the GPU
		/// </summary>
		public void SetVertexData(GLVertex[] verts, uint[] inds)
		{
			if (verts == null || verts.Length == 0)
			{
				throw new ArgumentException("Vertex array cannot be null or empty", nameof(verts));
			}

			vertices = verts;
			indices = inds;
			vertexCount = verts.Length;
			indexCount = inds?.Length ?? 0;

			// Upload vertex data
			Gl.glBindBuffer(Gl.GL_ARRAY_BUFFER, vboId);

			int vertexDataSize = vertexCount * GLVertex.SizeInBytes;
			GCHandle handle = GCHandle.Alloc(verts, GCHandleType.Pinned);
			try
			{
				Gl.glBufferData(Gl.GL_ARRAY_BUFFER, new IntPtr(vertexDataSize), handle.AddrOfPinnedObject(), Gl.GL_STATIC_DRAW);
			}
			finally
			{
				handle.Free();
			}

			// Upload index data if provided
			if (inds != null && inds.Length > 0)
			{
				Gl.glBindBuffer(Gl.GL_ELEMENT_ARRAY_BUFFER, iboId);

				int indexDataSize = indexCount * sizeof(uint);
				GCHandle indexHandle = GCHandle.Alloc(inds, GCHandleType.Pinned);
				try
				{
					Gl.glBufferData(Gl.GL_ELEMENT_ARRAY_BUFFER, new IntPtr(indexDataSize), indexHandle.AddrOfPinnedObject(), Gl.GL_STATIC_DRAW);
				}
				finally
				{
					indexHandle.Free();
				}
			}

			Gl.glBindBuffer(Gl.GL_ARRAY_BUFFER, 0);
			Gl.glBindBuffer(Gl.GL_ELEMENT_ARRAY_BUFFER, 0);

			OpenGLErrorHandler.CheckError("SetVertexData");
			OpenGLErrorHandler.LogDebug($"VBO {vboId}: {vertexCount} vertices, {indexCount} indices uploaded");
		}

		/// <summary>
		/// Binds this buffer for rendering
		/// </summary>
		public void Bind()
		{
			Gl.glBindBuffer(Gl.GL_ARRAY_BUFFER, vboId);
			if (indexCount > 0)
			{
				Gl.glBindBuffer(Gl.GL_ELEMENT_ARRAY_BUFFER, iboId);
			}
			OpenGLErrorHandler.CheckError("VBO Bind");
		}

		/// <summary>
		/// Unbinds this buffer
		/// </summary>
		public static void Unbind()
		{
			Gl.glBindBuffer(Gl.GL_ARRAY_BUFFER, 0);
			Gl.glBindBuffer(Gl.GL_ELEMENT_ARRAY_BUFFER, 0);
		}

		/// <summary>
		/// Renders this buffer using indexed or non-indexed drawing
		/// </summary>
		/// <param name="mode">OpenGL primitive mode (e.g., GL_TRIANGLES)</param>
		public void Draw(uint mode = Gl.GL_TRIANGLES)
		{
			if (vertexCount == 0)
			{
				OpenGLErrorHandler.LogWarning($"VBO {vboId}: Attempting to draw with 0 vertices");
				return;
			}

			Bind();

			// Setup vertex attributes
			// Position
			Gl.glEnableClientState(Gl.GL_VERTEX_ARRAY);
			Gl.glVertexPointer(3, Gl.GL_FLOAT, GLVertex.SizeInBytes, IntPtr.Zero);

			// Normal
			Gl.glEnableClientState(Gl.GL_NORMAL_ARRAY);
			Gl.glNormalPointer(Gl.GL_FLOAT, GLVertex.SizeInBytes, new IntPtr(12));

			// Color
			Gl.glEnableClientState(Gl.GL_COLOR_ARRAY);
			Gl.glColorPointer(4, Gl.GL_FLOAT, GLVertex.SizeInBytes, new IntPtr(24));

			// TexCoord
			Gl.glEnableClientState(Gl.GL_TEXTURE_COORD_ARRAY);
			Gl.glTexCoordPointer(2, Gl.GL_FLOAT, GLVertex.SizeInBytes, new IntPtr(40));

			// Draw
			if (indexCount > 0)
			{
				Gl.glDrawElements(mode, indexCount, Gl.GL_UNSIGNED_INT, IntPtr.Zero);
			}
			else
			{
				Gl.glDrawArrays(mode, 0, vertexCount);
			}

			// Disable vertex attributes
			Gl.glDisableClientState(Gl.GL_VERTEX_ARRAY);
			Gl.glDisableClientState(Gl.GL_NORMAL_ARRAY);
			Gl.glDisableClientState(Gl.GL_COLOR_ARRAY);
			Gl.glDisableClientState(Gl.GL_TEXTURE_COORD_ARRAY);

			Unbind();
			OpenGLErrorHandler.CheckError("VBO Draw");
		}

		/// <summary>
		/// Gets memory usage in bytes
		/// </summary>
		public long GetMemoryUsage()
		{
			long usage = 0;
			if (vertexCount > 0) usage += (long)vertexCount * GLVertex.SizeInBytes;
			if (indexCount > 0) usage += (long)indexCount * sizeof(uint);
			return usage;
		}

		/// <summary>
		/// Disposes this buffer and frees GPU resources
		/// </summary>
		public void Dispose()
		{
			if (disposed) return;
			disposed = true;

			if (vboId != 0)
			{
				try
				{
					Gl.glDeleteBuffers(1, new uint[] { vboId });
				}
				catch (Exception ex)
				{
					OpenGLErrorHandler.LogError($"Exception deleting VBO {vboId}: {ex.Message}");
				}
				vboId = 0;
			}

			if (iboId != 0)
			{
				try
				{
					Gl.glDeleteBuffers(1, new uint[] { iboId });
				}
				catch (Exception ex)
				{
					OpenGLErrorHandler.LogError($"Exception deleting IBO {iboId}: {ex.Message}");
				}
				iboId = 0;
			}

			vertices = null;
			indices = null;
			vertexCount = 0;
			indexCount = 0;

			OpenGLErrorHandler.LogDebug($"Vertex buffer disposed");
		}

		/// <summary>
		/// Finalizer to ensure cleanup
		/// </summary>
		~GLVertexBuffer()
		{
			try
			{
				Dispose();
			}
			catch { }
		}
	}
}
