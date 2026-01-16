using System;
using System.Runtime.InteropServices;
using System.Security;

namespace CADability
{
	/// <summary>
	/// Dynamically loads OpenGL function pointers at runtime
	/// Allows for runtime resolution of OpenGL functions without compile-time DllImport declarations
	/// </summary>
	[CLSCompliant(false)]
	public static class GLFunctionLoader
	{
		private const string OPENGL_DLL = "opengl32.dll";
		private static IntPtr openglModule = IntPtr.Zero;

		// Delegate definitions for shader functions
		[SuppressUnmanagedCodeSecurity]
		public delegate uint glCreateProgram();

		[SuppressUnmanagedCodeSecurity]
		public delegate void glDeleteProgram(uint program);

		[SuppressUnmanagedCodeSecurity]
		public delegate void glLinkProgram(uint program);

		[SuppressUnmanagedCodeSecurity]
		public delegate void glUseProgram(uint program);

		[SuppressUnmanagedCodeSecurity]
		public delegate void glAttachShader(uint program, uint shader);

		[SuppressUnmanagedCodeSecurity]
		public delegate void glBindAttribLocation(uint program, uint index, string name);

		[SuppressUnmanagedCodeSecurity]
		public delegate void glGetProgramiv(uint program, int pname, out int param);

		[SuppressUnmanagedCodeSecurity]
		public delegate void glGetProgramInfoLog(uint program, int bufSize, out int length, System.Text.StringBuilder infoLog);

		[SuppressUnmanagedCodeSecurity]
		public delegate uint glCreateShader(uint type);

		[SuppressUnmanagedCodeSecurity]
		public delegate void glDeleteShader(uint shader);

		[SuppressUnmanagedCodeSecurity]
		public delegate void glShaderSource(uint shader, int count, string[] strings, int[] lengths);

		[SuppressUnmanagedCodeSecurity]
		public delegate void glCompileShader(uint shader);

		[SuppressUnmanagedCodeSecurity]
		public delegate void glGetShaderiv(uint shader, int pname, out int param);

		[SuppressUnmanagedCodeSecurity]
		public delegate void glGetShaderInfoLog(uint shader, int bufSize, out int length, System.Text.StringBuilder infoLog);

		[SuppressUnmanagedCodeSecurity]
		public delegate int glGetUniformLocation(uint program, string name);

		[SuppressUnmanagedCodeSecurity]
		public delegate void glUniform1f(int location, float v0);

		[SuppressUnmanagedCodeSecurity]
		public delegate void glUniform1i(int location, int v0);

		[SuppressUnmanagedCodeSecurity]
		public delegate void glUniform2f(int location, float v0, float v1);

		[SuppressUnmanagedCodeSecurity]
		public delegate void glUniform3f(int location, float v0, float v1, float v2);

		[SuppressUnmanagedCodeSecurity]
		public delegate void glUniform4f(int location, float v0, float v1, float v2, float v3);

		[SuppressUnmanagedCodeSecurity]
		public delegate void glUniformMatrix4fv(int location, int count, bool transpose, float[] value);

		[SuppressUnmanagedCodeSecurity]
		public delegate void glGetIntegerv(int pname, out int param);

		// VBO and buffer management delegates
		[SuppressUnmanagedCodeSecurity]
		public delegate void glGenBuffers(int n, uint[] buffers);

		[SuppressUnmanagedCodeSecurity]
		public delegate void glBindBuffer(uint target, uint buffer);

		[SuppressUnmanagedCodeSecurity]
		public delegate void glBufferData(uint target, IntPtr size, IntPtr data, uint usage);

		[SuppressUnmanagedCodeSecurity]
		public delegate void glDeleteBuffers(int n, uint[] buffers);

		// Vertex attribute and VAO delegates
		[SuppressUnmanagedCodeSecurity]
		public delegate void glGenVertexArrays(int n, uint[] arrays);

		[SuppressUnmanagedCodeSecurity]
		public delegate void glBindVertexArray(uint array);

		[SuppressUnmanagedCodeSecurity]
		public delegate void glVertexAttribPointer(uint index, int size, uint type, bool normalized, int stride, IntPtr pointer);

		[SuppressUnmanagedCodeSecurity]
		public delegate void glEnableVertexAttribArray(uint index);

		[SuppressUnmanagedCodeSecurity]
		public delegate void glDisableVertexAttribArray(uint index);

