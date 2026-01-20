# ACadSharp Adapter Implementation Guide

## Overview

The ACadSharp library support has been scaffolded into the abstraction layer. The abstraction is **complete and functional with netDxf**. This guide provides detailed steps to complete the ACadSharp adapter implementation.

## Current Status

- ? **Abstraction Layer**: Complete (`IDxfLibrary.cs`)
- ? **netDxf Adapter**: Fully implemented and tested
- ? **Factory Pattern**: Ready for runtime library switching
- ? **Configuration System**: Available (`DxfSettings.cs`)
- ?? **ACadSharp Adapter**: Skeleton implementation (requires completion)

## Why ACadSharp Implementation is Incomplete

The ACadSharp library has a significantly different API structure than netDxf:

1. **Async/Await Pattern**: ACadSharp uses async I/O operations
2. **Different Entity Structure**: Entity hierarchy differs from netDxf
3. **Alternative Naming**: Property and class names differ
4. **Collection Types**: BlockRecords, Tables, etc. have different APIs

Due to these differences, a direct mapping would require significant research into the actual ACadSharp API at runtime.

## Getting Started

### Step 1: Install and Explore ACadSharp

```bash
# ACadSharp is already installed (3.3.23)
# Now explore the API:
```

### Step 2: Create a Test Project

Create a simple console app to explore ACadSharp:

```csharp
using ACadSharp;
using System;
using System.IO;

class Program
{
    static async Task Main(string[] args)
    {
        // Load a DXF file
        var doc = await CadDocument.ReadAsync("test.dxf");
        
        // Explore document structure
        Console.WriteLine($"Document: {doc.Header}");
        Console.WriteLine($"Entities: {doc.Entities.Count}");
        Console.WriteLine($"Blocks: {doc.BlockRecords.Count}");
        Console.WriteLine($"Layers: {doc.Layers.Count}");
    }
}
```

### Step 3: Map ACadSharp Types

Explore and document:

```csharp
// Key types to investigate
- ACadSharp.CadDocument
  - ReadAsync(string path)
  - ReadAsync(Stream stream)
  - Save(string path)
  - Save(Stream stream)
  - Header : CadHeader
  - Entities : EntityCollection
  - Blocks : BlockRecordsTable
  - Layers : LayersTable
  - LineTypes : LineTypesTable

- ACadSharp.Entities.Entity (base class)
  - All entity types derived from this

- ACadSharp.Tables.Layer
- ACadSharp.Tables.LineType
- ACadSharp.Tables.BlockRecord
- ACadSharp.Color
```

## Implementation Roadmap

### Phase 1: Core Infrastructure (Priority: HIGH)

1. **Document Adapter** (`ACadSharpDocumentAdapter`)
   - Wrap `CadDocument`
   - Implement `LoadAsync` ? synchronous wrapper
   - Map to `IDxfDocument` interface

2. **Basic Collections**
   - `ACadSharpBlockCollectionAdapter` ? `IDxfBlockCollection`
   - Layer iteration ? `IDxfLayer` collection
   - LineType iteration ? `IDxfLineType` collection

### Phase 2: Entity Adapters (Priority: HIGH)

Implement adapters for core entities:

1. **Basic Entities** (easiest)
   - `Line` ? `IDxfLine`
   - `Circle` ? `IDxfCircle`
   - `Arc` ? `IDxfArc`
   - `Point` ? `IDxfPoint`

2. **Text Entities**
   - `Text` ? `IDxfText`
   - `MText` ? `IDxfMText`

3. **Complex Entities**
   - `Polyline` ? `IDxfPolyline2D` or `IDxfPolyline3D`
   - `Spline` ? `IDxfSpline`
   - `Ellipse` ? `IDxfEllipse`
   - `Hatch` ? `IDxfHatch`
   - `Insert` ? `IDxfInsert`

### Phase 3: Advanced Features (Priority: MEDIUM)

1. Dimension support
2. Leader support
3. XData/Extended entity data
4. Complex hatch patterns

## Implementation Template

### Document Adapter

