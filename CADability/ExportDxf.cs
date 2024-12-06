using CADability.Attribute;
using CADability.GeoObject;
using MathNet.Numerics.Statistics.Mcmc;
using netDxf;
using netDxf.Blocks;
using netDxf.Entities;
using netDxf.Tables;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.WebSockets;
using System.Text;

namespace CADability.DXF
{
    public class Export
    {
        private DxfDocument doc;
        private Dictionary<CADability.Attribute.Layer, netDxf.Tables.Layer> createdLayers;
        Dictionary<CADability.Attribute.LinePattern, netDxf.Tables.Linetype> createdLinePatterns;
        int anonymousBlockCounter = 0;
        double triangulationPrecision = 0.1;
        public Export(netDxf.Header.DxfVersion version)
        {
            doc = new DxfDocument(version);
            createdLayers = new Dictionary<Attribute.Layer, netDxf.Tables.Layer>();
            createdLinePatterns = new Dictionary<LinePattern, Linetype>();
        }
        public byte[] WriteToByteArray(Project toExport)
        {
            var memoryStream = new System.IO.MemoryStream();
            Model modelSpace = null;
            if (toExport.GetModelCount() == 1) modelSpace = toExport.GetModel(0);
            else
            {
                modelSpace = toExport.FindModel("*Model_Space");
            }
            if (modelSpace == null) modelSpace = toExport.GetActiveModel();
            for (int i = 0; i < modelSpace.Count; i++)
            {
                EntityObject[] entity = GeoObjectToEntity(modelSpace[i]);
                if (entity != null) doc.Entities.Add(entity);
            }
            doc.Save(memoryStream);
            return memoryStream.ToArray();
        }

        public void WriteToFile(Project toExport, string filename)
        {
            Model modelSpace = null;
            if (toExport.GetModelCount() == 1) modelSpace = toExport.GetModel(0);
            else
            {
                modelSpace = toExport.FindModel("*Model_Space");
            }
            if (modelSpace == null) modelSpace = toExport.GetActiveModel();
            GeoObjectList geoObjects = new GeoObjectList();
            List<Face> faces = new List<Face>(); // all top level faces collected
            for (int i = 0; i < modelSpace.Count; i++)
            {
                if (modelSpace[i] is Face face) faces.Add(face.Clone() as Face); // Clone() to keep ownership
                else geoObjects.Add(modelSpace[i]);
            }
            if (faces.Count > 0) geoObjects.Add(Shell.FromFaces(faces.ToArray())); // this shell is only to combine same color faces to a single mesh
            for (int i = 0; i < geoObjects.Count; i++)
            {
                EntityObject[] entity = GeoObjectToEntity(geoObjects[i]);
                if (entity != null) doc.Entities.Add(entity);
            }
            doc.Save(filename);
        }

