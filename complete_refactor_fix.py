#!/usr/bin/env python3
import re

with open('CADability/ImportDxf.cs', 'r') as f:
    content = f.read()

# Fix CreateEllipse - update to use abstraction properties
# The abstraction provides MajorAxisEnd (a point, not a length) and MinorAxisRatio
ellipse_fix = r'''        private IGeoObject CreateEllipse(IDxfEllipse ellipse)
        {
            GeoObject.Ellipse e = GeoObject.Ellipse.Construct();
            Plane plane = Plane(ellipse.Center, ellipse.Normal);
            
            // Calculate major and minor axis from the abstraction properties
            GeoVector majorAxisVec = GeoVector(ellipse.MajorAxisEnd);
            double majorAxisLength = majorAxisVec.Length;
            double minorAxisLength = majorAxisLength * ellipse.MinorAxisRatio;
            
            GeoVector2D majorAxis2D = plane.Project(majorAxisVec).ToVector();
            if (majorAxis2D.IsNullVector()) majorAxis2D = GeoVector2D.XAxis * (majorAxisLength / 2);
            else majorAxis2D.Length = majorAxisLength / 2;
            
            GeoVector2D minorAxis2D = majorAxis2D.ToLeft();
            minorAxis2D.Length = minorAxisLength / 2;
            
            e.SetEllipseCenterAxis(GeoPoint(ellipse.Center), plane.ToGlobal(majorAxis2D), plane.ToGlobal(minorAxis2D));

            e.StartParameter = ellipse.StartAngle;
            e.SweepParameter = ellipse.EndAngle - ellipse.StartAngle;
            if (e.SweepParameter == 0.0) e.SweepParameter = Math.PI * 2.0;
            if (e.SweepParameter < 0.0) e.SweepParameter += Math.PI * 2.0;
            return e;
        }'''

content = re.sub(
    r'private IGeoObject CreateEllipse\(IDxfEllipse ellipse\).*?return e;\s*\}',
    ellipse_fix,
    content,
    flags=re.DOTALL
)

# Remove CalcStartEndParameter method as it's no longer needed
content = re.sub(
    r'private double CalcStartEndParameter\(Vector2 startEndPoint, double majorAxis, double minorAxis\).*?\n        \}',
    '',
    content,
    flags=re.DOTALL
)

# Fix CreateSpline - handle the ToPolyline2D issue
spline_topolyline = r'''                    // Polyline2D conversion not available in abstraction layer
                        // Fallback to returning the BSpline
                        return bsp;'''

content = re.sub(
    r'// Polyline2D conversion not available in abstraction.*?// return bsp; // fallback to spline',
    spline_topolyline,
    content,
    flags=re.DOTALL
)

# Fix CreatePolyline2D and CreateMLine explode
content = re.sub(
    r'var exploded = polyline2D\.Explode\(\);\s*List<IGeoObject> path = new List<IGeoObject>\(\);',
    r'List<IGeoObject> path = new List<IGeoObject>();\n            foreach (var item in polyline2D.Explode())\n            {',
    content
)

# Add closing brace for foreach
content = re.sub(
    r'(\s+if \(ent != null\) path\.Add\(ent\);\s+\})\s+(GeoObject\.Path go = GeoObject\.Path\.Construct\(\);)',
    r'\1\n            }\n            \2',
    content
)

# Same fix for MLine
content = re.sub(
    r'var exploded = mLine\.Explode\(\);\s*List<IGeoObject> path = new List<IGeoObject>\(\);',
    r'List<IGeoObject> path = new List<IGeoObject>();\n            foreach (var item in mLine.Explode())\n            {',
    content
)

# Fix CreateLeader vertex access
leader_fix = r'''        private IGeoObject CreateLeader(IDxfLeader leader)
        {
            var normal = leader.Normal;
            Plane ocs = Plane((leader.Elevation * normal.X, leader.Elevation * normal.Y, leader.Elevation * normal.Z), normal);
            GeoObject.Block blk = GeoObject.Block.Construct();
            blk.Name = "Leader:" + leader.Handle;
            if (leader.Annotation != null)
            {
                IGeoObject annotation = GeoObjectFromEntity(leader.Annotation);
                if (annotation != null) blk.Add(annotation);
            }
            var vertices = leader.Vertices.ToArray();
            GeoPoint[] vtx = new GeoPoint[vertices.Length];
            for (int i = 0; i < vtx.Length; i++)
            {
                vtx[i] = ocs.ToGlobal(new GeoPoint2D(vertices[i].X, vertices[i].Y));
            }
            GeoObject.Polyline pln = GeoObject.Polyline.Construct();
            pln.SetPoints(vtx, false);
            blk.Add(pln);
            return blk;
        }'''

content = re.sub(
    r'private IGeoObject CreateLeader\(IDxfLeader leader\).*?return blk;\s*\}',
    leader_fix,
    content,
    flags=re.DOTALL
)