```csharp
internal class ACadSharpDocumentAdapter : IDxfDocument
{
    private readonly CadDocument document;

    public ACadSharpDocumentAdapter(CadDocument doc)
    {
        this.document = doc ?? throw new ArgumentNullException(nameof(doc));
    }

    public IDxfBlockCollection Blocks => new ACadSharpBlockCollectionAdapter(document.BlockRecords);

    public IEnumerable<IDxfLayer> Layers
    {
        get
        {
            var result = new List<IDxfLayer>();
            foreach (var layer in document.Layers)
            {
                result.Add(new ACadSharpLayerAdapter(layer));
            }
            return result;
        }
    }

    // ... implement other members
}
```

### Entity Adapter Example

```csharp
internal class ACadSharpLineAdapter : ACadSharpEntityAdapter, IDxfLine
{
    private readonly Line line;

    public ACadSharpLineAdapter(Line line) : base(line)
    {
        this.line = line;
    }

    public override DxfEntityType EntityType => DxfEntityType.Line;
    
    public (double X, double Y, double Z) StartPoint 
    {
        get
        {
            var pt = line.StartPoint;
            return (pt.X, pt.Y, pt.Z);
        }
    }

    // ... implement other members
}
```

## Key Challenges & Solutions

### Challenge 1: Async/Await Pattern

**Problem**: ACadSharp uses async I/O, but our interface is synchronous

**Solution**:
```csharp
public IDxfDocument LoadFromStream(Stream stream)
{
    try
    {
        var doc = CadDocument.ReadAsync(stream).Result; // Sync wrapper
        return new ACadSharpDocumentAdapter(doc);
    }
    catch (AggregateException ex)
    {
        throw new InvalidOperationException("Failed to load with ACadSharp", ex.InnerException);
    }
}
```

### Challenge 2: API Differences

**Problem**: ACadSharp might use `Document.Read()` instead of `CadDocument.Load()`

**Solution**: 
- Check ACadSharp source code
- Use reflection if needed to discover methods
- Create wrapper methods

### Challenge 3: Property Name Mapping

**Problem**: ACadSharp might use `Vertex` instead of `Vertexes`

**Solution**:
- Document all property names
- Create mapping table
- Implement graceful fallbacks

## Testing Strategy

### Unit Tests to Create

```csharp
[TestClass]
public class ACadSharpAdapterTests
{
    [TestMethod]
    public void CanLoadDxfFile()
    {
        var adapter = new ACadSharpLibraryAdapter();
        var doc = adapter.LoadFromFile("test.dxf");
        Assert.IsNotNull(doc);
    }

    [TestMethod]
    public void LayersAreMapped()
    {
        var adapter = new ACadSharpLibraryAdapter();
        var doc = adapter.LoadFromFile("test.dxf");
        var layers = doc.Layers.ToList();
        Assert.IsTrue(layers.Count > 0);
    }

    [TestMethod]
    public void EntitiesAreWrapped()
    {
        var adapter = new ACadSharpLibraryAdapter();
        var doc = adapter.LoadFromFile("test.dxf");
        var entities = doc.Entities.ToList();
        Assert.IsTrue(entities.All(e => e is IDxfEntity));
    }
}
```

## Performance Considerations

1. **Caching**: Layer adapters and entity wrappers can be cached
2. **Lazy Loading**: Defer entity wrapping until accessed
3. **Async Integration**: Consider if ACadSharp's async should be leveraged

## Integration Points

Once ACadSharp adapter is complete:

1. Update `DxfLibraryFactory` to support ACadSharp selection
2. Update `DxfSettings` to configure import/export libraries
3. Update `Import` class to use factory
4. Update `Project.ReadFromFile` to route to appropriate library

## Resources

- **ACadSharp GitHub**: https://github.com/DomCR/ACadSharp
- **ACadSharp Documentation**: https://github.com/DomCR/ACadSharp/wiki
- **Reference Implementation**: `CADability/DXF/Adapters/NetDxfLibraryAdapter.cs`
- **Abstraction Interface**: `CADability/DXF/IDxfLibrary.cs`

## Summary

The infrastructure is ready. Completing the ACadSharp adapter involves:

1. ? **Already Done**: Abstraction layer and factory pattern
2. ? **Next**: Explore and document ACadSharp API
3. ? **Then**: Implement entity adapters following the netDxf adapter pattern
4. ? **Finally**: Test and integrate

**Estimated Effort**: 40-60 hours for complete implementation with testing

**Breaking Point**: Once you understand ACadSharp's actual API structure, implementation follows the same pattern as netDxf adapter.
