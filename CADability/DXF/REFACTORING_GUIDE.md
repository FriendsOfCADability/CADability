# ImportDxf and ExportDxf Refactoring Guide

This guide explains how to complete the refactoring of `ImportDxf.cs` and `ExportDxf.cs` to use the new abstraction layer.

## Key Changes Needed in ImportDxf.cs

### 1. Update all method signatures that take netDxf types

Replace all methods that take netDxf-specific types with the abstraction interfaces:

- `CreateLine(netDxf.Entities.Line)` ? `CreateLine(IDxfLine)`
- `CreateRay(netDxf.Entities.Ray)` ? `CreateRay(IDxfRay)`
- `CreateArc(netDxf.Entities.Arc)` ? `CreateArc(IDxfArc)`
- `CreateCircle(netDxf.Entities.Circle)` ? `CreateCircle(IDxfCircle)`
- `CreateEllipse(netDxf.Entities.Ellipse)` ? `CreateEllipse(IDxfEllipse)`
- `CreateSpline(netDxf.Entities.Spline)` ? `CreateSpline(IDxfSpline)`
- And so on for all entity types...

### 2. Replace all netDxf.Vector3 with tuples

Instead of:
```csharp
private static GeoPoint GeoPoint(netDxf.Vector3 p)
{
    return new GeoPoint(p.X, p.Y, p.Z);
}
```

Use tuples since IDxfEntity methods return tuples:
```csharp
private static GeoPoint GeoPoint((double X, double Y, double Z) p)
{
    return new GeoPoint(p.X, p.Y, p.Z);
}

private static GeoVector GeoVector((double X, double Y, double Z) p)
{
    return new GeoVector(p.X, p.Y, p.Z);
}
```

### 3. Update attribute handling

Replace:
```csharp
private void SetAttributes(IGeoObject go, netDxf.Entities.EntityObject entity)
{
    if (go is IColorDef cd) cd.ColorDef = FindOrCreateColor(entity.Color, entity.Layer);
    go.Layer = layerTable[entity.Layer];
    // ...
}
```

With:
```csharp
private void SetAttributes(IGeoObject go, IDxfEntity entity)
{
    if (go is IColorDef cd && entity.ColorArgb.HasValue)
        cd.ColorDef = FindOrCreateColor(entity.ColorArgb.Value);
    
    if (entity.LayerName != null && layerTable.TryGetValue(entity.LayerName, out var layer))
        go.Layer = layer;
    
    if (go is ILinePattern lp && entity.LineTypeName != null)
        lp.LinePattern = project.LinePatternList.Find(entity.LineTypeName);
    
    if (go is ILineWidth ld && entity.LineWeight.HasValue)
        ld.LineWidth = project.LineWidthList.CreateOrFind("DXF_" + entity.LineWeight.ToString(), entity.LineWeight.Value / 100.0);
}
```

### 4. Update SetUserData

Replace XData handling:
```csharp
private void SetUserData(IGeoObject go, IDxfEntity entity)
{
    foreach (var xdata in entity.XData)
    {
        ExtendedEntityData edata = new ExtendedEntityData();
        edata.ApplicationName = xdata.ApplicationName;
        
        string name = xdata.ApplicationName + ":" + xdata.ApplicationName;
        
        foreach (var record in xdata.Records)
        {
            edata.Data.Add(new KeyValuePair<XDataCode, object>((XDataCode)record.Code, record.Value));
        }
        
        go.UserData.Add(name, edata);
    }
    go.UserData["DxfImport.Handle"] = new UserInterface.StringProperty(entity.Handle, "DxfImport.Handle");
}
```

### 5. Update entity creation methods

Example for CreateLine:
```csharp
private IGeoObject CreateLine(IDxfLine line)
{
    GeoObject.Line l = GeoObject.Line.Construct();
    var sp = line.StartPoint;
    var ep = line.EndPoint;
    
    l.StartPoint = GeoPoint(sp);
    l.EndPoint = GeoPoint(ep);
    double th = line.Thickness;
    GeoVector no = GeoVector(line.Normal);
    
    if (th != 0.0 && !no.IsNullVector())
    {
        if (l.Length < Precision.eps)
        {
            l.EndPoint += th * no;
            return l;
        }
        else
        {
            return Make3D.Extrude(l, th * no, null);
        }
    }
    return l;
}
```

### 6. Color handling

Replace:
```csharp
private ColorDef FindOrCreateColor(AciColor color, netDxf.Tables.Layer layer)
```

With:
```csharp
private ColorDef FindOrCreateColor(int argb)
{
    Color rgb = Color.FromArgb(argb);
    if (rgb.ToArgb() == Color.White.ToArgb())
    {
        rgb = Color.Black;
    }
    string colorname = rgb.ToString();
    return project.ColorList.CreateOrFind(colorname, rgb);
}
```

## Key Changes Needed in ExportDxf.cs

### 1. Update ExportDxf constructor

Keep the netDxf document creation but make it library-agnostic where possible:

```csharp
public class Export
{
    private DxfDocument doc;
    private IDxfLibrary dxfLibrary;
    
    public Export(netDxf.Header.DxfVersion version)
    {
        doc = new DxfDocument(version);
        dxfLibrary = DxfLibraryFactory.GetLibrary();
    }
```

### 2. Update entity conversion

The export still uses netDxf internally (for now), but wrap created entities:

```csharp
private void SetAttributes(EntityObject entity, IGeoObject go)
{
    // Keep existing implementation for now
    // The netDxf layer is the export target
}
```

## Testing the Refactoring

1. Ensure ImportDxf can load DXF files with netDxf (default)
2. Add tests for layer/color/linetype mapping
3. Verify all entity types import correctly
4. Test with sample DXF files

## Next Steps for ACadSharp Support

1. Install ACadSharp NuGet package
2. Complete ACadSharpLibraryAdapter implementation
3. Map ACadSharp entity types to IDxfEntity interfaces
4. Add ExportDxf support for ACadSharp
5. Create configuration to switch between libraries

## Configuration

In Settings or Project properties, add:

```csharp
// Use netDxf (default)
DxfLibraryFactory.CurrentLibrary = DxfLibraryFactory.DxfLibraryType.NetDxf;

// Or use ACadSharp
DxfLibraryFactory.CurrentLibrary = DxfLibraryFactory.DxfLibraryType.ACadSharp;
```