        private EntityObject[] GeoObjectToEntity(IGeoObject geoObject)
        {
            EntityObject entity = null;
            EntityObject[] entities = null;
            switch (geoObject)
            {
                case GeoObject.Point point: entity = ExportPoint(point); break;
                case GeoObject.Line line: entity = ExportLine(line); break;
                case GeoObject.Ellipse elli: entity = ExportEllipse(elli); break;
                case GeoObject.Polyline polyline: entity = ExportPolyline(polyline); break;
                case GeoObject.BSpline bspline: entity = ExportBSpline(bspline); break;
                case GeoObject.Path path:
                    if (Settings.GlobalSettings.GetBoolValue("DxfExport.ExportPathsAsBlocks", true))
                    {
                        entity = ExportPath(path);
                    }
                    else
                    {
                        entities = ExportPathWithoutBlock(path);
                    }
                    break;
                case GeoObject.Text text: entity = ExportText(text); break;
                case GeoObject.Block block: entity = ExportBlock(block); break;
                case GeoObject.Face face: entity = ExportFace(face); break;
                case GeoObject.Shell shell: entities = ExportShell(shell); break;
                case GeoObject.Solid solid: entities = ExportShell(solid.Shells[0]); break;
            }
            if (entity != null)
            {
                SetAttributes(entity, geoObject);
                SetUserData(entity, geoObject);
                return new EntityObject[] { entity };
            }
            if (entities != null)
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    if (geoObject.Layer == null)
                    {
                        entities[i].Layer = netDxf.Tables.Layer.Default;
                        continue;
                    }
                    if (!createdLayers.TryGetValue(geoObject.Layer, out netDxf.Tables.Layer layer))
                    {
                        layer = new netDxf.Tables.Layer(geoObject.Layer.Name);
                        doc.Layers.Add(layer);
                        createdLayers[geoObject.Layer] = layer;
                    }
                    entities[i].Layer = layer;
                }
                return entities;
            }
            return null;
        }

        private void SetUserData(netDxf.Entities.EntityObject entity, IGeoObject go)
        {
            if (entity is null || go is null || go.UserData is null || go.UserData.Count == 0)
                return;

            foreach (KeyValuePair<string, object> de in go.UserData)
            {
                if (de.Value is ExtendedEntityData xData)
                {
                    ApplicationRegistry registry = new ApplicationRegistry(xData.ApplicationName);
                    XData data = new XData(registry);

                    foreach (var item in xData.Data)
                    {
                        XDataCode code = item.Key;
                        object newValue = null;
                        //Make the export more robust to wrong XDataCode entries.
                        //Try to fit the data into an existing datatype. Otherwise ignore entry.
                        switch (code)
                        {
                            case XDataCode.Int16:
                                if (item.Value is short int16val_1)
                                    newValue = int16val_1;
                                else if (item.Value is int int32val_1 && int32val_1 >= Int16.MinValue && int32val_1 <= Int16.MaxValue)
                                    newValue = Convert.ToInt16(int32val_1);
                                else if (item.Value is long int64val_1 && int64val_1 >= Int16.MinValue && int64val_1 <= Int16.MaxValue)
                                    newValue = Convert.ToInt16(int64val_1);
                                break;
                            case XDataCode.Int32:
                                if (item.Value is short int16val_2)
                                    newValue = Convert.ToInt32(int16val_2);
                                else if (item.Value is int int32val_2)
                                    newValue = int32val_2;
                                else if (item.Value is long int64val_2 && int64val_2 >= Int32.MinValue && int64val_2 <= Int32.MaxValue)
                                    newValue = Convert.ToInt32(int64val_2);
                                break;
                            default:
                                newValue = item.Value;
                                break;
                        }

                        if (newValue != null)
                        {
                            XDataRecord record = new XDataRecord(code, newValue);
                            data.XDataRecord.Add(record);
                        }
                    }
                    if (data.XDataRecord.Count > 0)
                        entity.XData.Add(data);
                }
                else
                {
                    ApplicationRegistry registry = new ApplicationRegistry(ApplicationRegistry.DefaultName);
                    XData data = new XData(registry);

                    XDataRecord record = null;

                    switch (de.Value)
                    {
                        case string strValue:
                            record = new XDataRecord(XDataCode.String, strValue);
                            break;
                        case short shrValue:
                            record = new XDataRecord(XDataCode.Int16, shrValue);
                            break;
                        case int intValue:
                            record = new XDataRecord(XDataCode.Int32, intValue);
                            break;
                        case double dblValue:
                            record = new XDataRecord(XDataCode.Real, dblValue);
                            break;
                        case byte[] bytValue:
                            record = new XDataRecord(XDataCode.BinaryData, bytValue);
                            break;
                    }

                    if (record != null)
                        data.XDataRecord.Add(record);
                }
            }
        }

        private EntityObject[] ExportShell(GeoObject.Shell shell)
        {
            if (Settings.GlobalSettings.GetBoolValue("DxfImport.SingleMeshPerFace", false))
            {
                List<EntityObject> res = new List<EntityObject>();
                for (int i = 0; i < shell.Faces.Length; i++)
                {
                    EntityObject mesh = ExportFace(shell.Faces[i]);
                    if (mesh != null) res.Add(mesh);
                }
                return res.ToArray();
            }
            else
            {
                List<EntityObject> res = new List<EntityObject>();
                Dictionary<int, (List<Vector3>, List<short[]>)> mesh = new Dictionary<int, (List<Vector3>, List<short[]>)>();
                for (int i = 0; i < shell.Faces.Length; i++)
                {
                    CollectMeshByColor(mesh, shell.Faces[i]);
                }
                foreach (KeyValuePair<int, (List<Vector3> vertices, List<short[]> indices)> item in mesh)
                {
                    PolyfaceMesh pfm = new PolyfaceMesh(item.Value.vertices, item.Value.indices);
                    SetColor(pfm, item.Key);
                    res.Add(pfm);
                }
                return res.ToArray();
            }
        }
        private netDxf.Entities.EntityObject ExportFace(GeoObject.Face face)
        {
            if (Settings.GlobalSettings.GetBoolValue("DxfImport.UseMesh", false))
            {
                if (face.Surface is PlaneSurface ps)
                {
                    if (face.OutlineEdges.Length == 4 && face.OutlineEdges[0].Curve3D is GeoObject.Line && face.OutlineEdges[1].Curve3D is GeoObject.Line && face.OutlineEdges[2].Curve3D is GeoObject.Line && face.OutlineEdges[3].Curve3D is GeoObject.Line)
                    {
                        // 4 lines, export as a simple PolyfaceMesh with 4 vertices
                        List<Vector3> vertices = new List<Vector3>();
                        for (int i = 0; i < 4; i++)
                        {
                            vertices.Add(Vector3(face.OutlineEdges[i].StartVertex(face).Position));
                        }
                        netDxf.Entities.Mesh res = new Mesh(vertices, new int[][] { new int[] { 0, 1, 2, 3 } });
                        SetAttributes(res, face);
                        return res;
                    }
                }
                {
                    face.GetTriangulation(triangulationPrecision, out GeoPoint[] trianglePoint, out GeoPoint2D[] triangleUVPoint, out int[] triangleIndex, out BoundingCube triangleExtent);
                    int[][] indices = new int[triangleIndex.Length / 3][];
                    for (int i = 0; i < triangleIndex.Length - 2; i += 3)
                    {   // it is strange, but the indices must be +1
                        indices[i / 3] = new int[] { triangleIndex[i], triangleIndex[i + 1], triangleIndex[i + 2] };
                    }
                    Vector3[] vertices = new Vector3[trianglePoint.Length];
                    for (int i = 0; i < trianglePoint.Length; i++)
                    {
                        vertices[i] = Vector3(trianglePoint[i]);
                    }
                    Mesh res = new Mesh(vertices, indices);
                    SetAttributes(res, face);
                    return res;
                }
            }
            else
            {
                if (face.Surface is PlaneSurface ps)
                {
                    if (face.OutlineEdges.Length == 4 && face.OutlineEdges[0].Curve3D is GeoObject.Line && face.OutlineEdges[1].Curve3D is GeoObject.Line && face.OutlineEdges[2].Curve3D is GeoObject.Line && face.OutlineEdges[3].Curve3D is GeoObject.Line)
                    {
                        // 4 lines, export as a simple PolyfaceMesh with 4 vertices
                        List<Vector3> vertices = new List<Vector3>();
                        for (int i = 0; i < 4; i++)
                        {
                            vertices.Add(Vector3(face.OutlineEdges[i].StartVertex(face).Position));
                        }
                        netDxf.Entities.PolyfaceMesh res = new PolyfaceMesh(vertices, new short[][] { new short[] { 1, 2, 3, 4 } });
                        SetAttributes(res, face);
                        return res;
                    }
                }
                {
                    face.GetTriangulation(triangulationPrecision, out GeoPoint[] trianglePoint, out GeoPoint2D[] triangleUVPoint, out int[] triangleIndex, out BoundingCube triangleExtent);
                    short[][] indices = new short[triangleIndex.Length / 3][];
                    for (int i = 0; i < triangleIndex.Length - 2; i += 3)
                    {   // it is strange, but the indices must be +1
                        indices[i / 3] = new short[] { (short)(triangleIndex[i] + 1), (short)(triangleIndex[i + 1] + 1), (short)(triangleIndex[i + 2] + 1) };
                    }
                    Vector3[] vertices = new Vector3[trianglePoint.Length];
                    for (int i = 0; i < trianglePoint.Length; i++)
                    {
                        vertices[i] = Vector3(trianglePoint[i]);
                    }
                    PolyfaceMesh res = new PolyfaceMesh(vertices, indices);
                    SetAttributes(res, face);
                    return res;
                }
            }
        }
        private void CollectMeshByColor(Dictionary<int, (List<Vector3>, List<short[]>)> mesh, Face face)
        {
            if (!mesh.TryGetValue(face.ColorDef.Color.ToArgb(), out (List<Vector3> vertices, List<short[]> indices) mc))
            {
                mesh[face.ColorDef.Color.ToArgb()] = mc = (new List<Vector3>(), new List<short[]>());
            }
            short offset = (short)(mc.vertices.Count + 1);
            face.GetTriangulation(triangulationPrecision, out GeoPoint[] trianglePoint, out GeoPoint2D[] triangleUVPoint, out int[] triangleIndex, out BoundingCube triangleExtent);
            short[][] indices = new short[triangleIndex.Length / 3][];
            for (int i = 0; i < triangleIndex.Length - 2; i += 3)
            {   // it is strange, but the indices must be +1
                indices[i / 3] = new short[] { (short)(triangleIndex[i] + offset), (short)(triangleIndex[i + 1] + offset), (short)(triangleIndex[i + 2] + offset) };
            }
            mc.indices.AddRange(indices);
            Vector3[] vertices = new Vector3[trianglePoint.Length];
            for (int i = 0; i < trianglePoint.Length; i++)
            {
                vertices[i] = Vector3(trianglePoint[i]);
            }
            mc.vertices.AddRange(vertices);
        }
        private netDxf.Entities.Text ExportText(GeoObject.Text text)
        {
            System.Drawing.FontStyle fs = System.Drawing.FontStyle.Regular;
            if (text.Bold) fs |= System.Drawing.FontStyle.Bold;
            if (text.Italic) fs |= System.Drawing.FontStyle.Italic;
            System.Drawing.Font font = new System.Drawing.Font(text.Font, 1000.0f, fs);
            netDxf.Entities.Text res = new netDxf.Entities.Text(text.TextString, Vector2.Zero, text.TextSize * 1000 / font.Height, new TextStyle(text.Font, text.Font + ".ttf"));
            ModOp toText = ModOp.Fit(GeoPoint.Origin, new GeoVector[] { GeoVector.XAxis, GeoVector.YAxis, GeoVector.ZAxis }, text.Location, new GeoVector[] { text.LineDirection.Normalized, text.GlyphDirection.Normalized, text.LineDirection.Normalized ^ text.GlyphDirection.Normalized });
            res.TransformBy(Matrix4(toText)); // easier than setting normal and rotation
            return res;
        }

        private netDxf.Entities.Insert ExportBlock(GeoObject.Block blk)
        {
            List<EntityObject> entities = new List<EntityObject>();
            for (int i = 0; i < blk.Children.Count; i++)
            {
                EntityObject[] entity = GeoObjectToEntity(blk.Child(i));
                if (entity != null) entities.AddRange(entity);
            }
            string name = blk.Name;
            if (name == null || doc.Blocks.Contains(name) || !TableObject.IsValidName(name)) name = GetNextAnonymousBlockName();
            netDxf.Blocks.Block block = new netDxf.Blocks.Block(name, entities);
            doc.Blocks.Add(block);
            return new netDxf.Entities.Insert(block);
        }
        private netDxf.Entities.Insert ExportPath(Path path)
        {
            List<EntityObject> entities = new List<EntityObject>();
            for (int i = 0; i < path.Curves.Length; i++)
            {
                EntityObject[] curve = GeoObjectToEntity(path.Curves[i] as IGeoObject);
                if (curve != null) entities.AddRange(curve);
            }
            netDxf.Blocks.Block block = new netDxf.Blocks.Block(GetNextAnonymousBlockName(), entities);
            doc.Blocks.Add(block);
            return new netDxf.Entities.Insert(block);
        }

        private EntityObject[] ExportPathWithoutBlock(Path path)
        {
            List<EntityObject> entities = new List<EntityObject>();
            for (int i = 0; i < path.Curves.Length; i++)
            {
                EntityObject[] curve = GeoObjectToEntity(path.Curves[i] as IGeoObject);
                if (curve != null) entities.AddRange(curve);
            }
            return entities.ToArray();
        }

        private netDxf.Entities.Spline ExportBSpline(BSpline bspline)
        {
            List<Vector3> poles = new List<Vector3>(bspline.Poles.Length);

            for (int i = 0; i < bspline.Poles.Length; i++)
                poles.Add(Vector3(bspline.Poles[i]));

            List<double> knots = new List<double>();
            for (int i = 0; i < bspline.Knots.Length; i++)
            {
                for (int j = 0; j < bspline.Multiplicities[i]; j++) knots.Add(bspline.Knots[i]);
            }

            if (bspline.HasWeights)
                return new netDxf.Entities.Spline(poles, bspline.Weights, knots, (short)bspline.Degree, bspline.IsClosed);
            else
                return new netDxf.Entities.Spline(poles, null, knots, (short)bspline.Degree, bspline.IsClosed);
        }

        private netDxf.Entities.Polyline3D ExportPolyline(GeoObject.Polyline polyline)
        {
            //TODO: Check if a new method for Polyline2D (old LwPolyline) is necessary
            List<Vector3> vertices = new List<Vector3>();
            for (int i = 0; i < polyline.Vertices.Length; i++)
            {
                vertices.Add(Vector3(polyline.Vertices[i]));
            }
            if (polyline.IsClosed) vertices.Add(Vector3(polyline.Vertices[0]));
            return new netDxf.Entities.Polyline3D(vertices);
        }

        private netDxf.Entities.Point ExportPoint(GeoObject.Point point)
        {
            return new netDxf.Entities.Point(Vector3(point.Location));
        }
        private netDxf.Entities.Line ExportLine(GeoObject.Line line)
        {
            return new netDxf.Entities.Line(Vector3(line.StartPoint), Vector3(line.EndPoint));
        }
        private EntityObject ExportEllipse(GeoObject.Ellipse elli)
        {
            netDxf.Entities.EntityObject entity = null;
            if (elli.IsArc)
            {
                Plane dxfPlane;
                if (elli.CounterClockWise) dxfPlane = Import.Plane(Vector3(elli.Center), Vector3(elli.Plane.Normal));
                else dxfPlane = Import.Plane(Vector3(elli.Center), Vector3(-elli.Plane.Normal));
                if (elli.IsCircle)
                {
                    GeoObject.Ellipse aligned = GeoObject.Ellipse.Construct();
                    aligned.SetArcPlaneCenterStartEndPoint(dxfPlane, dxfPlane.Project(elli.Center), dxfPlane.Project(elli.StartPoint), dxfPlane.Project(elli.EndPoint), dxfPlane, true);

                    //Check if ellipse is actually a closed circle
                    if (Math.Abs(elli.SweepParameter) > Math.PI && Precision.IsEqual(elli.StartPoint, elli.EndPoint))
                        entity = new netDxf.Entities.Circle(Vector3(aligned.Center), aligned.Radius);
                    else
                        entity = new netDxf.Entities.Arc(Vector3(aligned.Center), aligned.Radius, aligned.StartParameter / Math.PI * 180, (aligned.StartParameter + aligned.SweepParameter) / Math.PI * 180);

                    entity.Normal = Vector3(dxfPlane.Normal);
                }
                else
                {
                    netDxf.Entities.Ellipse expelli = new netDxf.Entities.Ellipse(Vector3(elli.Center), 2 * elli.MajorRadius, 2 * elli.MinorRadius);
                    entity = expelli;
                    entity.Normal = Vector3(elli.Plane.Normal);
                    Plane cdbplane = elli.Plane;
                    GeoVector2D dir = dxfPlane.Project(cdbplane.DirectionX);
                    SweepAngle rot = new SweepAngle(GeoVector2D.XAxis, dir);
                    if (elli.SweepParameter < 0)
                    {   // there are no clockwise oriented ellipse arcs in dxf
                        expelli.Rotation = -rot.Degree;

                        double startParameter = elli.StartParameter + elli.SweepParameter + Math.PI;
                        expelli.StartAngle = CalcStartEndAngle(startParameter, expelli.MajorAxis, expelli.MinorAxis);

                        double endParameter = elli.StartParameter + Math.PI;
                        expelli.EndAngle = CalcStartEndAngle(endParameter, expelli.MajorAxis, expelli.MinorAxis);
                    }
                    else
                    {
                        expelli.Rotation = rot.Degree;
                        expelli.StartAngle = CalcStartEndAngle(elli.StartParameter, expelli.MajorAxis, expelli.MinorAxis);

                        double endParameter = elli.StartParameter + elli.SweepParameter;
                        expelli.EndAngle = CalcStartEndAngle(endParameter, expelli.MajorAxis, expelli.MinorAxis);
                    }
                }
            }
            else
            {
                if (elli.IsCircle)
                {
                    entity = new netDxf.Entities.Circle(Vector3(elli.Center), elli.Radius);
                    entity.Normal = Vector3(elli.Plane.Normal);
                }
                else
                {
                    netDxf.Entities.Ellipse expelli = new netDxf.Entities.Ellipse(Vector3(elli.Center), 2 * elli.MajorRadius, 2 * elli.MinorRadius);
                    entity = expelli;
                    entity.Normal = Vector3(elli.Plane.Normal);
                    Plane dxfplane = Import.Plane(expelli.Center, expelli.Normal); // this plane is not correct, it has to be rotated
                    Plane cdbplane = elli.Plane;
                    GeoVector2D dir = dxfplane.Project(cdbplane.DirectionX);
                    SweepAngle rot = new SweepAngle(GeoVector2D.XAxis, dir);
                    expelli.Rotation = rot.Degree;
                }
            }
            return entity;
        }

        private double CalcStartEndAngle(double startEndParameter, double majorAxis, double minorAxis)
        {
            double a = majorAxis * 0.5d;
            double b = minorAxis * 0.5d;
            Vector2 startPoint = new Vector2(a * Math.Cos(startEndParameter), b * Math.Sin(startEndParameter));
            return Vector2.Angle(startPoint) * netDxf.MathHelper.RadToDeg;
        }

        private static void SetEllipseParameters(netDxf.Entities.Ellipse ellipse, double startparam, double endparam)
        {
            //CADability: also set the start and end parameter
            //ellipse.StartParameter = startparam;
            //ellipse.EndParameter = endparam;
            if (MathHelper.IsZero(startparam) && MathHelper.IsEqual(endparam, MathHelper.TwoPI))
            {
                ellipse.StartAngle = 0.0;
                ellipse.EndAngle = 0.0;
            }
            else
            {
                double a = ellipse.MajorAxis * 0.5;
                double b = ellipse.MinorAxis * 0.5;

                Vector2 startPoint = new Vector2(a * Math.Cos(startparam), b * Math.Sin(startparam));
                Vector2 endPoint = new Vector2(a * Math.Cos(endparam), b * Math.Sin(endparam));

                if (Vector2.Equals(startPoint, endPoint))
                {
                    ellipse.StartAngle = 0.0;
                    ellipse.EndAngle = 0.0;
                }
                else
                {
                    ellipse.StartAngle = Vector2.Angle(startPoint) * MathHelper.RadToDeg;
                    ellipse.EndAngle = Vector2.Angle(endPoint) * MathHelper.RadToDeg;
                }
            }
        }

        private void SetColor(EntityObject entity, int argb)
        {
            AciColor clr;
            if (argb.Equals(Color.White.ToArgb()) || argb.Equals(Color.Black.ToArgb()))
            {
                clr = AciColor.Default;
            }
            else
            {
                clr = AciColor.FromTrueColor(argb);
                if (clr.Index > 0 && clr.Index < 256)
                {
                    var indexColor = AciColor.FromCadIndex(clr.Index);
                    if (indexColor.ToColor().ToArgb().Equals(argb))
                    {
                        // if the color matches the index color exactly
                        // we don't need to use TruColor
                        clr.UseTrueColor = false;
                    }
                }
            }
            entity.Color = clr;
        }
        private void SetAttributes(EntityObject entity, IGeoObject go)
        {
            if (go is IColorDef cd && cd.ColorDef != null)
            {
                AciColor clr;
                if (cd.ColorDef.Color.ToArgb().Equals(Color.White.ToArgb()) || cd.ColorDef.Color.ToArgb().Equals(Color.Black.ToArgb()))
                {
                    clr = AciColor.Default;
                }
                else
                {
                    clr = AciColor.FromTrueColor(cd.ColorDef.Color.ToArgb());
                    if (clr.Index > 0 && clr.Index < 256)
                    {
                        var indexColor = AciColor.FromCadIndex(clr.Index);
                        if (indexColor.ToColor().ToArgb().Equals(cd.ColorDef.Color.ToArgb()))
                        {
                            // if the color matches the index color exactly
                            // we don't need to use TruColor
                            clr.UseTrueColor = false;
                        }
                    }
                }
                entity.Color = clr;
            }
            if (go.Layer != null)
            {
                if (!createdLayers.TryGetValue(go.Layer, out netDxf.Tables.Layer layer))
                {
                    layer = new netDxf.Tables.Layer(go.Layer.Name);
                    doc.Layers.Add(layer);
                    createdLayers[go.Layer] = layer;
                }
                entity.Layer = layer;
            }
            if (go is ILinePattern lp && lp.LinePattern != null)
            {
                if (!createdLinePatterns.TryGetValue(lp.LinePattern, out Linetype linetype))
                {
                    List<LinetypeSegment> segments = new List<LinetypeSegment>();
                    if (lp.LinePattern.Pattern != null)
                    {
                        for (int i = 0; i < lp.LinePattern.Pattern.Length; i++)
                        {
                            LinetypeSegment ls;
                            if ((i & 0x01) == 0) ls = new LinetypeSimpleSegment(lp.LinePattern.Pattern[i]);
                            else ls = new LinetypeSimpleSegment(-lp.LinePattern.Pattern[i]);
                            segments.Add(ls);
                        }
                    }
                    linetype = new Linetype(lp.LinePattern.Name);
                    linetype.Segments.AddRange(segments);
                    doc.Linetypes.Add(linetype);
                    createdLinePatterns[lp.LinePattern] = linetype;
                }
                entity.Linetype = linetype;
            }
            if (go is ILineWidth lw && lw.LineWidth != null)
            {
                double minError = double.MaxValue;
                Lineweight found = Lineweight.Default;
                foreach (Lineweight lwe in Enum.GetValues(typeof(Lineweight)))
                {
                    double err = Math.Abs(((int)lwe) / 100.0 - lw.LineWidth.Width);
                    if (err < minError)
                    {
                        minError = err;
                        found = lwe;
                    }
                }
                entity.Lineweight = found;
            }
        }
        private Vector3 Vector3(GeoPoint p)
        {
            return new Vector3(p.x, p.y, p.z);
        }
        private Vector3 Vector3(GeoVector v)
        {
            return new Vector3(v.x, v.y, v.z);
        }
        private netDxf.Matrix4 Matrix4(ModOp toText)
        {
            return new netDxf.Matrix4(toText[0, 0], toText[0, 1], toText[0, 2], toText[0, 3], toText[1, 0], toText[1, 1], toText[1, 2], toText[1, 3], toText[2, 0], toText[2, 1], toText[2, 2], toText[2, 3], 0, 0, 0, 1);
        }
        private string GetNextAnonymousBlockName()
        {
            return "AnonymousBlock" + (++anonymousBlockCounter).ToString();
        }
    }
}