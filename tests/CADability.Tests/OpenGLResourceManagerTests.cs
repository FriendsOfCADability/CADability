using Microsoft.VisualStudio.TestTools.UnitTesting;
using CADability;
using System;
using System.Threading;

namespace CADability.Tests
{
    /// <summary>
    /// Tests for OpenGL resource management and tracking
    /// </summary>
    [TestClass]
    public class OpenGLResourceManagerTests
    {
        [TestInitialize]
        public void Setup()
        {
            // Clear resource manager before each test
            OpenGLResourceManager.Clear();
        }

        [TestMethod]
        public void RegisterDisplayList_IncreasesActiveCount()
        {
            OpenGLResourceManager.RegisterDisplayList(1, "TestList");
            var stats = OpenGLResourceManager.GetStatistics();
            
            Assert.AreEqual(1, stats.ActiveDisplayLists);
            Assert.AreEqual(1, stats.TotalDisplayListsCreated);
        }

        [TestMethod]
        public void UnregisterDisplayList_DecreasesActiveCount()
        {
            OpenGLResourceManager.RegisterDisplayList(1, "TestList");
            OpenGLResourceManager.UnregisterDisplayList(1);
            var stats = OpenGLResourceManager.GetStatistics();
            
            Assert.AreEqual(0, stats.ActiveDisplayLists);
            Assert.AreEqual(1, stats.TotalDisplayListsDeleted);
        }

        [TestMethod]
        public void RegisterMultipleDisplayLists_TracksCorrectly()
        {
            OpenGLResourceManager.RegisterDisplayList(1, "List1");
            OpenGLResourceManager.RegisterDisplayList(2, "List2");
            OpenGLResourceManager.RegisterDisplayList(3, "List3");
            
            var stats = OpenGLResourceManager.GetStatistics();
            Assert.AreEqual(3, stats.ActiveDisplayLists);
            Assert.AreEqual(3, stats.TotalDisplayListsCreated);
        }

        [TestMethod]
        public void RegisterTexture_IncreasesActiveCount()
        {
            OpenGLResourceManager.RegisterTexture(1, "TestTexture", 256, 256, 4);
            var stats = OpenGLResourceManager.GetStatistics();
            
            Assert.AreEqual(1, stats.ActiveTextures);
            Assert.AreEqual(1, stats.TotalTexturesCreated);
        }

        [TestMethod]
        public void RegisterTexture_CalculatesMemoryUsage()
        {
            // 256x256 texture with 4 bytes per pixel = 262,144 bytes
            OpenGLResourceManager.RegisterTexture(1, "TestTexture", 256, 256, 4);
            var stats = OpenGLResourceManager.GetStatistics();
            
            Assert.AreEqual(256 * 256 * 4, stats.EstimatedMemoryUsageBytes);
        }

        [TestMethod]
        public void UnregisterTexture_DecreasesActiveCount()
        {
            OpenGLResourceManager.RegisterTexture(1, "TestTexture", 256, 256, 4);
            OpenGLResourceManager.UnregisterTexture(1);
            var stats = OpenGLResourceManager.GetStatistics();
            
            Assert.AreEqual(0, stats.ActiveTextures);
            Assert.AreEqual(1, stats.TotalTexturesDeleted);
        }

        [TestMethod]
        public void RegisterMultipleTextures_TracksMemoryCorrectly()
        {
            OpenGLResourceManager.RegisterTexture(1, "Tex1", 256, 256, 4);  // 262,144 bytes
            OpenGLResourceManager.RegisterTexture(2, "Tex2", 512, 512, 4);  // 1,048,576 bytes
            
            var stats = OpenGLResourceManager.GetStatistics();
            long expectedMemory = (256 * 256 * 4) + (512 * 512 * 4);
            
            Assert.AreEqual(2, stats.ActiveTextures);
            Assert.AreEqual(expectedMemory, stats.EstimatedMemoryUsageBytes);
        }

