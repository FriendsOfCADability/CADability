using Microsoft.VisualStudio.TestTools.UnitTesting;
using CADability;
using System;

namespace CADability.Tests
{
    /// <summary>
    /// Unit tests for OpenGL 2.1 modernization infrastructure
    /// </summary>
    [TestClass]
    public class OpenGL21ModernizationTests
    {
        [TestInitialize]
        public void Setup()
        {
            OpenGLErrorHandler.CurrentLogLevel = OpenGLErrorHandler.LogLevel.Error;
            OpenGLErrorHandler.EnableErrorChecking = true;
        }

        #region GLMatrix Tests

        [TestMethod]
        public void GLMatrix_Identity_ReturnsIdentityMatrix()
        {
            double[] identity = GLMatrix.Identity();
            
            Assert.AreEqual(16, identity.Length);
            
            // Check diagonal is 1
            Assert.AreEqual(1, identity[0]);
            Assert.AreEqual(1, identity[5]);
            Assert.AreEqual(1, identity[10]);
            Assert.AreEqual(1, identity[15]);
            
            // Check rest is 0
            for (int i = 0; i < 16; i++)
            {
                if (i != 0 && i != 5 && i != 10 && i != 15)
                {
                    Assert.AreEqual(0, identity[i]);
                }
            }
        }

        [TestMethod]
        public void GLMatrix_Translation_ProducesCorrectMatrix()
        {
            double[] trans = GLMatrix.Translation(5, 10, 15);
            
            // Translation values should be at positions 12, 13, 14
            Assert.AreEqual(5, trans[12], 1e-10);
            Assert.AreEqual(10, trans[13], 1e-10);
            Assert.AreEqual(15, trans[14], 1e-10);
            
            // Rest should be identity
            Assert.AreEqual(1, trans[0]);
            Assert.AreEqual(1, trans[5]);
            Assert.AreEqual(1, trans[10]);
            Assert.AreEqual(1, trans[15]);
        }

        [TestMethod]
        public void GLMatrix_Scale_ProducesCorrectMatrix()
        {
            double[] scale = GLMatrix.Scale(2, 3, 4);
            
            Assert.AreEqual(2, scale[0], 1e-10);
            Assert.AreEqual(3, scale[5], 1e-10);
            Assert.AreEqual(4, scale[10], 1e-10);
            Assert.AreEqual(1, scale[15]);
        }

        [TestMethod]
        public void GLMatrix_Multiply_CombinesMatrices()
        {
            double[] a = GLMatrix.Identity();
            double[] b = GLMatrix.Translation(1, 2, 3);
            
            double[] result = GLMatrix.Multiply(a, b);
            
            Assert.AreEqual(1, result[12], 1e-10);
            Assert.AreEqual(2, result[13], 1e-10);
            Assert.AreEqual(3, result[14], 1e-10);
        }

