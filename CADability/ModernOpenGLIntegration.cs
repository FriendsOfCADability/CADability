using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using CADability;

namespace CADability
{
    /// <summary>
    /// Manages integration of modern OpenGL features (shaders, VBOs) with PaintToOpenGL.
    /// Provides a bridge between legacy display list rendering and modern OpenGL pipelines.
    /// </summary>
    /// <remarks>
    /// This class handles:
    /// - Shader compilation and caching
    /// - Vertex buffer object (VBO) management
    /// - Capability detection and fallback mechanisms
    /// - Resource lifecycle management
    /// 
    /// It allows PaintToOpenGL to gradually adopt modern OpenGL techniques
    /// while maintaining backward compatibility with legacy rendering paths.
    /// </remarks>
    public class ModernOpenGLIntegration
    {
        private static bool capabilitiesDetected = false;
        private static bool supportsShaders = false;
        private static bool supportsVBO = false;
        private static bool supportsVAO = false;

        // Shader cache for common rendering scenarios
        private Dictionary<string, GLShader> shaderCache = new Dictionary<string, GLShader>();
        private Dictionary<string, GLVertexBuffer> vboCache = new Dictionary<string, GLVertexBuffer>();

        // Reference to parent PaintToOpenGL for context access
        private object parentPainter;

        public ModernOpenGLIntegration(object painter)
        {
            parentPainter = painter ?? throw new ArgumentNullException(nameof(painter));
        }

        /// <summary>
        /// Detects available modern OpenGL capabilities.
        /// CRITICAL: Must be called with an active OpenGL context via wglMakeCurrent().
        /// Should be called once after OpenGL context initialization and is_made_current.
        /// </summary>
        /// <remarks>
        /// This method requires:
        /// 1. Valid OpenGL context created via wglCreateContext()
        /// 2. Context made current via wglMakeCurrent(deviceContext, renderContext)
        /// 3. Pixel format set via SetPixelFormat()
        /// 
        /// Fails safely if context is not available - all capabilities default to false.
        /// </remarks>
        public static void DetectCapabilities()
        {
            if (capabilitiesDetected)
                return;

            try
            {
                // CRITICAL: Verify we have an active OpenGL context before querying capabilities
                IntPtr currentContext = Wgl.wglGetCurrentContext();
                if (currentContext == IntPtr.Zero)
                {
                    OpenGLErrorHandler.LogWarning(
                        "DetectCapabilities: No active OpenGL context detected. " +
                        "Ensure wglMakeCurrent() was called with valid device and render contexts. " +
                        "All modern OpenGL features will be disabled.");
                    capabilitiesDetected = true;
                    return;
                }

                // Now safe to query OpenGL state with active context
                IntPtr glVersionPtr = Gl.glGetString(Gl.GL_VERSION);
                
                // Handle case where glGetString returns null despite valid context
                if (glVersionPtr == IntPtr.Zero)
                {
                    OpenGLErrorHandler.LogWarning(
                        "DetectCapabilities: glGetString(GL_VERSION) returned null despite active context. " +
                        "Context may not be fully initialized. All modern OpenGL features will be disabled.");
                    capabilitiesDetected = true;
                    return;
                }

                // Convert OpenGL version string
                string glVersion = Marshal.PtrToStringAnsi(glVersionPtr);
                if (string.IsNullOrEmpty(glVersion))
                {
                    OpenGLErrorHandler.LogWarning(
                        "DetectCapabilities: OpenGL version string is empty. " +
                        "All modern OpenGL features will be disabled.");
                    capabilitiesDetected = true;
                    return;
                }

                // Parse version string (e.g., "4.5.0 NVIDIA 399.00" or "2.1")
                // Extract just the numeric part before any space or additional text
                string versionNumeric = glVersion.Split(' ')[0];
                string[] versionParts = versionNumeric.Split('.');
                
                if (versionParts.Length >= 2 &&
                    int.TryParse(versionParts[0], out int major) &&
                    int.TryParse(versionParts[1], out int minor))
                {
                    // Determine capability support based on version
                    supportsShaders = (major > 2) || (major == 2 && minor >= 0);
                    supportsVBO = (major > 1) || (major == 1 && minor >= 5);
                    supportsVAO = (major > 3) || (major == 3 && minor >= 0);
                    
                    OpenGLErrorHandler.LogDebug(
                        $"DetectCapabilities: OpenGL {major}.{minor} detected. " +
                        $"Shaders={supportsShaders}, VBO={supportsVBO}, VAO={supportsVAO}");
                }
                else
                {
                    OpenGLErrorHandler.LogWarning(
                        $"DetectCapabilities: Failed to parse OpenGL version '{versionNumeric}'. " +
                        $"All modern OpenGL features will be disabled.");
                    capabilitiesDetected = true;
                    return;
                }
            }
            catch (Exception ex)
            {
                OpenGLErrorHandler.LogError(
                    $"DetectCapabilities: Unexpected error detecting OpenGL capabilities: {ex.Message}");
                capabilitiesDetected = true;
            }
            finally
            {
                capabilitiesDetected = true;
            }
        }

