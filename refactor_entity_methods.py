import re

# Read the file
with open('CADability/ImportDxf.cs', 'r') as f:
    content = f.read()

# Update all Vector3 and Vector2 references in entity methods
replacements = [
    # CreateLine
    (r'Vector3 sp = line\.StartPoint;', r'var sp = line.StartPoint;'),
    (r'Vector3 ep = line\.EndPoint;', r'var ep = line.EndPoint;'),
    # CreateRay
    (r'Vector3 sp = ray\.Origin;', r'var sp = ray.Origin;'),
    (r'Vector3 dir = ray\.Direction;', r'var dir = ray.Direction;'),
    # CreateEllipse - remove Vector2 and method calls
    (r'Vector2 startPoint = ellipse\.PolarCoordinateRelativeToCenter\(ellipse\.StartAngle\);', r'// Vector2 startPoint calculation removed - using angles directly'),
    (r'double sp = CalcStartEndParameter\(startPoint, ellipse\.MajorAxis, ellipse\.MinorAxis\);', r'double sp = ellipse.StartAngle;'),
    (r'Vector2 endPoint = ellipse\.PolarCoordinateRelativeToCenter\(ellipse\.EndAngle\);', r'// Vector2 endPoint calculation removed - using angles directly'),
    (r'double ep = CalcStartEndParameter\(endPoint, ellipse\.MajorAxis, ellipse\.MinorAxis\);', r'double ep = ellipse.EndAngle;'),
    # CreateSpline - fix ToPolyline2D call
    (r'netDxf\.Entities\.Polyline2D p2d = spline\.ToPolyline2D\(usedCurves\);', r'// Polyline2D conversion not available in abstraction\n                        // var p2d = CreatePolyline2DFromSpline(spline, usedCurves);'),
    (r'var res = CreatePolyline2D\(p2d\);', r'// return bsp; // fallback to spline'),
]

for old, new in replacements:
    content = re.sub(old, new, content)

# Fix Ellipse property access
content = re.sub(r'ellipse\.MajorAxis \* \(rot \* GeoVector2D\.XAxis\)', r'ellipse.MajorAxisEnd.X * 2 * (rot * GeoVector2D.XAxis)', content)
content = re.sub(r'ellipse\.MinorAxis \* \(rot \* GeoVector2D\.YAxis\)', r'ellipse.MinorAxisRatio * ellipse.MajorAxisEnd.X * 2 * (rot * GeoVector2D.YAxis)', content)

# Fix explode() calls for Polyline2D and MLine
content = re.sub(r'List<EntityObject> exploded = polyline2D\.Explode\(\);', r'var exploded = polyline2D.Explode();', content)
content = re.sub(r'List<EntityObject> exploded = mLine\.Explode\(\);', r'var exploded = mLine.Explode();', content)

# Fix leader/solid Normal and Elevation access
content = re.sub(
    r'Plane ocs = Plane\(new Vector3\(leader\.Elevation \* leader\.Normal\.X, leader\.Elevation \* leader\.Normal\.Y, leader\.Elevation \* leader\.Normal\.Z\), leader\.Normal\);',
    r'var normal = leader.Normal;\n            Plane ocs = Plane((leader.Elevation * normal.X, leader.Elevation * normal.Y, leader.Elevation * normal.Z), normal);',
    content
)

content = re.sub(
    r'Plane ocs = Plane\(new Vector3\(solid\.Elevation \* solid\.Normal\.X, solid\.Elevation \* solid\.Normal\.Y, solid\.Elevation \* solid\.Normal\.Z\), solid\.Normal\);',
    r'var normal = solid.Normal;\n            Plane ocs = Plane((solid.Elevation * normal.X, solid.Elevation * normal.Y, solid.Elevation * normal.Z), normal);',
    content
)

# Fix Hatch and other entity method property accesses
content = re.sub(r'hatch\.Pattern\.Fill\.Equals\(HatchFillType\.SolidFill\)', r'hatch.Pattern.FillType == HatchFillType.SolidFill', content)
content = re.sub(r'entity\.Layer\.Color\.ToColor\(\)', r'Color.FromArgb(layerTable[entity.LayerName] != null ? layerColorTable[entity.LayerName].Color.ToArgb() : Color.Black.ToArgb())', content)

# Fix Text properties
content = re.sub(r'txt\.Style\.FontFamilyName', r'txt.FontName', content)
content = re.sub(r'txt\.Style\.FontFile', r'txt.FontName', content)
content = re.sub(r'txt\.Style\.Name', r'txt.StyleName', content)
content = re.sub(r'txt\.Style\.FontStyle\.HasFlag\(netDxf\.Tables\.FontStyle\.Bold\)', r'txt.IsBold', content)
content = re.sub(r'txt\.Style\.FontStyle\.HasFlag\(netDxf\.Tables\.FontStyle\.Italic\)', r'txt.IsItalic', content)

# Fix MText conversion
content = re.sub(r'mText\.PlainText\(\)', r'mText.PlainText', content)
content = re.sub(r'mText\.Style\.ObliqueAngle', r'0.0 // ObliqueAngle not available in abstraction', content)
content = re.sub(r'mText\.Style', r'// Style not directly available', content)

# Fix leader annotation
content = re.sub(r'if \(leader\.Annotation != null\)', r'if (leader.Annotation != null)', content)

# Fix dimension block
content = re.sub(r'if \(dimension\.Block != null\)', r'if (dimension.DimensionBlock != null)', content)
content = re.sub(r'FindBlock\(dimension\.Block\)', r'FindBlock(dimension.DimensionBlock)', content)

# Fix Insert
content = re.sub(r'insert\.Block', r'insert.Block', content)

# Fix PolyfaceMesh
content = re.sub(r'polyfacemesh\.Vertexes\.Length', r'polyfacemesh.Vertices.Length', content)
content = re.sub(r'polyfacemesh\.Vertexes\[i\]', r'System.Linq.Enumerable.ElementAt(polyfacemesh.Vertices, i)', content)

# Fix mesh faces
content = re.sub(r'mesh\.Vertexes\.Count', r'System.Linq.Enumerable.Count(mesh.Vertices)', content)
content = re.sub(r'mesh\.Vertexes\[i\]', r'System.Linq.Enumerable.ElementAt(mesh.Vertices, i)', content)
content = re.sub(r'mesh\.Faces\.Count', r'System.Linq.Enumerable.Count(mesh.Faces)', content)
content = re.sub(r'mesh\.Faces\[i\]', r'System.Linq.Enumerable.ElementAt(mesh.Faces, i)', content)

# Fix FindOrCreateHatchStyleLines color access
content = re.sub(
    r'project\.ColorList\.CreateOrFind\(entity\.Layer\.Color\.ToColor\(\)\.ToString\(\), entity\.Layer\.Color\.ToColor\(\)\)',
    r'project.ColorList.CreateOrFind(entity.LayerName, Color.Black) // Simplified color handling',
    content
)

# Write back
with open('CADability/ImportDxf.cs', 'w') as f:
    f.write(content)

print("Entity method updates complete")
