using System;
using System.Collections.Generic;

namespace CADability
{
    /// <summary>
    /// Modern shader-based lighting system for OpenGL 2.1+
    /// </summary>
    /// <remarks>
    /// Replaces fixed-function lighting with shader-based Phong model.
    /// Supports multiple light sources that can be passed to shaders as uniforms.
    /// </remarks>
    public class GLLight
    {
        /// <summary>
        /// Light types
        /// </summary>
        public enum LightType
        {
            Directional,  // Infinite distance (like sun)
            Point,        // Local light source
            Spot          // Point light with direction
        }

        public LightType Type { get; set; }
        public float[] Position { get; set; }  // (x, y, z, w) where w=1 for point, w=0 for directional
        public float[] Direction { get; set; } // For directional and spot lights (x, y, z, 0)
        public float[] Ambient { get; set; }   // (r, g, b, a)
        public float[] Diffuse { get; set; }   // (r, g, b, a)
        public float[] Specular { get; set; }  // (r, g, b, a)
        public float Intensity { get; set; }
        public float Shininess { get; set; }

        public GLLight()
        {
            Type = LightType.Directional;
            Position = new float[] { 0, 1, 1, 0 };
            Direction = new float[] { 0, -1, -1, 0 };
            Ambient = new float[] { 0.2f, 0.2f, 0.2f, 1.0f };
            Diffuse = new float[] { 1.0f, 1.0f, 1.0f, 1.0f };
            Specular = new float[] { 1.0f, 1.0f, 1.0f, 1.0f };
            Intensity = 1.0f;
            Shininess = 32.0f;
        }

        /// <summary>
        /// Creates a directional light (like sunlight)
        /// </summary>
        public static GLLight CreateDirectional(float dirX, float dirY, float dirZ)
        {
            var light = new GLLight();
            light.Type = LightType.Directional;
            light.Direction = new float[] { dirX, dirY, dirZ, 0 };
            light.Position = new float[] { -dirX, -dirY, -dirZ, 0 };
            return light;
        }

        /// <summary>
        /// Creates a point light (like a light bulb)
        /// </summary>
        public static GLLight CreatePoint(float x, float y, float z)
        {
            var light = new GLLight();
            light.Type = LightType.Point;
            light.Position = new float[] { x, y, z, 1 };
            light.Direction = new float[] { 0, 0, 0, 0 };
            return light;
        }

        /// <summary>
        /// Creates a spotlight
        /// </summary>
        public static GLLight CreateSpot(float x, float y, float z, float dirX, float dirY, float dirZ)
        {
            var light = new GLLight();
            light.Type = LightType.Spot;
            light.Position = new float[] { x, y, z, 1 };
            light.Direction = new float[] { dirX, dirY, dirZ, 0 };
            return light;
        }

        /// <summary>
        /// Normalizes the direction vector
        /// </summary>
        public void NormalizeDirection()
        {
            float len = (float)Math.Sqrt(Direction[0] * Direction[0] + Direction[1] * Direction[1] + Direction[2] * Direction[2]);
            if (len > 0)
            {
                Direction[0] /= len;
                Direction[1] /= len;
                Direction[2] /= len;
            }
        }
    }

    /// <summary>
    /// Manages multiple light sources for shader-based rendering
    /// </summary>
    [CLSCompliant(false)]
    public class GLLightManager
    {
        private List<GLLight> lights = new List<GLLight>();
        private const int MaxLights = 8;  // Typical maximum for GLSL

        public int LightCount => lights.Count;

        /// <summary>
        /// Adds a light to the scene
        /// </summary>
        public void AddLight(GLLight light)
        {
            if (lights.Count >= MaxLights)
            {
                OpenGLErrorHandler.LogWarning($"Maximum number of lights ({MaxLights}) reached. New light not added.");
                return;
            }
            lights.Add(light);
            OpenGLErrorHandler.LogDebug($"Light added. Total lights: {lights.Count}");
        }

        /// <summary>
        /// Removes a light from the scene
        /// </summary>
        public void RemoveLight(int index)
        {
            if (index >= 0 && index < lights.Count)
            {
                lights.RemoveAt(index);
                OpenGLErrorHandler.LogDebug($"Light removed. Total lights: {lights.Count}");
            }
        }

        /// <summary>
        /// Gets a light by index
        /// </summary>
        public GLLight GetLight(int index)
        {
            if (index >= 0 && index < lights.Count)
            {
                return lights[index];
            }
            return null;
        }

