using System;
using System.Collections.Generic;
using System.IO;

namespace CADability.DXF
{
    /// <summary>
    /// Abstract interface for DXF library operations. Allows switching between netDxf and ACadSharp.
    /// </summary>
    public interface IDxfLibrary
    {
        /// <summary>
        /// Gets the name of the DXF library implementation.
        /// </summary>
        string LibraryName { get; }

        /// <summary>
        /// Checks if the DXF file version is supported.
        /// </summary>
        bool CanImportVersion(string fileName);

        /// <summary>
        /// Loads a DXF document from a stream.
        /// </summary>
        IDxfDocument LoadFromStream(Stream stream);

        /// <summary>
        /// Loads a DXF document from a file.
        /// </summary>
        IDxfDocument LoadFromFile(string fileName);

        /// <summary>
        /// Creates a new DXF document for export.
        /// </summary>
        IDxfDocument CreateDocument();
    }

    /// <summary>
    /// Abstraction for DXF document operations.
    /// </summary>
    public interface IDxfDocument
    {
        /// <summary>
        /// Gets all blocks in the document.
        /// </summary>
        IDxfBlockCollection Blocks { get; }

        /// <summary>
        /// Gets all layers in the document.
        /// </summary>
        IEnumerable<IDxfLayer> Layers { get; }

        /// <summary>
        /// Gets all line types in the document.
        /// </summary>
        IEnumerable<IDxfLineType> LineTypes { get; }

        /// <summary>
        /// Gets or sets the document name.
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Saves the document to a file.
        /// </summary>
        void SaveToFile(string fileName);

        /// <summary>
        /// Saves the document to a stream.
        /// </summary>
        void SaveToStream(Stream stream);

        /// <summary>
        /// Gets all entities in the document.
        /// </summary>
        IEnumerable<IDxfEntity> Entities { get; }

        /// <summary>
        /// Adds an entity to the document.
        /// </summary>
        void AddEntity(IDxfEntity entity);

        /// <summary>
        /// Adds multiple entities to the document.
        /// </summary>
        void AddEntities(params IDxfEntity[] entities);
    }

    /// <summary>
    /// Abstraction for DXF block collection.
    /// </summary>
    public interface IDxfBlockCollection
    {
        /// <summary>
        /// Gets a block by name.
        /// </summary>
        IDxfBlock GetBlock(string name);

        /// <summary>
        /// Gets all entities in a block.
        /// </summary>
        IEnumerable<IDxfEntity> GetBlockEntities(string blockName);

        /// <summary>
        /// Adds a block to the document.
        /// </summary>
        void AddBlock(IDxfBlock block);
    }

    /// <summary>
    /// Abstraction for DXF block.
    /// </summary>
    public interface IDxfBlock
    {
        /// <summary>
        /// Gets the block name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the block origin point.
        /// </summary>
        (double X, double Y, double Z) Origin { get; }

        /// <summary>
        /// Gets all entities in the block.
        /// </summary>
        IEnumerable<IDxfEntity> Entities { get; }

        /// <summary>
        /// Gets the block handle.
        /// </summary>
        string Handle { get; }
    }

    /// <summary>
    /// Abstraction for DXF layer.
    /// </summary>
    public interface IDxfLayer
    {
        /// <summary>
        /// Gets the layer name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the layer color as ARGB.
        /// </summary>
        int ColorArgb { get; }

        /// <summary>
        /// Gets the line weight (in hundredths of mm).
        /// </summary>
        int LineWeight { get; }

        /// <summary>
        /// Gets the line type name.
        /// </summary>
        string LineTypeName { get; }
    }

    /// <summary>
    /// Abstraction for DXF line type.
    /// </summary>
    public interface IDxfLineType
    {
        /// <summary>
        /// Gets the line type name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the line pattern segments (positive = dash, negative = gap).
        /// </summary>
        double[] Segments { get; }
    }

    /// <summary>
    /// Abstraction for DXF entity.
    /// </summary>
    public interface IDxfEntity
    {
        /// <summary>
        /// Gets the entity type.
        /// </summary>
        DxfEntityType EntityType { get; }

        /// <summary>
        /// Gets or sets the layer name.
        /// </summary>
        string LayerName { get; set; }

        /// <summary>
        /// Gets or sets the color as ARGB.
        /// </summary>
        int? ColorArgb { get; set; }

        /// <summary>
        /// Gets or sets the line type name.
        /// </summary>
        string LineTypeName { get; set; }

        /// <summary>
        /// Gets or sets the line weight.
        /// </summary>
        int? LineWeight { get; set; }

        /// <summary>
        /// Gets the entity handle.
        /// </summary>
        string Handle { get; }

        /// <summary>
        /// Gets or sets the visibility.
        /// </summary>
        bool IsVisible { get; set; }

        /// <summary>
        /// Gets extended data if available.
        /// </summary>
        IEnumerable<IDxfXData> XData { get; }
    }

    /// <summary>
    /// Specific entity types.
    /// </summary>
    public interface IDxfLine : IDxfEntity
    {
        (double X, double Y, double Z) StartPoint { get; }
        (double X, double Y, double Z) EndPoint { get; }
        double Thickness { get; }
        (double X, double Y, double Z) Normal { get; }
    }

    public interface IDxfRay : IDxfEntity
    {
        (double X, double Y, double Z) Origin { get; }
        (double X, double Y, double Z) Direction { get; }
    }

    public interface IDxfArc : IDxfEntity
    {
        (double X, double Y, double Z) Center { get; }
        double Radius { get; }
        double StartAngle { get; }
        double EndAngle { get; }
        (double X, double Y, double Z) Normal { get; }
        double Thickness { get; }
    }

