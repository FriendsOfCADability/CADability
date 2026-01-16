using System;

namespace CADability
{
	/// <summary>
	/// OpenGL shader types
	/// </summary>
	[CLSCompliant(false)]
	public static class GLShaderType
	{
		public const uint VertexShader = 0x8B31;
		public const uint FragmentShader = 0x8B30;
	}

	/// <summary>
	/// OpenGL shader parameters for glGetShaderiv
	/// </summary>
	[CLSCompliant(false)]
	public static class GLShaderParameter
	{
		public const int CompileStatus = 0x8B81;
		public const int InfoLogLength = 0x8B84;
	}

	/// <summary>
	/// OpenGL program parameters for glGetProgramiv
	/// </summary>
	[CLSCompliant(false)]
	public static class GLProgramParameter
	{
		public const int LinkStatus = 0x8B82;
		public const int InfoLogLength = 0x8B84;
	}

	/// <summary>
	/// OpenGL state query parameters for glGetIntegerv
	/// </summary>
	[CLSCompliant(false)]
	public static class GLGetParameter
	{
		public const int CurrentProgram = 0x8B8D;
	}

	/// <summary>
	/// OpenGL buffer targets
	/// </summary>
	[CLSCompliant(false)]
	public static class GLBufferTarget
	{
		public const uint ArrayBuffer = 0x8892;
		public const uint ElementArrayBuffer = 0x8893;
		public const uint CopyReadBuffer = 0x8F36;
		public const uint CopyWriteBuffer = 0x8F37;
	}

	/// <summary>
	/// OpenGL buffer usage hints
	/// </summary>
	[CLSCompliant(false)]
	public static class GLBufferUsage
	{
		public const uint StaticDraw = 0x88E4;
		public const uint DynamicDraw = 0x88E8;
		public const uint StreamDraw = 0x88E0;
	}

	/// <summary>
	/// OpenGL primitive types for drawing
	/// </summary>
	[CLSCompliant(false)]
	public static class GLPrimitiveType
	{
		public const uint Triangles = 0x0004;
		public const uint TriangleStrip = 0x0005;
		public const uint TriangleFan = 0x0006;
	}

	/// <summary>
	/// OpenGL data types
	/// </summary>
	[CLSCompliant(false)]
	public static class GLDataType
	{
		public const uint Float = 0x1406;
		public const uint UnsignedInt = 0x1405;
		public const uint UnsignedShort = 0x1403;
		public const uint Byte = 0x1400;
	}
}
