using System;
using System.Collections.Generic;

namespace CADability.DXF
{
    /// <summary>
    /// Factory for creating DXF library implementations and managing library selection.
    /// </summary>
    public static class DxfLibraryFactory
    {
        /// <summary>
        /// Available DXF library implementations.
        /// </summary>
        public enum DxfLibraryType
        {
            NetDxf,
            ACadSharp
        }

        private static DxfLibraryType currentLibrary = DxfLibraryType.NetDxf;
        private static Dictionary<DxfLibraryType, IDxfLibrary> libraryCache = new Dictionary<DxfLibraryType, IDxfLibrary>();

        /// <summary>
        /// Gets or sets the current DXF library implementation to use.
        /// </summary>
        public static DxfLibraryType CurrentLibrary
        {
            get { return currentLibrary; }
            set { currentLibrary = value; }
        }

        /// <summary>
        /// Gets the current DXF library implementation instance.
        /// </summary>
        public static IDxfLibrary GetLibrary()
        {
            if (!libraryCache.TryGetValue(currentLibrary, out IDxfLibrary library))
            {
                library = CreateLibrary(currentLibrary);
                libraryCache[currentLibrary] = library;
            }
            return library;
        }

        /// <summary>
        /// Gets a specific DXF library implementation instance.
        /// </summary>
        public static IDxfLibrary GetLibrary(DxfLibraryType libraryType)
        {
            if (!libraryCache.TryGetValue(libraryType, out IDxfLibrary library))
            {
                library = CreateLibrary(libraryType);
                libraryCache[libraryType] = library;
            }
            return library;
        }

        /// <summary>
        /// Clears the library cache (useful for testing or when switching libraries).
        /// </summary>
        public static void ClearCache()
        {
            libraryCache.Clear();
        }

        private static IDxfLibrary CreateLibrary(DxfLibraryType libraryType)
        {
            if (libraryType == DxfLibraryType.NetDxf)
                return new Adapters.NetDxfLibraryAdapter();
            else if (libraryType == DxfLibraryType.ACadSharp)
                return new Adapters.ACadSharpLibraryAdapter();
            else
                throw new NotSupportedException($"DXF library type {libraryType} is not supported");
        }
    }
}