    public interface IDxfCircle : IDxfEntity
    {
        (double X, double Y, double Z) Center { get; }
        double Radius { get; }
        (double X, double Y, double Z) Normal { get; }
        double Thickness { get; }
    }

    public interface IDxfEllipse : IDxfEntity
    {
        (double X, double Y, double Z) Center { get; }
        (double X, double Y, double Z) MajorAxisEnd { get; }
        double MinorAxisRatio { get; }
        double StartAngle { get; }
        double EndAngle { get; }
        (double X, double Y, double Z) Normal { get; }
    }

    public interface IDxfSpline : IDxfEntity
    {
        int Degree { get; }
        (double X, double Y, double Z)[] ControlPoints { get; }
        double[] Weights { get; }
        double[] Knots { get; }
        IEnumerable<(double X, double Y, double Z)> FitPoints { get; }
        bool IsClosed { get; }
        bool IsClosedPeriodic { get; }
    }

    public interface IDxfPolyline2D : IDxfEntity
    {
        IEnumerable<IDxfVertex> Vertices { get; }
        bool IsClosed { get; }
        IEnumerable<IDxfEntity> Explode();
    }

    public interface IDxfVertex
    {
        (double X, double Y) Position { get; }
        double Bulge { get; }
    }

    public interface IDxfText : IDxfEntity
    {
        string Value { get; }
        (double X, double Y, double Z) Position { get; }
        double Height { get; }
        double Rotation { get; }
        double WidthFactor { get; }
        (double X, double Y, double Z) Normal { get; }
        string StyleName { get; }
        string FontName { get; }
        bool IsBold { get; }
        bool IsItalic { get; }
    }

    public interface IDxfMText : IDxfEntity
    {
        string PlainText { get; }
        (double X, double Y, double Z) Position { get; }
        double Height { get; }
        double Rotation { get; }
        (double X, double Y, double Z) Normal { get; }
        string StyleName { get; }
    }

    public interface IDxfHatch : IDxfEntity
    {
        IEnumerable<IDxfHatchBoundaryPath> BoundaryPaths { get; }
        IDxfHatchPattern Pattern { get; }
        (double X, double Y, double Z) Normal { get; }
    }

    public interface IDxfHatchBoundaryPath
    {
        IEnumerable<IDxfEntity> Edges { get; }
    }

    public interface IDxfHatchPattern
    {
        HatchFillType FillType { get; }
        IEnumerable<IDxfHatchLineDefinition> LineDefinitions { get; }
    }

    public interface IDxfHatchLineDefinition
    {
        double Angle { get; }
        (double X, double Y) Origin { get; }
        (double X, double Y) Delta { get; }
        double[] DashPattern { get; }
    }

    public interface IDxfInsert : IDxfEntity
    {
        string BlockName { get; }
        (double X, double Y, double Z) Position { get; }
        double Rotation { get; }
        (double X, double Y, double Z) Scale { get; }
        IDxfBlock Block { get; }
    }

    public interface IDxfFace3D : IDxfEntity
    {
        (double X, double Y, double Z) FirstVertex { get; }
        (double X, double Y, double Z) SecondVertex { get; }
        (double X, double Y, double Z) ThirdVertex { get; }
        (double X, double Y, double Z) FourthVertex { get; }
    }

    public interface IDxfPolyline3D : IDxfEntity
    {
        IEnumerable<(double X, double Y, double Z)> Vertices { get; }
        bool IsClosed { get; }
    }

    public interface IDxfSolid : IDxfEntity
    {
        (double X, double Y) FirstVertex { get; }
        (double X, double Y) SecondVertex { get; }
        (double X, double Y) ThirdVertex { get; }
        (double X, double Y) FourthVertex { get; }
        (double X, double Y, double Z) Normal { get; }
        double Elevation { get; }
    }

    public interface IDxfPoint : IDxfEntity
    {
        (double X, double Y, double Z) Position { get; }
    }

    public interface IDxfMLine : IDxfEntity
    {
        IEnumerable<IDxfEntity> Explode();
    }

    public interface IDxfPolyfaceMesh : IDxfEntity
    {
        (double X, double Y, double Z)[] Vertices { get; }
        IEnumerable<short[]> Faces { get; }
        void Explode();
    }

    public interface IDxfMesh : IDxfEntity
    {
        IEnumerable<(double X, double Y, double Z)> Vertices { get; }
        IEnumerable<int[]> Faces { get; }
    }

    public interface IDxfDimension : IDxfEntity
    {
        IDxfBlock DimensionBlock { get; }
    }

    public interface IDxfLeader : IDxfEntity
    {
        IEnumerable<(double X, double Y)> Vertices { get; }
        (double X, double Y, double Z) Normal { get; }
        double Elevation { get; }
        IDxfEntity Annotation { get; }
    }

    public interface IDxfXData
    {
        string ApplicationName { get; }
        IEnumerable<(int Code, object Value)> Records { get; }
    }

    /// <summary>
    /// Enumeration for DXF entity types.
    /// </summary>
    public enum DxfEntityType
    {
        Unknown,
        Line,
        Ray,
        Arc,
        Circle,
        Ellipse,
        Spline,
        Polyline2D,
        Polyline3D,
        Text,
        MText,
        Hatch,
        Insert,
        Face3D,
        PolyfaceMesh,
        Solid,
        Point,
        MLine,
        Mesh,
        Dimension,
        Leader,
        Attribute,
        AttributeDefinition
    }

    public enum HatchFillType
    {
        SolidFill,
        PatternFill,
        GradientFill
    }
}