# Fix CreateSolid similarly
solid_fix = r'''        private IGeoObject CreateSolid(IDxfSolid solid)
        {
            var normal = solid.Normal;
            Plane ocs = Plane((solid.Elevation * normal.X, solid.Elevation * normal.Y, solid.Elevation * normal.Z), normal);

            Color clr = Color.Black; // Default color
            HatchStyleSolid hst = FindOrCreateSolidHatchStyle(clr);
            List<GeoPoint> points = new List<GeoPoint>();
            points.Add(ocs.ToGlobal(new GeoPoint2D(solid.FirstVertex.X, solid.FirstVertex.Y)));
            points.Add(ocs.ToGlobal(new GeoPoint2D(solid.SecondVertex.X, solid.SecondVertex.Y)));
            points.Add(ocs.ToGlobal(new GeoPoint2D(solid.ThirdVertex.X, solid.ThirdVertex.Y)));
            points.Add(ocs.ToGlobal(new GeoPoint2D(solid.FourthVertex.X, solid.FourthVertex.Y)));
            for (int i = 3; i > 0; --i)
            {
                for (int j = 0; j < i; ++j)
                {
                    if (Precision.IsEqual(points[j], points[i]))
                    {
                        points.RemoveAt(i);
                        break;
                    }
                }
            }
            if (points.Count < 3) return null;

            Plane pln;
            try
            {
                pln = new Plane(points[0], points[1], points[2]);
            }
            catch (PlaneException)
            {
                return null;
            }
            GeoPoint2D[] vertex = new GeoPoint2D[points.Count + 1];
            for (int i = 0; i < points.Count; ++i) vertex[i] = pln.Project(points[i]);
            vertex[points.Count] = vertex[0];
            Curve2D.Polyline2D poly2d = new Curve2D.Polyline2D(vertex);
            Border bdr = new Border(poly2d);
            CompoundShape cs = new CompoundShape(new SimpleShape(bdr));
            GeoObject.Hatch hatch = GeoObject.Hatch.Construct();
            hatch.CompoundShape = cs;
            hatch.HatchStyle = hst;
            hatch.Plane = pln;
            return hatch;
        }'''

content = re.sub(
    r'private IGeoObject CreateSolid\(IDxfSolid solid\).*?return hatch;\s*\}',
    solid_fix,
    content,
    flags=re.DOTALL
)

# Fix CreatePolyfaceMesh
polymesh_fix = r'''        private IGeoObject CreatePolyfaceMesh(IDxfPolyfaceMesh polyfacemesh)
        {
            polyfacemesh.Explode();

            var vertexArray = polyfacemesh.Vertices;
            GeoPoint[] vertices = new GeoPoint[vertexArray.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = GeoPoint(vertexArray[i]);
            }

            List<Face> faces = new List<Face>();
            foreach (short[] indices in polyfacemesh.Faces)
            {
                for (int j = 0; j < indices.Length; j++)
                {
                    indices[j] = (short)(Math.Abs(indices[j]) - 1);
                }
                if (indices.Length <= 3 || indices[3] == indices[2])
                {
                    if (indices[0] != indices[1] && indices[1] != indices[2])
                    {
                        Plane pln = new Plane(vertices[indices[0]], vertices[indices[1]], vertices[indices[2]]);
                        PlaneSurface surf = new PlaneSurface(pln);
                        Border bdr = new Border(new GeoPoint2D[] { new GeoPoint2D(0.0, 0.0), pln.Project(vertices[indices[1]]), pln.Project(vertices[indices[2]]) });
                        SimpleShape ss = new SimpleShape(bdr);
                        Face fc = Face.MakeFace(surf, ss);
                        faces.Add(fc);
                    }
                }
                else
                {
                    if (indices[0] != indices[1] && indices[1] != indices[2])
                    {
                        Plane pln = new Plane(vertices[indices[0]], vertices[indices[1]], vertices[indices[2]]);
                        PlaneSurface surf = new PlaneSurface(pln);
                        Border bdr = new Border(new GeoPoint2D[] { new GeoPoint2D(0.0, 0.0), pln.Project(vertices[indices[1]]), pln.Project(vertices[indices[2]]) });
                        SimpleShape ss = new SimpleShape(bdr);
                        Face fc = Face.MakeFace(surf, ss);
                        faces.Add(fc);
                    }
                    if (indices[2] != indices[3] && indices[3] != indices[0])
                    {
                        Plane pln = new Plane(vertices[indices[2]], vertices[indices[3]], vertices[indices[0]]);
                        PlaneSurface surf = new PlaneSurface(pln);
                        Border bdr = new Border(new GeoPoint2D[] { new GeoPoint2D(0.0, 0.0), pln.Project(vertices[indices[3]]), pln.Project(vertices[indices[0]]) });
                        SimpleShape ss = new SimpleShape(bdr);
                        Face fc = Face.MakeFace(surf, ss);
                        faces.Add(fc);
                    }
                }
            }
            if (faces.Count > 1)
            {
                GeoObjectList sewed = Make3D.SewFacesAndShells(new GeoObjectList(faces.ToArray() as IGeoObject[]));
                return sewed[0];
            }
            else if (faces.Count == 1)
            {
                return faces[0];
            }
            else return null;
        }'''