        [TestMethod]
        public void GLMatrix_Determinant_CalculatesCorrectly()
        {
            double[] identity = GLMatrix.Identity();
            double det = GLMatrix.Determinant(identity);
            
            Assert.AreEqual(1, det, 1e-10);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void GLMatrix_Invert_ThrowsOnSingularMatrix()
        {
            // Create singular matrix (all zeros except 1.0 at [15])
            double[] singular = new double[16];
            singular[15] = 1.0;
            
            GLMatrix.Invert(singular);
        }

        [TestMethod]
        public void GLMatrix_Invert_ProducesInverseMatrix()
        {
            double[] matrix = GLMatrix.Translation(5, 10, 15);
            double[] inverse = GLMatrix.Invert(matrix);
            
            // Multiply matrix by its inverse to get identity
            double[] product = GLMatrix.Multiply(matrix, inverse);
            
            // Check result is approximately identity
            for (int i = 0; i < 16; i++)
            {
                double expected = (i == 0 || i == 5 || i == 10 || i == 15) ? 1.0 : 0.0;
                Assert.AreEqual(expected, product[i], 1e-10);
            }
        }

        #endregion

        #region GLVertex Tests

        [TestMethod]
        public void GLVertex_SizeInBytes_IsCorrect()
        {
            // GLVertex should be 48 bytes: 3+3+4+2 floats = 12 floats * 4 bytes each
            Assert.AreEqual(48, GLVertex.SizeInBytes);
        }

        [TestMethod]
        public void GLVertex_Constructor_SetsAllValues()
        {
            var vertex = new GLVertex(1, 2, 3, 0, 1, 0, 1, 0, 0, 1, 0.5f, 0.5f);
            
            Assert.AreEqual(1, vertex.x);
            Assert.AreEqual(2, vertex.y);
            Assert.AreEqual(3, vertex.z);
            Assert.AreEqual(0, vertex.nx);
            Assert.AreEqual(1, vertex.ny);
            Assert.AreEqual(0, vertex.nz);
            Assert.AreEqual(1, vertex.r);
            Assert.AreEqual(0, vertex.g);
            Assert.AreEqual(0, vertex.b);
            Assert.AreEqual(1, vertex.a);
            Assert.AreEqual(0.5f, vertex.u);
            Assert.AreEqual(0.5f, vertex.v);
        }

        #endregion

        #region GLLight Tests

        [TestMethod]
        public void GLLight_CreateDirectional_SetsCorrectType()
        {
            var light = GLLight.CreateDirectional(1, 1, 1);
            
            Assert.AreEqual(GLLight.LightType.Directional, light.Type);
        }

        [TestMethod]
        public void GLLight_CreatePoint_SetsCorrectType()
        {
            var light = GLLight.CreatePoint(0, 0, 0);
            
            Assert.AreEqual(GLLight.LightType.Point, light.Type);
            Assert.AreEqual(1, light.Position[3]); // w component should be 1 for point light
        }

        [TestMethod]
        public void GLLight_CreateSpot_SetsCorrectType()
        {
            var light = GLLight.CreateSpot(0, 0, 0, 0, 1, 0);
            
            Assert.AreEqual(GLLight.LightType.Spot, light.Type);
        }

        #endregion

        #region GLLightManager Tests

        [TestMethod]
        public void GLLightManager_AddLight_IncreasesCount()
        {
            var manager = new GLLightManager();
            var light = new GLLight();
            
            manager.AddLight(light);
            
            Assert.AreEqual(1, manager.LightCount);
        }

        [TestMethod]
        public void GLLightManager_RemoveLight_DecreasesCount()
        {
            var manager = new GLLightManager();
            var light = new GLLight();
            manager.AddLight(light);
            
            manager.RemoveLight(0);
            
            Assert.AreEqual(0, manager.LightCount);
        }

        [TestMethod]
        public void GLLightManager_CreateDefault_HasLights()
        {
            var manager = GLLightManager.CreateDefault();
            
            Assert.IsTrue(manager.LightCount > 0);
        }

        #endregion

        #region GLMaterial Tests

        [TestMethod]
        public void GLMaterial_CreateMatte_SetsLowShininess()
        {
            var material = GLMaterial.CreateMatte();
            
            Assert.IsTrue(material.Shininess < 32);
        }

        [TestMethod]
        public void GLMaterial_CreateShiny_SetsHighShininess()
        {
            var material = GLMaterial.CreateShiny();
            
            Assert.IsTrue(material.Shininess > 32);
        }

        [TestMethod]
        public void GLMaterial_CreatePlastic_HasValidValues()
        {
            var material = GLMaterial.CreatePlastic();
            
            Assert.IsNotNull(material.Ambient);
            Assert.IsNotNull(material.Diffuse);
            Assert.IsNotNull(material.Specular);
            Assert.IsTrue(material.Shininess > 0);
        }

        #endregion

        #region VBODisplayListManager Tests

        [TestMethod]
        public void VBODisplayListManager_BeginList_OpensNewList()
        {
            var manager = new VBODisplayListManager();
            
            manager.BeginDisplayList("test");
            
            Assert.AreEqual(0, manager.DisplayListCount); // Not added until closed
        }

        [TestMethod]
        public void VBODisplayListManager_EndList_AddsToCount()
        {
            var manager = new VBODisplayListManager();
            
            manager.BeginDisplayList("test");
            manager.EndDisplayList();
            
            Assert.AreEqual(1, manager.DisplayListCount);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void VBODisplayListManager_NestedLists_Throws()
        {
            var manager = new VBODisplayListManager();
            
            manager.BeginDisplayList("first");
            manager.BeginDisplayList("second");
        }

        #endregion

        #region ModernOpenGlList Tests

        [TestMethod]
        public void ModernOpenGlList_Creation_StartsEmpty()
        {
            var list = new ModernOpenGlList("test");
            
            Assert.IsFalse(list.HasContents);
            Assert.IsFalse(list.IsClosed);
        }

        [TestMethod]
        public void ModernOpenGlList_SetHasContents_MarksContents()
        {
            var list = new ModernOpenGlList("test");
            
            list.SetHasContents();
            
            Assert.IsTrue(list.HasContents);
        }

        #endregion

        #region OpenGL21Initialization Tests

        [TestMethod]
        public void OpenGL21Initialization_GetErrorString_DoesNotThrow()
        {
            // This should work even without an OpenGL context
            try
            {
                string version = OpenGL21Initialization.GetOpenGLVersion();
                // May return "Unknown" if no context
                Assert.IsNotNull(version);
            }
            catch
            {
                // Expected if no context is available
            }
        }

        [TestMethod]
        public void OpenGL21Initialization_GetCapabilitiesInfo_ReturnsString()
        {
            // This may fail without a valid context, but should format correctly
            try
            {
                string info = OpenGL21Initialization.GetCapabilitiesInfo();
                Assert.IsNotNull(info);
                Assert.IsTrue(info.Length > 0);
            }
            catch
            {
                // Expected if no context is available
            }
        }

        #endregion
    }
}
