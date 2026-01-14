using System;
using System.Runtime.InteropServices;

namespace CADability
{
    /// <summary>
    /// OpenGL 2.1 context initialization and capability detection
    /// </summary>
    /// <remarks>
    /// This class handles creation and configuration of OpenGL 2.1+ contexts,
    /// including capability detection and version validation.
    /// </remarks>
    public static class OpenGL21Initialization
    {
        /// <summary>
        /// Gets the OpenGL version string
        /// </summary>
        public static string GetOpenGLVersion()
        {
            try
            {
                IntPtr versionPtr = Gl.glGetString(Gl.GL_VERSION);
                if (versionPtr != IntPtr.Zero)
                {
                    return Marshal.PtrToStringAnsi(versionPtr);
                }
            }
            catch (Exception ex)
            {
                OpenGLErrorHandler.LogError($"Failed to get OpenGL version: {ex.Message}");
            }
            return "Unknown";
        }

        /// <summary>
        /// Gets the OpenGL vendor string
        /// </summary>
        public static string GetVendor()
        {
            try
            {
                IntPtr vendorPtr = Gl.glGetString(Gl.GL_VENDOR);
                if (vendorPtr != IntPtr.Zero)
                {
                    return Marshal.PtrToStringAnsi(vendorPtr);
                }
            }
            catch (Exception ex)
            {
                OpenGLErrorHandler.LogError($"Failed to get OpenGL vendor: {ex.Message}");
            }
            return "Unknown";
        }

        /// <summary>
        /// Gets the OpenGL renderer string
        /// </summary>
        public static string GetRenderer()
        {
            try
            {
                IntPtr rendererPtr = Gl.glGetString(Gl.GL_RENDERER);
                if (rendererPtr != IntPtr.Zero)
                {
                    return Marshal.PtrToStringAnsi(rendererPtr);
                }
            }
            catch (Exception ex)
            {
                OpenGLErrorHandler.LogError($"Failed to get OpenGL renderer: {ex.Message}");
            }
            return "Unknown";
        }

        /// <summary>
        /// Gets the GLSL version string
        /// </summary>
        public static string GetGLSLVersion()
        {
            try
            {
                IntPtr versionPtr = Gl.glGetString(Gl.GL_SHADING_LANGUAGE_VERSION);
                if (versionPtr != IntPtr.Zero)
                {
                    return Marshal.PtrToStringAnsi(versionPtr);
                }
            }
            catch (Exception ex)
            {
                OpenGLErrorHandler.LogError($"Failed to get GLSL version: {ex.Message}");
            }
            return "Unknown";
        }

