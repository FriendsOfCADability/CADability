using System;

namespace CADability.DXF
{
    /// <summary>
    /// Configuration and settings for DXF library selection and behavior.
    /// </summary>
    public static class DxfSettings
    {
        /// <summary>
        /// Gets or sets the preferred DXF library for import operations.
        /// Default: NetDxf
        /// </summary>
        public static DxfLibraryFactory.DxfLibraryType PreferredImportLibrary { get; set; } = 
            DxfLibraryFactory.DxfLibraryType.NetDxf;

        /// <summary>
        /// Gets or sets the preferred DXF library for export operations.
        /// Default: NetDxf
        /// </summary>
        public static DxfLibraryFactory.DxfLibraryType PreferredExportLibrary { get; set; } = 
            DxfLibraryFactory.DxfLibraryType.NetDxf;

        /// <summary>
        /// Gets or sets whether to automatically switch libraries if the preferred one fails to load a file.
        /// When enabled, if the preferred library cannot load a DXF file, the system will attempt to use an alternative library.
        /// Default: true
        /// </summary>
        public static bool AllowLibraryFallback { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable library-specific optimizations.
        /// Some libraries may have optimizations that can improve performance for specific operations.
        /// Default: true
        /// </summary>
        public static bool EnableLibraryOptimizations { get; set; } = true;

        /// <summary>
        /// Initializes DXF settings with default values.
        /// Call this method if you want to reset settings to defaults.
        /// </summary>
        public static void ResetToDefaults()
        {
            PreferredImportLibrary = DxfLibraryFactory.DxfLibraryType.NetDxf;
            PreferredExportLibrary = DxfLibraryFactory.DxfLibraryType.NetDxf;
            AllowLibraryFallback = true;
            EnableLibraryOptimizations = true;
            DxfLibraryFactory.ClearCache();
        }

        /// <summary>
        /// Configures the DXF system to use a specific library for all operations.
        /// </summary>
        /// <param name="library">The library to use for all operations.</param>
        public static void UseLibraryForAllOperations(DxfLibraryFactory.DxfLibraryType library)
        {
            PreferredImportLibrary = library;
            PreferredExportLibrary = library;
            DxfLibraryFactory.CurrentLibrary = library;
            DxfLibraryFactory.ClearCache();
        }

        /// <summary>
        /// Gets information about the current DXF library being used.
        /// </summary>
        /// <returns>A string containing library name and version information.</returns>
        public static string GetLibraryInfo()
        {
            return $"DXF Import Library: {DxfLibraryFactory.GetLibrary(PreferredImportLibrary).LibraryName}" +
                   $", DXF Export Library: {DxfLibraryFactory.GetLibrary(PreferredExportLibrary).LibraryName}";
        }
    }
}
