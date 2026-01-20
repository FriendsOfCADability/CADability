import re

# Read the file
with open('CADability/ImportDxf.cs', 'r') as f:
    content = f.read()

# 1. Fix GeoObjectFromEntity parameter and switch statement
content = re.sub(
    r'private IGeoObject GeoObjectFromEntity\(EntityObject item\)',
    r'private IGeoObject GeoObjectFromEntity(IDxfEntity item)',
    content
)

content = re.sub(
    r'switch \(item\)\s*\{',
    r'switch (item.EntityType)\n        {',
    content
)

# 2. Update all case statements in GeoObjectFromEntity
replacements = [
    (r'case netDxf\.Entities\.Line dxfLine: res = CreateLine\(dxfLine\);', r'case DxfEntityType.Line: res = CreateLine((IDxfLine)item);'),
    (r'case netDxf\.Entities\.Ray dxfRay: res = CreateRay\(dxfRay\);', r'case DxfEntityType.Ray: res = CreateRay((IDxfRay)item);'),
    (r'case netDxf\.Entities\.Arc dxfArc: res = CreateArc\(dxfArc\);', r'case DxfEntityType.Arc: res = CreateArc((IDxfArc)item);'),
    (r'case netDxf\.Entities\.Circle dxfCircle: res = CreateCircle\(dxfCircle\);', r'case DxfEntityType.Circle: res = CreateCircle((IDxfCircle)item);'),
    (r'case netDxf\.Entities\.Ellipse dxfEllipse: res = CreateEllipse\(dxfEllipse\);', r'case DxfEntityType.Ellipse: res = CreateEllipse((IDxfEllipse)item);'),
    (r'case netDxf\.Entities\.Spline dxfSpline: res = CreateSpline\(dxfSpline\);', r'case DxfEntityType.Spline: res = CreateSpline((IDxfSpline)item);'),
    (r'case netDxf\.Entities\.Face3D dxfFace: res = CreateFace\(dxfFace\);', r'case DxfEntityType.Face3D: res = CreateFace((IDxfFace3D)item);'),
    (r'case netDxf\.Entities\.PolyfaceMesh dxfPolyfaceMesh: res = CreatePolyfaceMesh\(dxfPolyfaceMesh\);', r'case DxfEntityType.PolyfaceMesh: res = CreatePolyfaceMesh((IDxfPolyfaceMesh)item);'),
    (r'case netDxf\.Entities\.Hatch dxfHatch: res = CreateHatch\(dxfHatch\);', r'case DxfEntityType.Hatch: res = CreateHatch((IDxfHatch)item);'),
    (r'case netDxf\.Entities\.Solid dxfSolid: res = CreateSolid\(dxfSolid\);', r'case DxfEntityType.Solid: res = CreateSolid((IDxfSolid)item);'),
    (r'case netDxf\.Entities\.Insert dxfInsert: res = CreateInsert\(dxfInsert\);', r'case DxfEntityType.Insert: res = CreateInsert((IDxfInsert)item);'),
    (r'case netDxf\.Entities\.Polyline2D dxfPolyline2D: res = CreatePolyline2D\(dxfPolyline2D\);', r'case DxfEntityType.Polyline2D: res = CreatePolyline2D((IDxfPolyline2D)item);'),
    (r'case netDxf\.Entities\.MLine dxfMLine: res = CreateMLine\(dxfMLine\);', r'case DxfEntityType.MLine: res = CreateMLine((IDxfMLine)item);'),
    (r'case netDxf\.Entities\.Text dxfText: res = CreateText\(dxfText\);', r'case DxfEntityType.Text: res = CreateText((IDxfText)item);'),
    (r'case netDxf\.Entities\.Dimension dxfDimension: res = CreateDimension\(dxfDimension\);', r'case DxfEntityType.Dimension: res = CreateDimension((IDxfDimension)item);'),
    (r'case netDxf\.Entities\.MText dxfMText: res = CreateMText\(dxfMText\);', r'case DxfEntityType.MText: res = CreateMText((IDxfMText)item);'),
    (r'case netDxf\.Entities\.Leader dxfLeader: res = CreateLeader\(dxfLeader\);', r'case DxfEntityType.Leader: res = CreateLeader((IDxfLeader)item);'),
    (r'case netDxf\.Entities\.Polyline3D dxfPolyline3D: res = CreatePolyline3D\(dxfPolyline3D\);', r'case DxfEntityType.Polyline3D: res = CreatePolyline3D((IDxfPolyline3D)item);'),
    (r'case netDxf\.Entities\.Point dxfPoint: res = CreatePoint\(dxfPoint\);', r'case DxfEntityType.Point: res = CreatePoint((IDxfPoint)item);'),
    (r'case netDxf\.Entities\.Mesh dxfMesh: res = CreateMesh\(dxfMesh\);', r'case DxfEntityType.Mesh: res = CreateMesh((IDxfMesh)item);'),
]