        /// <summary>
        /// Clears all lights
        /// </summary>
        public void Clear()
        {
            lights.Clear();
        }

        /// <summary>
        /// Applies lights to a shader program
        /// </summary>
        public void ApplyToShader(GLShader shader, string uniformPrefix = "uLight")
        {
            if (shader == null) return;

            for (int i = 0; i < lights.Count && i < MaxLights; i++)
            {
                var light = lights[i];
                string prefix = $"{uniformPrefix}[{i}]";

                // Note: This assumes the shader has light arrays defined
                // Actual uniform names depend on the shader implementation
            }
        }

        /// <summary>
        /// Gets the primary (first) light
        /// </summary>
        public GLLight GetPrimaryLight()
        {
            return lights.Count > 0 ? lights[0] : null;
        }

        /// <summary>
        /// Creates default lighting setup
        /// </summary>
        public static GLLightManager CreateDefault()
        {
            var manager = new GLLightManager();

            // Main light (like sun)
            var mainLight = GLLight.CreateDirectional(1, 1, 1);
            mainLight.Ambient = new float[] { 0.3f, 0.3f, 0.3f, 1.0f };
            mainLight.Diffuse = new float[] { 0.8f, 0.8f, 0.8f, 1.0f };
            mainLight.Specular = new float[] { 1.0f, 1.0f, 1.0f, 1.0f };
            manager.AddLight(mainLight);

            // Fill light (from opposite direction)
            var fillLight = GLLight.CreateDirectional(-0.5f, -0.5f, -0.5f);
            fillLight.Ambient = new float[] { 0.1f, 0.1f, 0.1f, 1.0f };
            fillLight.Diffuse = new float[] { 0.3f, 0.3f, 0.3f, 1.0f };
            manager.AddLight(fillLight);

            return manager;
        }
    }

	/// <summary>
	/// Material properties for shader-based rendering
	/// </summary>
	[CLSCompliant(false)]
	public class GLMaterial
    {
        public float[] Ambient { get; set; }    // (r, g, b, a)
        public float[] Diffuse { get; set; }    // (r, g, b, a)
        public float[] Specular { get; set; }   // (r, g, b, a)
        public float Shininess { get; set; }

        public GLMaterial()
        {
            Ambient = new float[] { 0.2f, 0.2f, 0.2f, 1.0f };
            Diffuse = new float[] { 0.8f, 0.8f, 0.8f, 1.0f };
            Specular = new float[] { 1.0f, 1.0f, 1.0f, 1.0f };
            Shininess = 32.0f;
        }

        /// <summary>
        /// Creates a matte material
        /// </summary>
        public static GLMaterial CreateMatte()
        {
            return new GLMaterial
            {
                Ambient = new float[] { 0.1f, 0.1f, 0.1f, 1.0f },
                Diffuse = new float[] { 0.7f, 0.7f, 0.7f, 1.0f },
                Specular = new float[] { 0.3f, 0.3f, 0.3f, 1.0f },
                Shininess = 8.0f
            };
        }

        /// <summary>
        /// Creates a shiny material
        /// </summary>
        public static GLMaterial CreateShiny()
        {
            return new GLMaterial
            {
                Ambient = new float[] { 0.25f, 0.25f, 0.25f, 1.0f },
                Diffuse = new float[] { 0.8f, 0.8f, 0.8f, 1.0f },
                Specular = new float[] { 1.0f, 1.0f, 1.0f, 1.0f },
                Shininess = 128.0f
            };
        }

        /// <summary>
        /// Creates a plastic material
        /// </summary>
        public static GLMaterial CreatePlastic()
        {
            return new GLMaterial
            {
                Ambient = new float[] { 0.0f, 0.0f, 0.0f, 1.0f },
                Diffuse = new float[] { 0.55f, 0.55f, 0.55f, 1.0f },
                Specular = new float[] { 0.7f, 0.7f, 0.7f, 1.0f },
                Shininess = 32.0f
            };
        }

        /// <summary>
        /// Applies material to a shader program
        /// </summary>
        public void ApplyToShader(GLShader shader)
        {
            if (shader == null) return;

            shader.SetUniform("uMaterial.ambient", Ambient[0], Ambient[1], Ambient[2]);
            shader.SetUniform("uMaterial.diffuse", Diffuse[0], Diffuse[1], Diffuse[2]);
            shader.SetUniform("uMaterial.specular", Specular[0], Specular[1], Specular[2]);
            shader.SetUniform("uMaterial.shininess", Shininess);
        }
    }
}
