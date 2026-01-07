using Microsoft.VisualStudio.TestTools.UnitTesting;
using CADability;
using System;

namespace CADability.Tests
{
    /// <summary>
    /// Tests for OpenGL error handling and logging infrastructure
    /// </summary>
    [TestClass]
    public class OpenGLErrorHandlerTests
    {
        [TestInitialize]
        public void Setup()
        {
            // Reset error handler state before each test
            OpenGLErrorHandler.CurrentLogLevel = OpenGLErrorHandler.LogLevel.Error;
            OpenGLErrorHandler.EnableErrorChecking = true;
        }

        [TestMethod]
        public void GetErrorString_ReturnsCorrectDescriptionForKnownErrors()
        {
            // Test known error codes
            Assert.IsTrue(OpenGLErrorHandler.GetErrorString(Gl.GL_NO_ERROR).Contains("No error"));
            Assert.IsTrue(OpenGLErrorHandler.GetErrorString(Gl.GL_INVALID_ENUM).Contains("Invalid enum"));
            Assert.IsTrue(OpenGLErrorHandler.GetErrorString(Gl.GL_INVALID_VALUE).Contains("Invalid value"));
            Assert.IsTrue(OpenGLErrorHandler.GetErrorString(Gl.GL_INVALID_OPERATION).Contains("Invalid operation"));
            Assert.IsTrue(OpenGLErrorHandler.GetErrorString(Gl.GL_OUT_OF_MEMORY).Contains("Out of memory"));
        }

        [TestMethod]
        public void GetErrorString_ReturnsUnknownForInvalidCode()
        {
            string result = OpenGLErrorHandler.GetErrorString(0xDEADBEEF);
            Assert.IsTrue(result.Contains("Unknown"));
        }

        [TestMethod]
        public void LogLevel_CanBeSetAndRetrieved()
        {
            OpenGLErrorHandler.CurrentLogLevel = OpenGLErrorHandler.LogLevel.Debug;
            Assert.AreEqual(OpenGLErrorHandler.LogLevel.Debug, OpenGLErrorHandler.CurrentLogLevel);

            OpenGLErrorHandler.CurrentLogLevel = OpenGLErrorHandler.LogLevel.None;
            Assert.AreEqual(OpenGLErrorHandler.LogLevel.None, OpenGLErrorHandler.CurrentLogLevel);
        }

        [TestMethod]
        public void EnableErrorChecking_CanBeSetAndRetrieved()
        {
            OpenGLErrorHandler.EnableErrorChecking = false;
            Assert.IsFalse(OpenGLErrorHandler.EnableErrorChecking);

            OpenGLErrorHandler.EnableErrorChecking = true;
            Assert.IsTrue(OpenGLErrorHandler.EnableErrorChecking);
        }

        [TestMethod]
        public void ClearErrors_DoesNotThrowException()
        {
            // Should not throw even if OpenGL is not initialized
            // (will just call glGetError which returns 0 when no context)
            try
            {
                OpenGLErrorHandler.ClearErrors();
                Assert.IsTrue(true); // If we get here, no exception was thrown
            }
            catch
            {
                Assert.Fail("ClearErrors should not throw exception");
            }
        }

        [TestMethod]
        public void ExecuteWithErrorCheck_ExecutesActionSuccessfully()
        {
            bool actionExecuted = false;
            
            OpenGLErrorHandler.ExecuteWithErrorCheck(() => 
            {
                actionExecuted = true;
            }, "Test operation");

            Assert.IsTrue(actionExecuted);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void ExecuteWithErrorCheck_PropagatesException()
        {
            OpenGLErrorHandler.ExecuteWithErrorCheck(() => 
            {
                throw new InvalidOperationException("Test exception");
            }, "Test operation");
        }

        [TestMethod]
        public void LogMethods_DoNotThrowExceptions()
        {
            // Set to highest log level to test all log methods
            OpenGLErrorHandler.CurrentLogLevel = OpenGLErrorHandler.LogLevel.Debug;

            try
            {
                OpenGLErrorHandler.LogError("Test error");
                OpenGLErrorHandler.LogWarning("Test warning");
                OpenGLErrorHandler.LogInfo("Test info");
                OpenGLErrorHandler.LogDebug("Test debug");
                Assert.IsTrue(true);
            }
            catch
            {
                Assert.Fail("Log methods should not throw exceptions");
            }
        }

        [TestMethod]
        public void CheckError_WithNoOpenGLContext_DoesNotThrow()
        {
            // When no OpenGL context exists, glGetError returns 0
            // CheckError should handle this gracefully
            try
            {
                bool hasError = OpenGLErrorHandler.CheckError("Test operation");
                // We expect false since there's no OpenGL error (or no context)
                Assert.IsFalse(hasError);
            }
            catch
            {
                Assert.Fail("CheckError should not throw when no OpenGL context exists");
            }
        }
    }
}
