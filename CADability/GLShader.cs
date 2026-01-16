using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace CADability
{
	/// <summary>
	/// Manages GLSL shader compilation and linking for OpenGL 2.1+
	/// </summary>
	/// <remarks>
	/// This class handles shader program creation, compilation, and linking.
	/// Supports both vertex and fragment shaders with error reporting.
	/// Uses dynamically loaded OpenGL function pointers for runtime flexibility.
	/// 
	/// NOTE: This class MUST be disposed explicitly using Dispose() or a using statement
	/// BEFORE the OpenGL context is destroyed. GPU resources cannot be safely cleaned up
	/// during garbage collection finalizers because the OpenGL context may no longer be active.
	/// </remarks>
	[CLSCompliant(false)]
	public class GLShader : IDisposable
	{
		private uint programId = 0;
		private Dictionary<string, int> uniformLocations = new Dictionary<string, int>();
		private bool disposed = false;

		/// <summary>
		/// Gets the OpenGL program ID for this shader
		/// </summary>
		public uint ProgramId => programId;

		/// <summary>
		/// Gets whether this shader program is currently in use
		/// </summary>
		public bool IsActive
		{
			get
			{
				GLFunctionLoader.GetIntegerv(GLGetParameter.CurrentProgram, out int current);
				return current == (int)programId;
			}
		}

		/// <summary>
		/// Creates a new shader program from vertex and fragment shader sources
		/// </summary>
		/// <param name="vertexSource">GLSL vertex shader source code</param>
		/// <param name="fragmentSource">GLSL fragment shader source code</param>
		/// <exception cref="InvalidOperationException">Thrown if shader compilation or linking fails</exception>
		public GLShader(string vertexSource, string fragmentSource)
		{
			if (string.IsNullOrWhiteSpace(vertexSource))
			{
				throw new ArgumentNullException(nameof(vertexSource), "Vertex shader source cannot be null or empty");
			}

			if (string.IsNullOrWhiteSpace(fragmentSource))
			{
				throw new ArgumentNullException(nameof(fragmentSource), "Fragment shader source cannot be null or empty");
			}

			// Ensure OpenGL functions are loaded
			if (GLFunctionLoader.CreateProgram == null)
			{
				try
				{
					GLFunctionLoader.Initialize();
				}
				catch (Exception ex)
				{
					throw new InvalidOperationException("Failed to initialize OpenGL shader functions. Ensure your GPU supports GLSL and OpenGL 2.1+", ex);
				}
			}

			try
			{
				uint vertexShader = CompileShader(GLFunctionLoader.CreateShader((uint)GLShaderType.VertexShader), vertexSource, "Vertex");
				uint fragmentShader = CompileShader(GLFunctionLoader.CreateShader((uint)GLShaderType.FragmentShader), fragmentSource, "Fragment");

				programId = GLFunctionLoader.CreateProgram();
				if (programId == 0)
				{
					throw new InvalidOperationException("Failed to create OpenGL shader program. GPU may not support shaders.");
				}

				GLFunctionLoader.AttachShader(programId, vertexShader);
				GLFunctionLoader.AttachShader(programId, fragmentShader);

				// Set attribute locations before linking
				GLFunctionLoader.BindAttribLocation(programId, 0, "position");
				GLFunctionLoader.BindAttribLocation(programId, 1, "normal");
				GLFunctionLoader.BindAttribLocation(programId, 2, "color");
				GLFunctionLoader.BindAttribLocation(programId, 3, "texCoord");

				GLFunctionLoader.LinkProgram(programId);

				GLFunctionLoader.GetProgramiv(programId, GLProgramParameter.LinkStatus, out int linkStatus);
				if (linkStatus == 0)
				{
					string infoLog = GetProgramInfoLog(programId);
					throw new InvalidOperationException($"Shader program linking failed:\n{infoLog}");
				}

				// Clean up individual shaders (they're now linked into the program)
				GLFunctionLoader.DeleteShader(vertexShader);
				GLFunctionLoader.DeleteShader(fragmentShader);

				OpenGLErrorHandler.LogDebug($"Shader program {programId} created and linked successfully");
			}
			catch (Exception)
			{
				Dispose();
				throw;
			}
		}

		/// <summary>
		/// Compiles an individual shader (vertex or fragment)
		/// </summary>
		private uint CompileShader(uint shader, string source, string shaderName)
		{
			if (string.IsNullOrEmpty(source))
			{
				throw new ArgumentException($"{shaderName} shader source is null or empty", nameof(source));
			}

			if (shader == 0)
			{
				throw new InvalidOperationException($"Failed to create {shaderName} shader");
			}

			GLFunctionLoader.ShaderSource(shader, 1, new string[] { source }, null);
			GLFunctionLoader.CompileShader(shader);

			GLFunctionLoader.GetShaderiv(shader, GLShaderParameter.CompileStatus, out int compileStatus);
			if (compileStatus == 0)
			{
				string infoLog = GetShaderInfoLog(shader);
				GLFunctionLoader.DeleteShader(shader);
				throw new InvalidOperationException($"{shaderName} shader compilation failed:\n{infoLog}");
			}

			OpenGLErrorHandler.LogDebug($"{shaderName} shader {shader} compiled successfully");
			return shader;
		}

		/// <summary>
		/// Gets the info log from a shader compilation
		/// </summary>
		private string GetShaderInfoLog(uint shader)
		{
			GLFunctionLoader.GetShaderiv(shader, GLShaderParameter.InfoLogLength, out int logLength);
			if (logLength <= 1) return "";

			StringBuilder sb = new StringBuilder(logLength);
			GLFunctionLoader.GetShaderInfoLog(shader, logLength, out int charsWritten, sb);
			return sb.ToString();
		}

		/// <summary>
		/// Gets the info log from a program linking
		/// </summary>
		private string GetProgramInfoLog(uint program)
		{
			GLFunctionLoader.GetProgramiv(program, GLProgramParameter.InfoLogLength, out int logLength);
			if (logLength <= 1) return "";

			StringBuilder sb = new StringBuilder(logLength);
			GLFunctionLoader.GetProgramInfoLog(program, logLength, out int charsWritten, sb);
			return sb.ToString();
		}

		/// <summary>
		/// Uses this shader program for subsequent rendering
		/// </summary>
		public void Use()
		{
			if (programId == 0)
			{
				throw new ObjectDisposedException("GLShader");
			}

			GLFunctionLoader.UseProgram(programId);
			OpenGLErrorHandler.CheckError("glUseProgram");
		}

		/// <summary>
		/// Gets the uniform location for a shader variable
		/// </summary>
		/// <param name="uniformName">Name of the uniform variable</param>
		/// <returns>Location ID or -1 if not found</returns>
		public int GetUniformLocation(string uniformName)
		{
			if (uniformLocations.TryGetValue(uniformName, out int location))
			{
				return location;
			}

			location = GLFunctionLoader.GetUniformLocation(programId, uniformName);
			uniformLocations[uniformName] = location;

			if (location == -1)
			{
				OpenGLErrorHandler.LogWarning($"Uniform '{uniformName}' not found in shader program {programId}");
			}

			return location;
		}

		/// <summary>
		/// Sets a float uniform
		/// </summary>
		public void SetUniform(string uniformName, float value)
		{
			int location = GetUniformLocation(uniformName);
			if (location >= 0)
			{
				GLFunctionLoader.Uniform1f(location, value);
			}
		}

		/// <summary>
		/// Sets an integer uniform
		/// </summary>
		public void SetUniform(string uniformName, int value)
		{
			int location = GetUniformLocation(uniformName);
			if (location >= 0)
			{
				GLFunctionLoader.Uniform1i(location, value);
			}
		}

		/// <summary>
		/// Sets a vec2 uniform
		/// </summary>
		public void SetUniform(string uniformName, float x, float y)
		{
			int location = GetUniformLocation(uniformName);
			if (location >= 0)
			{
				GLFunctionLoader.Uniform2f(location, x, y);
			}
		}

		/// <summary>
		/// Sets a vec3 uniform
		/// </summary>
		public void SetUniform(string uniformName, float x, float y, float z)
		{
			int location = GetUniformLocation(uniformName);
			if (location >= 0)
			{
				GLFunctionLoader.Uniform3f(location, x, y, z);
			}
		}

		/// <summary>
		/// Sets a vec4 uniform
		/// </summary>
		public void SetUniform(string uniformName, float x, float y, float z, float w)
		{
			int location = GetUniformLocation(uniformName);
			if (location >= 0)
			{
				GLFunctionLoader.Uniform4f(location, x, y, z, w);
			}
		}

		/// <summary>
		/// Sets a matrix4 uniform
		/// </summary>
		public void SetUniform(string uniformName, double[] matrix4x4)
		{
			if (matrix4x4 == null || matrix4x4.Length != 16)
			{
				throw new ArgumentException("Matrix must be 4x4 (16 elements)", nameof(matrix4x4));
			}

			int location = GetUniformLocation(uniformName);
			if (location >= 0)
			{
				float[] floatMatrix = new float[16];
				for (int i = 0; i < 16; i++)
				{
					floatMatrix[i] = (float)matrix4x4[i];
				}
				GLFunctionLoader.UniformMatrix4fv(location, 1, false, floatMatrix);
			}
		}

		/// <summary>
		/// Disposes this shader program and frees GPU resources.
		/// IMPORTANT: Call this BEFORE the OpenGL context is destroyed!
		/// </summary>
		public void Dispose()
		{
			if (disposed) return;
			disposed = true;

			if (programId != 0)
			{
				try
				{
					GLFunctionLoader.DeleteProgram(programId);
					OpenGLErrorHandler.LogDebug($"Shader program {programId} deleted");
				}
				catch (Exception ex)
				{
					OpenGLErrorHandler.LogError($"Exception deleting shader program {programId}: {ex.Message}");
				}
				programId = 0;
			}

			uniformLocations.Clear();
			GC.SuppressFinalize(this);
		}
	}
}