for old, new in replacements:
    content = re.sub(old, new, content)

# 3. Update GeoPoint and GeoVector helper methods
content = re.sub(
    r'private static GeoPoint GeoPoint\(Vector3 p\)',
    r'private static GeoPoint GeoPoint((double X, double Y, double Z) p)',
    content
)
content = re.sub(
    r'private static GeoVector GeoVector\(Vector3 p\)',
    r'private static GeoVector GeoVector((double X, double Y, double Z) p)',
    content
)
content = re.sub(
    r'internal static Plane Plane\(Vector3 center, Vector3 normal\)',
    r'internal static Plane Plane((double X, double Y, double Z) center, (double X, double Y, double Z) normal)',
    content
)

# 4. Update method signatures for entity creation methods
content = re.sub(r'private IGeoObject CreateLine\(netDxf\.Entities\.Line line\)', r'private IGeoObject CreateLine(IDxfLine line)', content)
content = re.sub(r'private IGeoObject CreateRay\(Ray ray\)', r'private IGeoObject CreateRay(IDxfRay ray)', content)
content = re.sub(r'private IGeoObject CreateArc\(Arc arc\)', r'private IGeoObject CreateArc(IDxfArc arc)', content)
content = re.sub(r'private IGeoObject CreateCircle\(netDxf\.Entities\.Circle circle\)', r'private IGeoObject CreateCircle(IDxfCircle circle)', content)
content = re.sub(r'private IGeoObject CreateEllipse\(netDxf\.Entities\.Ellipse ellipse\)', r'private IGeoObject CreateEllipse(IDxfEllipse ellipse)', content)
content = re.sub(r'private IGeoObject CreateSpline\(netDxf\.Entities\.Spline spline\)', r'private IGeoObject CreateSpline(IDxfSpline spline)', content)
content = re.sub(r'private IGeoObject CreateFace\(netDxf\.Entities\.Face3D face\)', r'private IGeoObject CreateFace(IDxfFace3D face)', content)
content = re.sub(r'private IGeoObject CreatePolyfaceMesh\(netDxf\.Entities\.PolyfaceMesh polyfacemesh\)', r'private IGeoObject CreatePolyfaceMesh(IDxfPolyfaceMesh polyfacemesh)', content)
content = re.sub(r'private IGeoObject CreateHatch\(netDxf\.Entities\.Hatch hatch\)', r'private IGeoObject CreateHatch(IDxfHatch hatch)', content)
content = re.sub(r'private IGeoObject CreateSolid\(netDxf\.Entities\.Solid solid\)', r'private IGeoObject CreateSolid(IDxfSolid solid)', content)
content = re.sub(r'private IGeoObject CreateInsert\(netDxf\.Entities\.Insert insert\)', r'private IGeoObject CreateInsert(IDxfInsert insert)', content)
content = re.sub(r'private IGeoObject CreatePolyline2D\(netDxf\.Entities\.Polyline2D polyline2D\)', r'private IGeoObject CreatePolyline2D(IDxfPolyline2D polyline2D)', content)
content = re.sub(r'private IGeoObject CreateMLine\(netDxf\.Entities\.MLine mLine\)', r'private IGeoObject CreateMLine(IDxfMLine mLine)', content)
content = re.sub(r'private IGeoObject CreateText\(netDxf\.Entities\.Text txt\)', r'private IGeoObject CreateText(IDxfText txt)', content)
content = re.sub(r'private IGeoObject CreateDimension\(netDxf\.Entities\.Dimension dimension\)', r'private IGeoObject CreateDimension(IDxfDimension dimension)', content)
content = re.sub(r'private IGeoObject CreateMText\(netDxf\.Entities\.MText mText\)', r'private IGeoObject CreateMText(IDxfMText mText)', content)
content = re.sub(r'private IGeoObject CreateLeader\(netDxf\.Entities\.Leader leader\)', r'private IGeoObject CreateLeader(IDxfLeader leader)', content)
content = re.sub(r'private IGeoObject CreatePolyline3D\(netDxf\.Entities\.Polyline3D polyline3D\)', r'private IGeoObject CreatePolyline3D(IDxfPolyline3D polyline3D)', content)
content = re.sub(r'private IGeoObject CreatePoint\(netDxf\.Entities\.Point point\)', r'private IGeoObject CreatePoint(IDxfPoint point)', content)
content = re.sub(r'private IGeoObject CreateMesh\(netDxf\.Entities\.Mesh mesh\)', r'private IGeoObject CreateMesh(IDxfMesh mesh)', content)

# Write the file back
with open('CADability/ImportDxf.cs', 'w') as f:
    f.write(content)

print("Phase 1 refactoring complete")