        /// <summary>
        /// Gets whether modern shaders are supported.
        /// </summary>
        public static bool SupportsShaders => supportsShaders;

        /// <summary>
        /// Gets whether Vertex Buffer Objects are supported.
        /// </summary>
        public static bool SupportsVBO => supportsVBO;

        /// <summary>
        /// Gets whether Vertex Array Objects are supported.
        /// </summary>
        public static bool SupportsVAO => supportsVAO;

        /// <summary>
        /// Gets or creates a shader program from cache.
        /// </summary>
        /// <param name="shaderKey">Unique identifier for the shader</param>
        /// <param name="vertexSource">Vertex shader GLSL source</param>
        /// <param name="fragmentSource">Fragment shader GLSL source</param>
        /// <returns>Compiled GLShader or null if shaders unsupported</returns>
        public GLShader GetOrCreateShader(string shaderKey, string vertexSource, string fragmentSource)
        {
            if (!supportsShaders)
                return null;

            if (shaderCache.TryGetValue(shaderKey, out GLShader cached))
                return cached;

            try
            {
                GLShader shader = new GLShader(vertexSource, fragmentSource);
                shaderCache[shaderKey] = shader;
                OpenGLErrorHandler.LogDebug($"Created shader: {shaderKey}");
                return shader;
            }
            catch (Exception ex)
            {
                OpenGLErrorHandler.LogError($"Failed to create shader {shaderKey}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Creates a basic lighting shader for modern rendering.
        /// </summary>
        /// <returns>GLShader for phong lighting or null if unsupported</returns>
        public GLShader GetPhongLightingShader()
        {
            const string vertexShader = @"
                #version 120
                
                varying vec3 fragNormal;
                varying vec3 fragPosition;
                
                void main()
                {
                    fragNormal = normalize(gl_NormalMatrix * gl_Normal);
                    fragPosition = vec3(gl_ModelViewMatrix * gl_Vertex);
                    gl_Position = gl_ProjectionMatrix * gl_ModelViewMatrix * gl_Vertex;
                }
            ";

            const string fragmentShader = @"
                #version 120
                
                varying vec3 fragNormal;
                varying vec3 fragPosition;
                
                uniform vec3 lightPosition;
                uniform vec3 viewPosition;
                uniform vec3 objectColor;
                uniform float ambientStrength;
                uniform float diffuseStrength;
                uniform float specularStrength;
                uniform float shininess;
                
                void main()
                {
                    // Ambient
                    vec3 ambient = ambientStrength * objectColor;
                    
                    // Diffuse
                    vec3 norm = normalize(fragNormal);
                    vec3 lightDir = normalize(lightPosition - fragPosition);
                    float diff = max(dot(norm, lightDir), 0.0);
                    vec3 diffuse = diffuseStrength * diff * objectColor;
                    
                    // Specular
                    vec3 viewDir = normalize(viewPosition - fragPosition);
                    vec3 reflectDir = reflect(-lightDir, norm);
                    float spec = pow(max(dot(viewDir, reflectDir), 0.0), shininess);
                    vec3 specular = specularStrength * spec * vec3(1.0);
                    
                    vec3 result = ambient + diffuse + specular;
                    gl_FragColor = vec4(result, 1.0);
                }
            ";

            return GetOrCreateShader("phong_lighting", vertexShader, fragmentShader);
        }

        /// <summary>
        /// Creates a basic color shader for simple geometry rendering.
        /// </summary>
        /// <returns>GLShader for basic color rendering or null if unsupported</returns>
        public GLShader GetBasicColorShader()
        {
            const string vertexShader = @"
                #version 120
                
                void main()
                {
                    gl_Position = gl_ProjectionMatrix * gl_ModelViewMatrix * gl_Vertex;
                }
            ";

            const string fragmentShader = @"
                #version 120
                
                uniform vec4 objectColor;
                
                void main()
                {
                    gl_FragColor = objectColor;
                }
            ";

            return GetOrCreateShader("basic_color", vertexShader, fragmentShader);
        }

        /// <summary>
        /// Creates a texture shader for bitmap rendering.
        /// </summary>
        /// <returns>GLShader for texture rendering or null if unsupported</returns>
        public GLShader GetTextureShader()
        {
            const string vertexShader = @"
                #version 120
                
                varying vec2 fragTexCoord;
                
                void main()
                {
                    fragTexCoord = gl_MultiTexCoord0;
                    gl_Position = gl_ProjectionMatrix * gl_ModelViewMatrix * gl_Vertex;
                }
            ";

            const string fragmentShader = @"
                #version 120
                
                varying vec2 fragTexCoord;
                uniform sampler2D texture0;
                
                void main()
                {
                    gl_FragColor = texture2D(texture0, fragTexCoord);
                }
            ";

            return GetOrCreateShader("texture", vertexShader, fragmentShader);
        }

        /// <summary>
        /// Cleans up all cached shaders and VBOs.
        /// Should be called during PaintToOpenGL.Dispose.
        /// </summary>
        public void Cleanup()
        {
            try
            {
                // Dispose all shaders
                foreach (var shader in shaderCache.Values)
                {
                    shader?.Dispose();
                }
                shaderCache.Clear();

                // Dispose all VBOs
                foreach (var vbo in vboCache.Values)
                {
                    vbo?.Dispose();
                }
                vboCache.Clear();

                OpenGLErrorHandler.LogDebug("ModernOpenGL resources cleaned up");
            }
            catch (Exception ex)
            {
                OpenGLErrorHandler.LogError($"Error cleaning up modern OpenGL resources: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets diagnostic information about modern OpenGL usage.
        /// </summary>
        /// <returns>String with statistics</returns>
        public string GetStatistics()
        {
            return $"Modern OpenGL Integration:\n" +
                   $"  Shaders cached: {shaderCache.Count}\n" +
                   $"  VBOs cached: {vboCache.Count}\n" +
                   $"  Capabilities - Shaders: {supportsShaders}, VBO: {supportsVBO}, VAO: {supportsVAO}";
        }
    }

    /// <summary>
    /// Alternative rendering path using modern OpenGL for PaintToOpenGL.
    /// This class encapsulates modern rendering techniques that can complement
    /// or replace legacy display list rendering.
    /// </summary>
    /// <remarks>
    /// Provides optimized rendering paths for:
    /// - Triangle meshes via VBOs
    /// - Textured geometry via shaders
    /// - Lighting calculations via shader-based Phong model
    /// 
    /// All methods gracefully degrade to legacy rendering if modern features unavailable.
    /// </remarks>
    public class ModernRenderingPath
    {
        private ModernOpenGLIntegration integration;
        private GLShader basicColorShader;
        private GLShader lightingShader;

        public ModernRenderingPath(ModernOpenGLIntegration modernIntegration)
        {
            integration = modernIntegration ?? throw new ArgumentNullException(nameof(modernIntegration));
            
            if (ModernOpenGLIntegration.SupportsShaders)
            {
                basicColorShader = integration.GetBasicColorShader();
                lightingShader = integration.GetPhongLightingShader();
            }
        }

        /// <summary>
        /// Renders a triangle mesh using modern VBO/shader approach if available.
        /// Falls back to immediate mode if not supported.
        /// </summary>
        public bool TryRenderTriangleMesh(GeoPoint[] vertices, GeoVector[] normals, int[] indices)
        {
            if (!ModernOpenGLIntegration.SupportsVBO || lightingShader == null)
                return false; // Fall back to legacy rendering

            if (vertices == null || vertices.Length == 0 || indices == null || indices.Length == 0)
                return false; // Invalid input

            try
            {
                // Use the lighting shader for rendering
                lightingShader.Use();

                // Prepare vertex data as interleaved arrays (position followed by normal)
                float[] vertexData = new float[vertices.Length * 6]; // 3 for position + 3 for normal
                for (int i = 0; i < vertices.Length; i++)
                {
                    // Position
                    vertexData[i * 6 + 0] = (float)vertices[i].x;
                    vertexData[i * 6 + 1] = (float)vertices[i].y;
                    vertexData[i * 6 + 2] = (float)vertices[i].z;

                    // Normal (use provided normal or calculate from geometry if missing)
                    if (normals != null && i < normals.Length)
                    {
                        vertexData[i * 6 + 3] = (float)normals[i].x;
                        vertexData[i * 6 + 4] = (float)normals[i].y;
                        vertexData[i * 6 + 5] = (float)normals[i].z;
                    }
                    else
                    {
                        // Default normal pointing upward if not provided
                        vertexData[i * 6 + 3] = 0.0f;
                        vertexData[i * 6 + 4] = 0.0f;
                        vertexData[i * 6 + 5] = 1.0f;
                    }
                }

                // Convert indices to unsigned int array
                uint[] indexData = new uint[indices.Length];
                for (int i = 0; i < indices.Length; i++)
                {
                    indexData[i] = (uint)indices[i];
                }

                // Set shader uniforms for lighting
                lightingShader.SetUniform("objectColor", 0.5f, 0.5f, 0.5f);
                lightingShader.SetUniform("lightPosition", 1.0f, 1.0f, 1.0f);
                lightingShader.SetUniform("viewPosition", 0.0f, 0.0f, 1.0f);
                lightingShader.SetUniform("ambientStrength", 0.1f);
                lightingShader.SetUniform("diffuseStrength", 0.7f);
                lightingShader.SetUniform("specularStrength", 0.5f);
                lightingShader.SetUniform("shininess", 32.0f);

                // Create Vertex Array Object (VAO)
                uint[] vaoHandle = new uint[1];
                GLFunctionLoader.GenVertexArrays(1, vaoHandle);
                GLFunctionLoader.BindVertexArray(vaoHandle[0]);

                try
                {
                    // Create Vertex Buffer Object (VBO) for vertex data
                    uint[] vboHandle = new uint[1];
                    GLFunctionLoader.GenBuffers(1, vboHandle);
                    GLFunctionLoader.BindBuffer(GLBufferTarget.ArrayBuffer, vboHandle[0]);

                    // Upload vertex data to GPU
                    GCHandle vertexHandle = GCHandle.Alloc(vertexData, GCHandleType.Pinned);
                    try
                    {
                        IntPtr vertexPtr = vertexHandle.AddrOfPinnedObject();
                        IntPtr vertexSize = new IntPtr(vertexData.Length * sizeof(float));
                        GLFunctionLoader.BufferData(
                            GLBufferTarget.ArrayBuffer,
                            vertexSize,
                            vertexPtr,
                            GLBufferUsage.StaticDraw);
                    }
                    finally
                    {
                        vertexHandle.Free();
                    }

                    // Configure vertex attributes
                    // Vertex position (location 0)
                    GLFunctionLoader.VertexAttribPointer(
                        0, // position attribute location
                        3, // 3 components (x, y, z)
                        GLDataType.Float,
                        false, // not normalized
                        24, // stride: 6 floats * 4 bytes = 24 bytes
                        IntPtr.Zero); // offset: position starts at 0
                    GLFunctionLoader.EnableVertexAttribArray(0);

                    // Vertex normal (location 1)
                    GLFunctionLoader.VertexAttribPointer(
                        1, // normal attribute location
                        3, // 3 components (nx, ny, nz)
                        GLDataType.Float,
                        false, // not normalized
                        24, // stride: 6 floats * 4 bytes = 24 bytes
                        new IntPtr(12)); // offset: normal starts at 12 bytes (3 floats)
                    GLFunctionLoader.EnableVertexAttribArray(1);

                    // Create Element Buffer Object (EBO/IBO) for indices
                    uint[] eboHandle = new uint[1];
                    GLFunctionLoader.GenBuffers(1, eboHandle);
                    GLFunctionLoader.BindBuffer(GLBufferTarget.ElementArrayBuffer, eboHandle[0]);

                    // Upload index data to GPU
                    GCHandle indexHandle = GCHandle.Alloc(indexData, GCHandleType.Pinned);
                    try
                    {
                        IntPtr indexPtr = indexHandle.AddrOfPinnedObject();
                        IntPtr indexSize = new IntPtr(indexData.Length * sizeof(uint));
                        GLFunctionLoader.BufferData(
                            GLBufferTarget.ElementArrayBuffer,
                            indexSize,
                            indexPtr,
                            GLBufferUsage.StaticDraw);
                    }
                    finally
                    {
                        indexHandle.Free();
                    }

                    // Render the triangles
                    GLFunctionLoader.DrawElements(
                        GLPrimitiveType.Triangles,
                        indices.Length, // number of indices to render
                        GLDataType.UnsignedInt,
                        IntPtr.Zero); // offset in the EBO

                    OpenGLErrorHandler.LogDebug(
                        $"Modern triangle rendering: {vertices.Length} vertices, {indices.Length / 3} triangles rendered");

                    // Clean up GPU resources
                    GLFunctionLoader.DeleteBuffers(1, vboHandle);
                    GLFunctionLoader.DeleteBuffers(1, eboHandle);
                }
                finally
                {
                    // Unbind and delete VAO
                    GLFunctionLoader.BindVertexArray(0);
                    GLFunctionLoader.DeleteVertexArrays(1, vaoHandle);
                }

                return true; // Modern rendering path was successfully used
            }
            catch (Exception ex)
            {
                OpenGLErrorHandler.LogError($"Error in modern rendering path: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the current lighting shader.
        /// </summary>
        public GLShader LightingShader => lightingShader;

        /// <summary>
        /// Cleans up resources used by this rendering path.
        /// </summary>
        public void Cleanup()
        {
            basicColorShader?.Dispose();
            lightingShader?.Dispose();
        }
    }
}