        /// <summary>
        /// Checks if a specific OpenGL extension is supported
        /// </summary>
        public static bool IsExtensionSupported(string extensionName)
        {
            try
            {
                IntPtr extensionsPtr = Gl.glGetString(Gl.GL_EXTENSIONS);
                if (extensionsPtr != IntPtr.Zero)
                {
                    string extensions = Marshal.PtrToStringAnsi(extensionsPtr);
                    return extensions.Contains(extensionName);
                }
            }
            catch (Exception ex)
            {
                OpenGLErrorHandler.LogError($"Failed to check extension {extensionName}: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// Initializes OpenGL 2.1 context with proper state
        /// </summary>
        public static void InitializeOpenGL21()
        {
            OpenGLErrorHandler.ClearErrors();

            // Log context information
            string version = GetOpenGLVersion();
            string vendor = GetVendor();
            string renderer = GetRenderer();
            string glslVersion = GetGLSLVersion();

            OpenGLErrorHandler.LogInfo($"OpenGL Version: {version}");
            OpenGLErrorHandler.LogInfo($"Vendor: {vendor}");
            OpenGLErrorHandler.LogInfo($"Renderer: {renderer}");
            OpenGLErrorHandler.LogInfo($"GLSL Version: {glslVersion}");

            // Validate minimum version (OpenGL 2.1)
            if (!IsOpenGL21OrNewer(version))
            {
                OpenGLErrorHandler.LogWarning($"OpenGL version {version} may not support all features. OpenGL 2.1+ recommended.");
            }

            // Set up basic rendering state
            Gl.glClearColor(0.0f, 0.0f, 0.0f, 1.0f);
            Gl.glEnable(Gl.GL_DEPTH_TEST);
            Gl.glDepthFunc(Gl.GL_LEQUAL);
            Gl.glEnable(Gl.GL_CULL_FACE);
            Gl.glCullFace(Gl.GL_BACK);
            Gl.glFrontFace(Gl.GL_CCW);
            
            // Enable lighting for Phong shading
            Gl.glEnable(Gl.GL_LIGHTING);
            Gl.glEnable(Gl.GL_LIGHT0);
            Gl.glEnable(Gl.GL_COLOR_MATERIAL);
            Gl.glColorMaterial(Gl.GL_FRONT_AND_BACK, Gl.GL_AMBIENT_AND_DIFFUSE);
            
            // Two-sided lighting
            Gl.glLightModeli(Gl.GL_LIGHT_MODEL_TWO_SIDE, Gl.GL_TRUE);
            Gl.glLightModelfv(Gl.GL_LIGHT_MODEL_AMBIENT, new float[] { 0.2f, 0.2f, 0.2f, 1.0f });

            // Initialize shader library
            try
            {
                GLShaderLibrary.GetFlatColorShader();
                GLShaderLibrary.GetPhongShader();
                GLShaderLibrary.GetTextureShader();
                GLShaderLibrary.GetOrtho2DShader();
                OpenGLErrorHandler.LogInfo("Shader library initialized successfully");
            }
            catch (Exception ex)
            {
                OpenGLErrorHandler.LogError($"Failed to initialize shader library: {ex.Message}");
            }

            // Check for optional but useful extensions
            if (IsExtensionSupported("GL_ARB_vertex_buffer_object"))
            {
                OpenGLErrorHandler.LogInfo("VBO support detected (GL_ARB_vertex_buffer_object)");
            }

            if (IsExtensionSupported("GL_ARB_shader_objects"))
            {
                OpenGLErrorHandler.LogInfo("Shader support detected (GL_ARB_shader_objects)");
            }

            if (IsExtensionSupported("GL_EXT_framebuffer_object"))
            {
                OpenGLErrorHandler.LogInfo("Framebuffer support detected (GL_EXT_framebuffer_object)");
            }

            OpenGLErrorHandler.CheckError("InitializeOpenGL21");
        }

        /// <summary>
        /// Validates that the current OpenGL version is 2.1 or newer
        /// </summary>
        private static bool IsOpenGL21OrNewer(string versionString)
        {
            try
            {
                // Parse version string like "2.1", "3.0", "4.5", etc.
                string[] parts = versionString.Split('.');
                if (parts.Length >= 2 &&
                    int.TryParse(parts[0], out int major) &&
                    int.TryParse(parts[1], out int minor))
                {
                    return major > 2 || (major == 2 && minor >= 1);
                }
            }
            catch { }
            
            return false;
        }

        /// <summary>
        /// Gets OpenGL context capabilities information
        /// </summary>
        public static string GetCapabilitiesInfo()
        {
            var sb = new System.Text.StringBuilder();
            
            sb.AppendLine("=== OpenGL Capabilities ===");
            sb.AppendLine($"Version: {GetOpenGLVersion()}");
            sb.AppendLine($"Vendor: {GetVendor()}");
            sb.AppendLine($"Renderer: {GetRenderer()}");
            sb.AppendLine($"GLSL: {GetGLSLVersion()}");
            
            // Get various limits
            int maxTexUnits;
            Gl.glGetIntegerv(Gl.GL_MAX_TEXTURE_UNITS, out maxTexUnits);
            sb.AppendLine($"Max Texture Units: {maxTexUnits}");
            
            int maxTexSize;
            Gl.glGetIntegerv(Gl.GL_MAX_TEXTURE_SIZE, out maxTexSize);
            sb.AppendLine($"Max Texture Size: {maxTexSize}x{maxTexSize}");
            
            int maxLights;
            Gl.glGetIntegerv(Gl.GL_MAX_LIGHTS, out maxLights);
            sb.AppendLine($"Max Lights: {maxLights}");
            
            float[] range = new float[2];
            Gl.glGetFloatv(Gl.GL_LINE_WIDTH_RANGE, range);
            sb.AppendLine($"Line Width Range: {range[0]}-{range[1]}");
            
            // Check key extensions
            sb.AppendLine("Key Extensions:");
            sb.AppendLine($"  VBO (GL_ARB_vertex_buffer_object): {IsExtensionSupported("GL_ARB_vertex_buffer_object")}");
            sb.AppendLine($"  Shaders (GL_ARB_shader_objects): {IsExtensionSupported("GL_ARB_shader_objects")}");
            sb.AppendLine($"  FBO (GL_EXT_framebuffer_object): {IsExtensionSupported("GL_EXT_framebuffer_object")}");
            
            return sb.ToString();
        }
    }
}
