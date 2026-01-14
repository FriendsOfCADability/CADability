using System;
using System.Collections.Generic;

namespace CADability
{
	/// <summary>
	/// Provides built-in shader programs for common rendering tasks in OpenGL 2.1
	/// </summary>
	/// <remarks>
	/// This class maintains a cache of pre-built shader programs to avoid
	/// recompiling the same shaders repeatedly. Supports basic lighting,
	/// texture mapping, and solid color rendering.
	/// </remarks>
	[CLSCompliant(false)]
	public static class GLShaderLibrary
    {
        private static Dictionary<string, GLShader> shaderCache = new Dictionary<string, GLShader>();

        /// <summary>
        /// Basic flat color rendering (no lighting)
        /// </summary>
        public static GLShader GetFlatColorShader()
        {
            return GetOrCreateShader("FlatColor",
                GetVertexShaderFlatColor(),
                GetFragmentShaderFlatColor());
        }

        /// <summary>
        /// Phong lighting model with per-vertex lighting
        /// </summary>
        public static GLShader GetPhongShader()
        {
            return GetOrCreateShader("Phong",
                GetVertexShaderPhong(),
                GetFragmentShaderPhong());
        }

        /// <summary>
        /// Simple texture mapping
        /// </summary>
        public static GLShader GetTextureShader()
        {
            return GetOrCreateShader("Texture",
                GetVertexShaderTexture(),
                GetFragmentShaderTexture());
        }

        /// <summary>
        /// 2D orthographic rendering (for UI and background elements)
        /// </summary>
        public static GLShader GetOrtho2DShader()
        {
            return GetOrCreateShader("Ortho2D",
                GetVertexShaderOrtho2D(),
                GetFragmentShaderOrtho2D());
        }

        private static GLShader GetOrCreateShader(string name, string vertexSource, string fragmentSource)
        {
            if (!shaderCache.TryGetValue(name, out GLShader shader))
            {
                try
                {
                    shader = new GLShader(vertexSource, fragmentSource);
                    shaderCache[name] = shader;
                    OpenGLErrorHandler.LogDebug($"Shader '{name}' created and cached");
                }
                catch (Exception ex)
                {
                    OpenGLErrorHandler.LogError($"Failed to create shader '{name}': {ex.Message}");
                    throw;
                }
            }
            return shader;
        }

        /// <summary>
        /// Clears the shader cache and disposes all cached shaders
        /// Call this when switching OpenGL contexts or shutting down
        /// </summary>
        public static void ClearCache()
        {
            foreach (var shader in shaderCache.Values)
            {
                try
                {
                    shader.Dispose();
                }
                catch (Exception ex)
                {
                    OpenGLErrorHandler.LogError($"Exception disposing shader: {ex.Message}");
                }
            }
            shaderCache.Clear();
            OpenGLErrorHandler.LogDebug("Shader cache cleared");
        }

        #region Vertex Shaders

        private static string GetVertexShaderFlatColor()
        {
            return @"
#version 120

uniform mat4 uProjection;
uniform mat4 uModelView;

attribute vec3 position;
attribute vec4 color;

varying vec4 fragColor;

void main()
{
    gl_Position = uProjection * uModelView * vec4(position, 1.0);
    fragColor = color;
}
";
        }

        private static string GetVertexShaderPhong()
        {
            return @"
#version 120

uniform mat4 uProjection;
uniform mat4 uModelView;
uniform mat3 uNormalMatrix;
uniform vec3 uLightPosition;
uniform vec3 uViewPosition;

attribute vec3 position;
attribute vec3 normal;
attribute vec4 color;

varying vec3 fragNormal;
varying vec3 fragPosition;
varying vec4 fragColor;
varying vec3 fragLightPosition;
varying vec3 fragViewPosition;

void main()
{
    vec4 worldPos = uModelView * vec4(position, 1.0);
    gl_Position = uProjection * worldPos;
    
    fragPosition = worldPos.xyz;
    fragNormal = normalize(uNormalMatrix * normal);
    fragColor = color;
    fragLightPosition = uLightPosition;
    fragViewPosition = uViewPosition;
}
";
        }

        private static string GetVertexShaderTexture()
        {
            return @"
#version 120

uniform mat4 uProjection;
uniform mat4 uModelView;

attribute vec3 position;
attribute vec2 texCoord;

varying vec2 fragTexCoord;

void main()
{
    gl_Position = uProjection * uModelView * vec4(position, 1.0);
    fragTexCoord = texCoord;
}
";
        }

        private static string GetVertexShaderOrtho2D()
        {
            return @"
#version 120

uniform mat4 uProjection;

attribute vec2 position;
attribute vec4 color;

varying vec4 fragColor;

void main()
{
    gl_Position = uProjection * vec4(position, 0.0, 1.0);
    fragColor = color;
}
";
        }

        #endregion

        #region Fragment Shaders

        private static string GetFragmentShaderFlatColor()
        {
            return @"
#version 120

varying vec4 fragColor;

void main()
{
    gl_FragColor = fragColor;
}
";
        }

        private static string GetFragmentShaderPhong()
        {
            return @"
#version 120

uniform vec3 uLightAmbient;
uniform vec3 uLightDiffuse;
uniform vec3 uLightSpecular;
uniform float uShininess;

varying vec3 fragNormal;
varying vec3 fragPosition;
varying vec4 fragColor;
varying vec3 fragLightPosition;
varying vec3 fragViewPosition;

void main()
{
    vec3 normal = normalize(fragNormal);
    vec3 lightDir = normalize(fragLightPosition - fragPosition);
    vec3 viewDir = normalize(fragViewPosition - fragPosition);
    
    // Ambient
    vec3 ambient = uLightAmbient * fragColor.rgb;
    
    // Diffuse
    float diff = max(dot(normal, lightDir), 0.0);
    vec3 diffuse = uLightDiffuse * diff * fragColor.rgb;
    
    // Specular
    vec3 reflectDir = reflect(-lightDir, normal);
    float spec = pow(max(dot(viewDir, reflectDir), 0.0), uShininess);
    vec3 specular = uLightSpecular * spec;
    
    // Two-sided lighting
    if (dot(normal, lightDir) < 0.0)
    {
        normal = -normal;
        lightDir = normalize(fragLightPosition - fragPosition);
        diff = max(dot(normal, lightDir), 0.0);
        diffuse = uLightDiffuse * diff * fragColor.rgb;
    }
    
    vec3 result = ambient + diffuse + specular;
    gl_FragColor = vec4(result, fragColor.a);
}
";
        }

        private static string GetFragmentShaderTexture()
        {
            return @"
#version 120

uniform sampler2D uTexture;

varying vec2 fragTexCoord;

void main()
{
    gl_FragColor = texture2D(uTexture, fragTexCoord);
}
";
        }

        private static string GetFragmentShaderOrtho2D()
        {
            return @"
#version 120

varying vec4 fragColor;

void main()
{
    gl_FragColor = fragColor;
}
";
        }

        #endregion
    }
}