		[SuppressUnmanagedCodeSecurity]
		public delegate void glDrawElements(uint mode, int count, uint type, IntPtr indices);

		[SuppressUnmanagedCodeSecurity]
		public delegate void glDeleteVertexArrays(int n, uint[] arrays);

		// Static instances of dynamically loaded functions
		public static glCreateProgram CreateProgram;
		public static glDeleteProgram DeleteProgram;
		public static glLinkProgram LinkProgram;
		public static glUseProgram UseProgram;
		public static glAttachShader AttachShader;
		public static glBindAttribLocation BindAttribLocation;
		public static glGetProgramiv GetProgramiv;
		public static glGetProgramInfoLog GetProgramInfoLog;
		public static glCreateShader CreateShader;
		public static glDeleteShader DeleteShader;
		public static glShaderSource ShaderSource;
		public static glCompileShader CompileShader;
		public static glGetShaderiv GetShaderiv;
		public static glGetShaderInfoLog GetShaderInfoLog;
		public static glGetUniformLocation GetUniformLocation;
		public static glUniform1f Uniform1f;
		public static glUniform1i Uniform1i;
		public static glUniform2f Uniform2f;
		public static glUniform3f Uniform3f;
		public static glUniform4f Uniform4f;
		public static glUniformMatrix4fv UniformMatrix4fv;
		public static glGetIntegerv GetIntegerv;

		// VBO and buffer management statics
		public static glGenBuffers GenBuffers;
		public static glBindBuffer BindBuffer;
		public static glBufferData BufferData;
		public static glDeleteBuffers DeleteBuffers;

		// Vertex attribute and VAO statics
		public static glGenVertexArrays GenVertexArrays;
		public static glBindVertexArray BindVertexArray;
		public static glVertexAttribPointer VertexAttribPointer;
		public static glEnableVertexAttribArray EnableVertexAttribArray;
		public static glDisableVertexAttribArray DisableVertexAttribArray;
		public static glDrawElements DrawElements;
		public static glDeleteVertexArrays DeleteVertexArrays;

		/// <summary>
		/// Loads all required OpenGL shader function pointers
		/// Must be called during initialization before any shader operations
		/// </summary>
		/// <exception cref="InvalidOperationException">Thrown if OpenGL module cannot be loaded or required functions are missing</exception>
		public static void Initialize()
		{
			if (openglModule == IntPtr.Zero)
			{
				openglModule = Kernel.GetModuleHandle(OPENGL_DLL);
				if (openglModule == IntPtr.Zero)
				{
					throw new InvalidOperationException($"Failed to load {OPENGL_DLL}. Ensure OpenGL is installed and accessible on your system.");
				}
			}

			try
			{
				// Load shader function pointers
				CreateProgram = LoadFunction<glCreateProgram>("glCreateProgram");
				DeleteProgram = LoadFunction<glDeleteProgram>("glDeleteProgram");
				LinkProgram = LoadFunction<glLinkProgram>("glLinkProgram");
				UseProgram = LoadFunction<glUseProgram>("glUseProgram");
				AttachShader = LoadFunction<glAttachShader>("glAttachShader");
				BindAttribLocation = LoadFunction<glBindAttribLocation>("glBindAttribLocation");
				GetProgramiv = LoadFunction<glGetProgramiv>("glGetProgramiv");
				GetProgramInfoLog = LoadFunction<glGetProgramInfoLog>("glGetProgramInfoLog");
				CreateShader = LoadFunction<glCreateShader>("glCreateShader");
				DeleteShader = LoadFunction<glDeleteShader>("glDeleteShader");
				ShaderSource = LoadFunction<glShaderSource>("glShaderSource");
				CompileShader = LoadFunction<glCompileShader>("glCompileShader");
				GetShaderiv = LoadFunction<glGetShaderiv>("glGetShaderiv");
				GetShaderInfoLog = LoadFunction<glGetShaderInfoLog>("glGetShaderInfoLog");
				GetUniformLocation = LoadFunction<glGetUniformLocation>("glGetUniformLocation");
				Uniform1f = LoadFunction<glUniform1f>("glUniform1f");
				Uniform1i = LoadFunction<glUniform1i>("glUniform1i");
				Uniform2f = LoadFunction<glUniform2f>("glUniform2f");
				Uniform3f = LoadFunction<glUniform3f>("glUniform3f");
				Uniform4f = LoadFunction<glUniform4f>("glUniform4f");
				UniformMatrix4fv = LoadFunction<glUniformMatrix4fv>("glUniformMatrix4fv");
				GetIntegerv = LoadFunction<glGetIntegerv>("glGetIntegerv");

				// Load VBO and buffer function pointers
				GenBuffers = LoadFunction<glGenBuffers>("glGenBuffers");
				BindBuffer = LoadFunction<glBindBuffer>("glBindBuffer");
				BufferData = LoadFunction<glBufferData>("glBufferData");
				DeleteBuffers = LoadFunction<glDeleteBuffers>("glDeleteBuffers");

				// Load VAO and vertex attribute function pointers
				GenVertexArrays = LoadFunction<glGenVertexArrays>("glGenVertexArrays");
				BindVertexArray = LoadFunction<glBindVertexArray>("glBindVertexArray");
				VertexAttribPointer = LoadFunction<glVertexAttribPointer>("glVertexAttribPointer");
				EnableVertexAttribArray = LoadFunction<glEnableVertexAttribArray>("glEnableVertexAttribArray");
				DisableVertexAttribArray = LoadFunction<glDisableVertexAttribArray>("glDisableVertexAttribArray");
				DrawElements = LoadFunction<glDrawElements>("glDrawElements");
				DeleteVertexArrays = LoadFunction<glDeleteVertexArrays>("glDeleteVertexArrays");
			}
			catch (InvalidOperationException ex)
			{
				// Clean up on partial load failure
				Unload();
				throw new InvalidOperationException($"Failed to load required OpenGL shader functions: {ex.Message}", ex);
			}
		}

