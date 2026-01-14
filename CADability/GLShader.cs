using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CADability
{
	/// <summary>
	/// Manages GLSL shader compilation and linking for OpenGL 2.1+
	/// </summary>
	/// <remarks>
	/// This class handles shader program creation, compilation, and linking.
	/// Supports both vertex and fragment shaders with error reporting.
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
				Gl.glGetIntegerv(Gl.GL_CURRENT_PROGRAM, out int current);
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
			try
			{
				uint vertexShader = CompileShader(Gl.GL_VERTEX_SHADER, vertexSource, "Vertex");
				uint fragmentShader = CompileShader(Gl.GL_FRAGMENT_SHADER, fragmentSource, "Fragment");

				programId = Gl.glCreateProgram();
				if (programId == 0)
				{
					throw new InvalidOperationException("Failed to create OpenGL shader program");
				}

				Gl.glAttachShader(programId, vertexShader);
				Gl.glAttachShader(programId, fragmentShader);

				// Set attribute locations before linking
				Gl.glBindAttribLocation(programId, 0, "position");
				Gl.glBindAttribLocation(programId, 1, "normal");
				Gl.glBindAttribLocation(programId, 2, "color");
				Gl.glBindAttribLocation(programId, 3, "texCoord");

				Gl.glLinkProgram(programId);

				int linkStatus;
				Gl.glGetProgramiv(programId, Gl.GL_LINK_STATUS, out linkStatus);
				if (linkStatus == 0)
				{
					string infoLog = GetProgramInfoLog(programId);
					throw new InvalidOperationException($"Shader program linking failed:\n{infoLog}");
				}

				// Clean up individual shaders (they're now linked into the program)
				Gl.glDeleteShader(vertexShader);
				Gl.glDeleteShader(fragmentShader);

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
		private uint CompileShader(int shaderType, string source, string shaderName)
		{
			if (string.IsNullOrEmpty(source))
			{
				throw new ArgumentException($"{shaderName} shader source is null or empty", nameof(source));
			}

			uint shader = Gl.glCreateShader((uint)shaderType);
			if (shader == 0)
			{
				throw new InvalidOperationException($"Failed to create {shaderName} shader");
			}

			Gl.glShaderSource(shader, 1, new string[] { source }, null);
			Gl.glCompileShader(shader);

			int compileStatus;
			Gl.glGetShaderiv(shader, Gl.GL_COMPILE_STATUS, out compileStatus);
			if (compileStatus == 0)
			{
				string infoLog = GetShaderInfoLog(shader);
				Gl.glDeleteShader(shader);
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
			int logLength;
			Gl.glGetShaderiv(shader, Gl.GL_INFO_LOG_LENGTH, out logLength);
			if (logLength <= 1) return "";

			System.Text.StringBuilder sb = new System.Text.StringBuilder(logLength);
			int charsWritten;
			Gl.glGetShaderInfoLog(shader, logLength, out charsWritten, sb);
			return sb.ToString();
		}

		/// <summary>
		/// Gets the info log from a program linking
		/// </summary>
		private string GetProgramInfoLog(uint program)
		{
			int logLength;
			Gl.glGetProgramiv(program, Gl.GL_INFO_LOG_LENGTH, out logLength);
			if (logLength <= 1) return "";

			System.Text.StringBuilder sb = new System.Text.StringBuilder(logLength);
			int charsWritten;
			Gl.glGetProgramInfoLog(program, logLength, out charsWritten, sb);
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

			Gl.glUseProgram(programId);
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

			location = Gl.glGetUniformLocation(programId, uniformName);
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
				Gl.glUniform1f(location, value);
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
				Gl.glUniform1i(location, value);
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
				Gl.glUniform2f(location, x, y);
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
				Gl.glUniform3f(location, x, y, z);
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
				Gl.glUniform4f(location, x, y, z, w);
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
				Gl.glUniformMatrix4fv(location, 1, false, floatMatrix);
			}
		}

		/// <summary>
		/// Disposes this shader program and frees GPU resources
		/// </summary>
		public void Dispose()
		{
			if (disposed) return;
			disposed = true;

			if (programId != 0)
			{
				try
				{
					Gl.glDeleteProgram(programId);
					OpenGLErrorHandler.LogDebug($"Shader program {programId} deleted");
				}
				catch (Exception ex)
				{
					OpenGLErrorHandler.LogError($"Exception deleting shader program {programId}: {ex.Message}");
				}
				programId = 0;
			}

			uniformLocations.Clear();
		}

		/// <summary>
		/// Finalizer to ensure cleanup
		/// </summary>
		~GLShader()
		{
			try
			{
				Dispose();
			}
			catch { }
		}
	}
}
