using System;
using System.Collections.Generic;

namespace CADability
{
	/// <summary>
	/// OpenGL 2.1 modernized display list implementation using VBOs
	/// </summary>
	/// <remarks>
	/// This replaces the legacy glNewList/glEndList with VBO-based rendering
	/// while maintaining backwards compatibility through IPaintTo3DList interface.
	/// </remarks>
	[CLSCompliant(false)]
	public class ModernOpenGlList : IPaintTo3DList
	{
		private GLVertexBuffer vbo = null;
		private List<GLVertex> vertices = new List<GLVertex>();
		private List<uint> indices = new List<uint>();
		private bool hasContents = false;
		private bool isClosed = false;
		private string name;
		private List<IPaintTo3DList> sublists;
		private static Dictionary<int, string> allLists = new Dictionary<int, string>();
		private static readonly object listLock = new object();

		/// <summary>
		/// Gets the VBO ID for this list
		/// </summary>
		public GLVertexBuffer VBO => vbo;

		/// <summary>
		/// Gets whether this list has any geometry
		/// </summary>
		public bool HasContents => hasContents;

		/// <summary>
		/// Gets whether this list is finalized (closed)
		/// </summary>
		public bool IsClosed => isClosed;

		public ModernOpenGlList(string listName = null)
		{
			name = listName ?? $"ModernList_{Guid.NewGuid().ToString().Substring(0, 8)}";
			lock (listLock)
			{
				allLists[GetHashCode()] = name;
			}
			OpenGLErrorHandler.LogDebug($"ModernOpenGlList '{name}' created");
		}

		/// <summary>
		/// Marks that this list has geometry
		/// </summary>
		public void SetHasContents()
		{
			hasContents = true;
		}

		/// <summary>
		/// Begins recording vertices for this list
		/// </summary>
		public void OpenList()
		{
			if (isClosed)
			{
				throw new InvalidOperationException($"List '{name}' is already closed and cannot be reopened");
			}
			vertices.Clear();
			indices.Clear();
			OpenGLErrorHandler.LogDebug($"List '{name}' opened for recording");
		}

		/// <summary>
		/// Closes the list and uploads geometry to GPU
		/// </summary>
		public void CloseList()
		{
			if (isClosed)
			{
				return;
			}

			if (vertices.Count > 0)
			{
				vbo = new GLVertexBuffer();
				vbo.SetVertexData(vertices.ToArray(), indices.Count > 0 ? indices.ToArray() : null);
				hasContents = true;
				OpenGLErrorHandler.LogDebug($"List '{name}' closed with {vertices.Count} vertices, {indices.Count} indices");
			}
			else
			{
				hasContents = false;
			}

			isClosed = true;
		}

		/// <summary>
		/// Adds a polyline to this list
		/// </summary>
		public void AddPolyline(GeoPoint[] points, System.Drawing.Color color, bool indexed = false)
		{
			if (isClosed)
			{
				throw new InvalidOperationException("Cannot add geometry to a closed list");
			}

			uint baseIndex = (uint)vertices.Count;
			float r = color.R / 255.0f;
			float g = color.G / 255.0f;
			float b = color.B / 255.0f;
			float a = color.A / 255.0f;

			// Add vertices
			for (int i = 0; i < points.Length; i++)
			{
				var vertex = new GLVertex(
					(float)points[i].x,
					(float)points[i].y,
					(float)points[i].z,
					0, 0, 1,
					r, g, b, a
				);
				vertices.Add(vertex);
			}

			// Add indices for line strip
			if (indexed && points.Length > 1)
			{
				for (int i = 1; i < points.Length; i++)
				{
					indices.Add(baseIndex + (uint)(i - 1));
					indices.Add(baseIndex + (uint)i);
				}
			}

			hasContents = true;
		}

		/// <summary>
		/// Adds triangles to this list
		/// </summary>
		public void AddTriangles(GeoPoint[] verts, GeoVector[] normals, int[] indexTriples, System.Drawing.Color color)
		{
			if (isClosed)
			{
				throw new InvalidOperationException("Cannot add geometry to a closed list");
			}

			uint baseIndex = (uint)vertices.Count;
			float r = color.R / 255.0f;
			float g = color.G / 255.0f;
			float b = color.B / 255.0f;
			float a = color.A / 255.0f;

			// Add all vertices
			for (int i = 0; i < verts.Length; i++)
			{
				GeoVector normal = i < normals.Length ? normals[i] : GeoVector.ZAxis;
				var vertex = new GLVertex(
					(float)verts[i].x,
					(float)verts[i].y,
					(float)verts[i].z,
					(float)normal.x,
					(float)normal.y,
					(float)normal.z,
					r, g, b, a
				);
				vertices.Add(vertex);
			}

			// Add triangle indices
			for (int i = 0; i < indexTriples.Length; i++)
			{
				indices.Add(baseIndex + (uint)indexTriples[i]);
			}

			hasContents = true;
		}

		/// <summary>
		/// Renders this list
		/// </summary>
		public void Render(uint primitiveMode = Gl.GL_TRIANGLES)
		{
			if (!isClosed)
			{
				throw new InvalidOperationException("Cannot render a list that is not closed");
			}

			if (vbo != null)
			{
				vbo.Draw(primitiveMode);
			}
		}

		#region IPaintTo3DList Implementation

		string IPaintTo3DList.Name
		{
			get => name;
			set => name = value;
		}

		List<IPaintTo3DList> IPaintTo3DList.containedSubLists
		{
			set => sublists = value;
		}

		void IPaintTo3DList.Dispose()
		{
			try
			{
				if (vbo != null)
				{
					vbo.Dispose();
					vbo = null;
				}
			}
			catch (Exception ex)
			{
				OpenGLErrorHandler.LogError($"Exception disposing ModernOpenGlList '{name}': {ex.Message}");
			}

			vertices.Clear();
			indices.Clear();
			sublists?.Clear();

			lock (listLock)
			{
				allLists.Remove(GetHashCode());
			}

			OpenGLErrorHandler.LogDebug($"ModernOpenGlList '{name}' disposed");
		}

		#endregion

		/// <summary>
		/// Gets diagnostic information about all open lists
		/// </summary>
		public static Dictionary<int, string> GetAllLists()
		{
			lock (listLock)
			{
				return new Dictionary<int, string>(allLists);
			}
		}

		/// <summary>
		/// Gets total count of open lists
		/// </summary>
		public static int GetOpenListCount()
		{
			lock (listLock)
			{
				return allLists.Count;
			}
		}

		public override string ToString()
		{
			return $"ModernOpenGlList({name}, Vertices={vertices.Count}, Indices={indices.Count}, Closed={isClosed})";
		}
	}
}