		/// <summary>
		/// Loads a single OpenGL function pointer and returns a delegate
		/// </summary>
		/// <typeparam name="T">Delegate type matching the function signature</typeparam>
		/// <param name="functionName">Name of the OpenGL function to load</param>
		/// <returns>Delegate that can be used to call the function</returns>
		/// <exception cref="InvalidOperationException">Thrown if function cannot be found</exception>
		private static T LoadFunction<T>(string functionName) where T : class
		{
			if (openglModule == IntPtr.Zero)
			{
				throw new InvalidOperationException("OpenGL module not loaded. Call Initialize() first.");
			}

			// Try wglGetProcAddress first for modern OpenGL functions
			// Modern shader functions like glCreateProgram are exported by the driver, not the DLL
			IntPtr functionPointer = Wgl.wglGetProcAddress(functionName);
			
			// Fall back to GetProcAddress for legacy functions or if wglGetProcAddress fails
			if (functionPointer == IntPtr.Zero)
			{
				functionPointer = Kernel.GetProcAddress(openglModule, functionName);
			}
			
			if (functionPointer == IntPtr.Zero)
			{
				throw new InvalidOperationException($"Failed to load OpenGL function: {functionName}");
			}

			return Marshal.GetDelegateForFunctionPointer(functionPointer, typeof(T)) as T;
		}

		/// <summary>
		/// Unloads the OpenGL module and clears all function pointers
		/// </summary>
		public static void Unload()
		{
			// Clear all delegates
			CreateProgram = null;
			DeleteProgram = null;
			LinkProgram = null;
			UseProgram = null;
			AttachShader = null;
			BindAttribLocation = null;
			GetProgramiv = null;
			GetProgramInfoLog = null;
			CreateShader = null;
			DeleteShader = null;
			ShaderSource = null;
			CompileShader = null;
			GetShaderiv = null;
			GetShaderInfoLog = null;
			GetUniformLocation = null;
			Uniform1f = null;
			Uniform1i = null;
			Uniform2f = null;
			Uniform3f = null;
			Uniform4f = null;
			UniformMatrix4fv = null;
			GetIntegerv = null;

			// Clear VBO and buffer delegates
			GenBuffers = null;
			BindBuffer = null;
			BufferData = null;
			DeleteBuffers = null;

			// Clear VAO and vertex attribute delegates
			GenVertexArrays = null;
			BindVertexArray = null;
			VertexAttribPointer = null;
			EnableVertexAttribArray = null;
			DisableVertexAttribArray = null;
			DrawElements = null;
			DeleteVertexArrays = null;

			if (openglModule != IntPtr.Zero)
			{
				Kernel.FreeLibrary(openglModule);
				openglModule = IntPtr.Zero;
			}
		}
	}
}