        [TestMethod]
        public void DetectLeaks_FindsOldResources()
        {
            OpenGLResourceManager.RegisterDisplayList(1, "OldList");
            
            // Wait a short time
            Thread.Sleep(100);
            
            // Check for leaks with a very short threshold
            var leaks = OpenGLResourceManager.DetectLeaks(TimeSpan.FromMilliseconds(50));
            
            // Should detect the list as a potential leak
            Assert.IsTrue(leaks.Count > 0);
            Assert.IsTrue(leaks[0].Contains("Display List 1"));
        }

        [TestMethod]
        public void DetectLeaks_DoesNotFindNewResources()
        {
            OpenGLResourceManager.RegisterDisplayList(1, "NewList");
            
            // Check immediately with a longer threshold
            var leaks = OpenGLResourceManager.DetectLeaks(TimeSpan.FromMinutes(5));
            
            // Should not detect any leaks
            Assert.AreEqual(0, leaks.Count);
        }

        [TestMethod]
        public void Clear_RemovesAllTracking()
        {
            OpenGLResourceManager.RegisterDisplayList(1, "List1");
            OpenGLResourceManager.RegisterDisplayList(2, "List2");
            OpenGLResourceManager.RegisterTexture(1, "Tex1", 256, 256, 4);
            
            OpenGLResourceManager.Clear();
            
            var stats = OpenGLResourceManager.GetStatistics();
            Assert.AreEqual(0, stats.ActiveDisplayLists);
            Assert.AreEqual(0, stats.ActiveTextures);
        }

        [TestMethod]
        public void RegisterDisplayList_WithZeroId_DoesNotTrack()
        {
            OpenGLResourceManager.RegisterDisplayList(0, "InvalidList");
            var stats = OpenGLResourceManager.GetStatistics();
            
            Assert.AreEqual(0, stats.ActiveDisplayLists);
        }

        [TestMethod]
        public void RegisterTexture_WithZeroId_DoesNotTrack()
        {
            OpenGLResourceManager.RegisterTexture(0, "InvalidTexture", 256, 256, 4);
            var stats = OpenGLResourceManager.GetStatistics();
            
            Assert.AreEqual(0, stats.ActiveTextures);
        }

        [TestMethod]
        public void UnregisterDisplayList_ThatDoesNotExist_DoesNotThrow()
        {
            try
            {
                OpenGLResourceManager.UnregisterDisplayList(999);
                var stats = OpenGLResourceManager.GetStatistics();
                Assert.AreEqual(0, stats.TotalDisplayListsDeleted);
            }
            catch
            {
                Assert.Fail("Should not throw when unregistering non-existent list");
            }
        }

        [TestMethod]
        public void LogStatistics_DoesNotThrow()
        {
            OpenGLResourceManager.RegisterDisplayList(1, "TestList");
            OpenGLResourceManager.RegisterTexture(1, "TestTexture", 256, 256, 4);
            
            try
            {
                OpenGLResourceManager.LogStatistics();
                Assert.IsTrue(true);
            }
            catch
            {
                Assert.Fail("LogStatistics should not throw");
            }
        }

        [TestMethod]
        public void GetStatistics_ReturnsConsistentData()
        {
            // Register some resources
            OpenGLResourceManager.RegisterDisplayList(1, "List1");
            OpenGLResourceManager.RegisterDisplayList(2, "List2");
            OpenGLResourceManager.UnregisterDisplayList(1);
            
            OpenGLResourceManager.RegisterTexture(1, "Tex1", 256, 256, 4);
            OpenGLResourceManager.RegisterTexture(2, "Tex2", 512, 512, 4);
            OpenGLResourceManager.UnregisterTexture(1);
            
            var stats = OpenGLResourceManager.GetStatistics();
            
            // Verify consistency
            Assert.AreEqual(1, stats.ActiveDisplayLists); // 2 created, 1 deleted
            Assert.AreEqual(2, stats.TotalDisplayListsCreated);
            Assert.AreEqual(1, stats.TotalDisplayListsDeleted);
            
            Assert.AreEqual(1, stats.ActiveTextures); // 2 created, 1 deleted
            Assert.AreEqual(2, stats.TotalTexturesCreated);
            Assert.AreEqual(1, stats.TotalTexturesDeleted);
            
            // Memory should only count active texture (512x512x4)
            Assert.AreEqual(512 * 512 * 4, stats.EstimatedMemoryUsageBytes);
        }
    }
}