content = re.sub(
    r'private IGeoObject CreatePolyfaceMesh\(IDxfPolyfaceMesh polyfacemesh\).*?else return null;\s*\}',
    polymesh_fix,
    content,
    flags=re.DOTALL
)

# Fix CreateMesh similarly
mesh_fix = r'''        private IGeoObject CreateMesh(IDxfMesh mesh)
        {
            var vertexList = mesh.Vertices.ToArray();
            GeoPoint[] vertices = new GeoPoint[vertexList.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = GeoPoint(vertexList[i]);
            }
            List<Face> faces = new List<Face>();
            foreach (int[] indices in mesh.Faces)
            {
                if (indices.Length <= 3 || indices[3] == indices[2])
                {
                    if (indices[0] != indices[1] && indices[1] != indices[2])
                    {
                        try
                        {
                            Plane pln = new Plane(vertices[indices[0]], vertices[indices[1]], vertices[indices[2]]);
                            PlaneSurface surf = new PlaneSurface(pln);
                            Border bdr = new Border(new GeoPoint2D[] { new GeoPoint2D(0.0, 0.0), pln.Project(vertices[indices[1]]), pln.Project(vertices[indices[2]]) });
                            SimpleShape ss = new SimpleShape(bdr);
                            Face fc = Face.MakeFace(surf, ss);
                            faces.Add(fc);
                        }
                        catch { };
                    }
                }
                else
                {
                    if (indices[0] != indices[1] && indices[1] != indices[2])
                    {
                        try
                        {
                            Plane pln = new Plane(vertices[indices[0]], vertices[indices[1]], vertices[indices[2]]);
                            PlaneSurface surf = new PlaneSurface(pln);
                            Border bdr = new Border(new GeoPoint2D[] { new GeoPoint2D(0.0, 0.0), pln.Project(vertices[indices[1]]), pln.Project(vertices[indices[2]]) });
                            SimpleShape ss = new SimpleShape(bdr);
                            Face fc = Face.MakeFace(surf, ss);
                            faces.Add(fc);
                        }
                        catch { };
                    }
                    if (indices[2] != indices[3] && indices[3] != indices[0])
                    {
                        try
                        {
                            Plane pln = new Plane(vertices[indices[2]], vertices[indices[3]], vertices[indices[0]]);
                            PlaneSurface surf = new PlaneSurface(pln);
                            Border bdr = new Border(new GeoPoint2D[] { new GeoPoint2D(0.0, 0.0), pln.Project(vertices[indices[3]]), pln.Project(vertices[indices[0]]) });
                            SimpleShape ss = new SimpleShape(bdr);
                            Face fc = Face.MakeFace(surf, ss);
                            faces.Add(fc);
                        }
                        catch { };
                    }
                }
            }
            if (faces.Count > 1)
            {
                GeoObjectList sewed = Make3D.SewFacesAndShells(new GeoObjectList(faces.ToArray() as IGeoObject[]));
                if (sewed.Count == 1) return sewed[0];
                else
                {
                    GeoObject.Block blk = GeoObject.Block.Construct();
                    blk.Name = "Mesh";
                    blk.Set(new GeoObjectList(faces as ICollection<IGeoObject>));
                    return blk;
                }
            }
            else if (faces.Count == 1)
            {
                return faces[0];
            }
            else return null;
        }'''

content = re.sub(
    r'private IGeoObject CreateMesh\(IDxfMesh mesh\).*?else return null;\s*\}',
    mesh_fix,
    content,
    flags=re.DOTALL
)

# Fix CreatePolyline3D
polyline3d_fix = r'''        private IGeoObject CreatePolyline3D(IDxfPolyline3D polyline3D)
        {
            GeoObject.Polyline res = GeoObject.Polyline.Construct();
            foreach (var vertex in polyline3D.Vertices)
            {
                res.AddPoint(GeoPoint(vertex));
            }
            res.IsClosed = polyline3D.IsClosed;
            if (res.GetExtent(0.0).Size < 1e-6) return null;
            return res;
        }'''

content = re.sub(
    r'private IGeoObject CreatePolyline3D\(IDxfPolyline3D polyline3D\).*?return null;\s*\}',
    polyline3d_fix,
    content,
    flags=re.DOTALL
)

with open('CADability/ImportDxf.cs', 'w') as f:
    f.write(content)

print("Complete refactor fixes applied")
