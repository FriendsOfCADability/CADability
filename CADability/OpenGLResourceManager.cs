using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

namespace CADability
{
	/// <summary>
	/// Manages OpenGL resources (display lists, textures, buffers) to prevent leaks
	/// and provide better resource tracking
	/// </summary>
	[CLSCompliant(false)]
	public static class OpenGLResourceManager
	{
		private static readonly ConcurrentDictionary<int, ResourceInfo> displayLists = new ConcurrentDictionary<int, ResourceInfo>();
		private static readonly ConcurrentDictionary<uint, ResourceInfo> textures = new ConcurrentDictionary<uint, ResourceInfo>();
		private static readonly object statsLock = new object();
		private static long totalDisplayListsCreated = 0;
		private static long totalDisplayListsDeleted = 0;
		private static long totalTexturesCreated = 0;
		private static long totalTexturesDeleted = 0;

		/// <summary>
		/// Information about an OpenGL resource
		/// </summary>
		private class ResourceInfo
		{
			public string Name { get; set; }
			public DateTime CreatedAt { get; set; }
			public string CreatedBy { get; set; }
			public long MemorySize { get; set; }

			public ResourceInfo(string name, string createdBy, long memorySize = 0)
			{
				Name = name;
				CreatedAt = DateTime.Now;
				CreatedBy = createdBy;
				MemorySize = memorySize;
			}
		}

		/// <summary>
		/// Statistics about OpenGL resource usage
		/// </summary>
		public class ResourceStats
		{
			public int ActiveDisplayLists { get; set; }
			public int ActiveTextures { get; set; }
			public long TotalDisplayListsCreated { get; set; }
			public long TotalDisplayListsDeleted { get; set; }
			public long TotalTexturesCreated { get; set; }
			public long TotalTexturesDeleted { get; set; }
			public long EstimatedMemoryUsageBytes { get; set; }
		}

		/// <summary>
		/// Registers a display list creation
		/// </summary>
		public static void RegisterDisplayList(int listId, string name = null, string createdBy = null)
		{
			if (listId == 0) return;

			var info = new ResourceInfo(name ?? $"List_{listId}", createdBy ?? "Unknown", 0);
			if (displayLists.TryAdd(listId, info))
			{
				lock (statsLock)
				{
					totalDisplayListsCreated++;
				}
				OpenGLErrorHandler.LogDebug($"Display list registered: {listId} ({info.Name})");
			}
		}

		/// <summary>
		/// Registers a display list deletion
		/// </summary>
		public static void UnregisterDisplayList(int listId)
		{
			if (listId == 0) return;

			if (displayLists.TryRemove(listId, out ResourceInfo info))
			{
				lock (statsLock)
				{
					totalDisplayListsDeleted++;
				}
				OpenGLErrorHandler.LogDebug($"Display list unregistered: {listId} ({info.Name})");
			}
		}

		/// <summary>
		/// Registers a texture creation
		/// </summary>
		public static void RegisterTexture(uint textureId, string name = null, int width = 0, int height = 0, int bytesPerPixel = 4)
		{
			if (textureId == 0) return;

			long memorySize = (long)width * height * bytesPerPixel;
			var info = new ResourceInfo(name ?? $"Texture_{textureId}", "Unknown", memorySize);
			if (textures.TryAdd(textureId, info))
			{
				lock (statsLock)
				{
					totalTexturesCreated++;
				}
				OpenGLErrorHandler.LogDebug($"Texture registered: {textureId} ({info.Name}, {memorySize} bytes)");
			}
		}

		/// <summary>
		/// Registers a texture deletion
		/// </summary>
		public static void UnregisterTexture(uint textureId)
		{
			if (textureId == 0) return;

			if (textures.TryRemove(textureId, out ResourceInfo info))
			{
				lock (statsLock)
				{
					totalTexturesDeleted++;
				}
				OpenGLErrorHandler.LogDebug($"Texture unregistered: {textureId} ({info.Name})");
			}
		}

		/// <summary>
		/// Gets current resource statistics
		/// </summary>
		public static ResourceStats GetStatistics()
		{
			long estimatedMemory = 0;
			foreach (var texture in textures.Values)
			{
				estimatedMemory += texture.MemorySize;
			}

			lock (statsLock)
			{
				return new ResourceStats
				{
					ActiveDisplayLists = displayLists.Count,
					ActiveTextures = textures.Count,
					TotalDisplayListsCreated = totalDisplayListsCreated,
					TotalDisplayListsDeleted = totalDisplayListsDeleted,
					TotalTexturesCreated = totalTexturesCreated,
					TotalTexturesDeleted = totalTexturesDeleted,
					EstimatedMemoryUsageBytes = estimatedMemory
				};
			}
		}

		/// <summary>
		/// Logs current resource statistics
		/// </summary>
		public static void LogStatistics()
		{
			var stats = GetStatistics();
			OpenGLErrorHandler.LogInfo($"OpenGL Resource Statistics:");
			OpenGLErrorHandler.LogInfo($"  Display Lists: {stats.ActiveDisplayLists} active (created: {stats.TotalDisplayListsCreated}, deleted: {stats.TotalDisplayListsDeleted})");
			OpenGLErrorHandler.LogInfo($"  Textures: {stats.ActiveTextures} active (created: {stats.TotalTexturesCreated}, deleted: {stats.TotalTexturesDeleted})");
			OpenGLErrorHandler.LogInfo($"  Est. Memory: {stats.EstimatedMemoryUsageBytes / (1024 * 1024)} MB");
		}

		/// <summary>
		/// Detects potential resource leaks (resources older than threshold)
		/// </summary>
		public static List<string> DetectLeaks(TimeSpan threshold)
		{
			var leaks = new List<string>();
			var now = DateTime.Now;

			foreach (var kvp in displayLists)
			{
				if (now - kvp.Value.CreatedAt > threshold)
				{
					leaks.Add($"Display List {kvp.Key} ({kvp.Value.Name}) - Age: {(now - kvp.Value.CreatedAt).TotalMinutes:F1} minutes");
				}
			}

			foreach (var kvp in textures)
			{
				if (now - kvp.Value.CreatedAt > threshold)
				{
					leaks.Add($"Texture {kvp.Key} ({kvp.Value.Name}) - Age: {(now - kvp.Value.CreatedAt).TotalMinutes:F1} minutes");
				}
			}

			return leaks;
		}

		/// <summary>
		/// Clears all resource tracking (use with caution, should only be called when resetting OpenGL context)
		/// </summary>
		public static void Clear()
		{
			displayLists.Clear();
			textures.Clear();
			OpenGLErrorHandler.LogInfo("Resource manager cleared");
		}
	}
}
