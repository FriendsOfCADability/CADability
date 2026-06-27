using CADability.Attribute;
using CADability.GeoObject;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using ACadSharp.Types.Units;
using ACadSharp.XData;
using CSMath;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Color = System.Drawing.Color;

namespace CADability.DXF
{
    public class Export
    {
        private CadDocument doc;
        private Dictionary<CADability.Attribute.Layer, ACadSharp.Tables.Layer> createdLayers;
        private Dictionary<CADability.Attribute.LinePattern, ACadSharp.Tables.LineType> createdLinePatterns;
        private Dictionary<string, ACadSharp.Tables.TextStyle> createdTextStyles;
        private HashSet<string> createdBlockNames;
        private int anonymousBlockCounter = 0;
        private double triangulationPrecision = 0.1;

        public Export(ACadVersion version = ACadVersion.AC1015)
        {
            doc = new CadDocument(version);
            doc.Header.AngularDirection = AngularDirection.CounterClockWise;
            doc.Header.InsUnits = UnitsType.Millimeters;
            createdLayers = new Dictionary<CADability.Attribute.Layer, ACadSharp.Tables.Layer>();
            createdLinePatterns = new Dictionary<CADability.Attribute.LinePattern, ACadSharp.Tables.LineType>();
            createdTextStyles = new Dictionary<string, ACadSharp.Tables.TextStyle>();
            createdBlockNames = new HashSet<string>();
        }

        public byte[] WriteToByteArray(Project toExport)
        {
            Model modelSpace = null;
            if (toExport.GetModelCount() == 1) modelSpace = toExport.GetModel(0);
            else modelSpace = toExport.FindModel("*Model_Space");
            if (modelSpace == null) modelSpace = toExport.GetActiveModel();

            BlockRecord msBlock = doc.ModelSpace;
            for (int i = 0; i < modelSpace.Count; i++)
            {
                Entity[] entities = GeoObjectToEntity(modelSpace[i]);
                if (entities != null)
                    foreach (var e in entities)
                        msBlock.Entities.Add(e);
            }
            SetExtents(modelSpace);
            var ms = new MemoryStream();
            using (var writer = new DxfWriter(ms, doc, false))
                writer.Write();
            return ms.ToArray();
        }

        public void WriteToFile(Project toExport, string filename)
        {
            Model modelSpace = null;
            if (toExport.GetModelCount() == 1) modelSpace = toExport.GetModel(0);
            else modelSpace = toExport.FindModel("*Model_Space");
            if (modelSpace == null) modelSpace = toExport.GetActiveModel();

            GeoObjectList geoObjects = new GeoObjectList();
            List<Face> faces = new List<Face>();
            for (int i = 0; i < modelSpace.Count; i++)
            {
                if (modelSpace[i] is Face face) faces.Add(face.Clone() as Face);
                else geoObjects.Add(modelSpace[i]);
            }
            if (faces.Count > 0) geoObjects.Add(Shell.FromFaces(faces.ToArray()));

            BlockRecord msBlock = doc.ModelSpace;
            for (int i = 0; i < geoObjects.Count; i++)
            {
                Entity[] entities = GeoObjectToEntity(geoObjects[i]);
                if (entities != null)
                    foreach (var e in entities)
                        msBlock.Entities.Add(e);
            }
            SetExtents(modelSpace);
            using (var writer = new DxfWriter(filename, doc, false))
                writer.Write();
        }

        private void SetExtents(Model model)
        {
            try
            {
                BoundingCube ext = model.Extent;
                if (!ext.IsEmpty)
                {
                    doc.Header.ModelSpaceExtMin = new XYZ(ext.Xmin, ext.Ymin, ext.Zmin);
                    doc.Header.ModelSpaceExtMax = new XYZ(ext.Xmax, ext.Ymax, ext.Zmax);
                    doc.Header.ModelSpaceLimitsMin = new CSMath.XY(ext.Xmin, ext.Ymin);
                    doc.Header.ModelSpaceLimitsMax = new CSMath.XY(ext.Xmax, ext.Ymax);
                }
            }
            catch { }
        }

        private Entity[] GeoObjectToEntity(IGeoObject geoObject)
        {
            Entity entity = null;
            Entity[] entities = null;
            switch (geoObject)
            {
                case GeoObject.Point point: entity = ExportPoint(point); break;
                case GeoObject.Line line: entity = ExportLine(line); break;
                case GeoObject.Ellipse elli: entity = ExportEllipse(elli); break;
                case GeoObject.Polyline polyline: entity = ExportPolyline(polyline); break;
                case GeoObject.BSpline bspline: entity = ExportBSpline(bspline); break;
                case GeoObject.Path path:
                    if (Settings.GlobalSettings.GetBoolValue("DxfExport.ExportPathsAsBlocks", false))
                        entity = ExportPath(path);
                    else
                        entities = ExportPathWithoutBlock(path);
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
                return new Entity[] { entity };
            }
            if (entities != null)
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    if (geoObject.Layer == null) continue;
                    entities[i].Layer = GetOrCreateLayer(geoObject.Layer);
                }
                return entities;
            }
            return null;
        }

        private void SetUserData(Entity entity, IGeoObject go)
        {
            if (entity is null || go is null || go.UserData is null || go.UserData.Count == 0)
                return;

            foreach (KeyValuePair<string, object> de in go.UserData)
            {
                if (de.Value is ExtendedEntityData xData)
                {
                    AppId appId = GetOrCreateAppId(xData.ApplicationName);
                    ExtendedData extData = new ExtendedData();
                    foreach (var item in xData.Data)
                    {
                        ExtendedDataRecord record = CreateXDataRecord(item.Key, item.Value);
                        if (record != null)
                        {
                            try { extData.Records.Add(record); }
                            catch { /* skip unserializable values */ }
                        }
                    }
                    if (extData.Records.Count > 0)
                        entity.ExtendedData.Add(appId, extData);
                }
                else if (de.Value != null && !(de.Value is UserInterface.StringProperty))
                {
                    AppId appId = GetOrCreateAppId("CADABILITY");
                    ExtendedDataRecord record = null;
                    switch (de.Value)
                    {
                        case string strVal: record = new ExtendedDataString(strVal); break;
                        case short shrVal: record = new ExtendedDataInteger16(shrVal); break;
                        case int intVal: record = new ExtendedDataInteger32(intVal); break;
                        case double dblVal: record = new ExtendedDataReal(dblVal); break;
                        case byte[] bytVal: record = new ExtendedDataBinaryChunk(bytVal); break;
                        default: continue;
                    }
                    try
                    {
                        if (!entity.ExtendedData.ContainsKey(appId))
                            entity.ExtendedData.Add(appId, new ExtendedData());
                        if (entity.ExtendedData.TryGet(appId, out ExtendedData existing))
                            existing.Records.Add(record);
                    }
                    catch { /* skip */ }
                }
            }
        }

        private ACadSharp.Tables.Layer GetOrCreateLayer(CADability.Attribute.Layer cadLayer)
        {
            if (createdLayers.TryGetValue(cadLayer, out ACadSharp.Tables.Layer layer))
                return layer;
            foreach (ACadSharp.Tables.Layer existing in doc.Layers)
                if (string.Equals(existing.Name, cadLayer.Name, StringComparison.OrdinalIgnoreCase)) { createdLayers[cadLayer] = existing; return existing; }
            layer = new ACadSharp.Tables.Layer(cadLayer.Name);
            doc.Layers.Add(layer);
            createdLayers[cadLayer] = layer;
            return layer;
        }

        private ACadSharp.Tables.LineType GetOrCreateLineType(CADability.Attribute.LinePattern lp)
        {
            if (createdLinePatterns.TryGetValue(lp, out ACadSharp.Tables.LineType lt))
                return lt;
            foreach (ACadSharp.Tables.LineType existing in doc.LineTypes)
                if (string.Equals(existing.Name, lp.Name, StringComparison.OrdinalIgnoreCase)) { createdLinePatterns[lp] = existing; return existing; }
            lt = new ACadSharp.Tables.LineType(lp.Name);
            if (lp.Pattern != null)
                for (int i = 0; i < lp.Pattern.Length; i++)
                {
                    double len = (i & 1) == 0 ? lp.Pattern[i] : -lp.Pattern[i];
                    lt.AddSegment(new ACadSharp.Tables.LineType.Segment { Length = len });
                }
            doc.LineTypes.Add(lt);
            createdLinePatterns[lp] = lt;
            return lt;
        }

        private AppId GetOrCreateAppId(string name)
        {
            foreach (AppId existing in doc.AppIds)
                if (existing.Name == name) return existing;
            AppId appId = new AppId(name);
            doc.AppIds.Add(appId);
            return appId;
        }

        private static ExtendedDataRecord CreateXDataRecord(XDataCode code, object value)
        {
            try
            {
                switch (code)
                {
                    case XDataCode.Int16:
                        short s16 = value is short sh ? sh
                            : value is int i16 ? (short)i16
                            : value is long l16 ? (short)l16
                            : (short)0;
                        return new ExtendedDataInteger16(s16);
                    case XDataCode.Int32:
                        int i32 = value is int ii ? ii
                            : value is short si ? (int)si
                            : value is long li ? (int)li
                            : 0;
                        return new ExtendedDataInteger32(i32);
                    case XDataCode.Real:
                    case XDataCode.Distance:
                    case XDataCode.ScaleFactor:
                        double d = value is double dv ? dv : Convert.ToDouble(value);
                        return new ExtendedDataReal(d);
                    case XDataCode.BinaryData:
                        return value is byte[] bytes ? new ExtendedDataBinaryChunk(bytes) : null;
                    case XDataCode.DatabaseHandle:
                        string hStr = value?.ToString() ?? "";
                        ulong hVal = ulong.TryParse(hStr, System.Globalization.NumberStyles.HexNumber, null, out ulong hParsed) ? hParsed : 0;
                        return new ExtendedDataHandle(hVal);
                    case XDataCode.ControlString:
                        // { = opening (isClosing=false), } = closing (isClosing=true)
                        string cs = value?.ToString() ?? "";
                        return new ExtendedDataControlString(cs == "}");
                    default:
                        return new ExtendedDataString(value?.ToString() ?? "");
                }
            }
            catch { return null; }
        }

        private Entity[] ExportShell(GeoObject.Shell shell)
        {
            if (Settings.GlobalSettings.GetBoolValue("DxfImport.SingleMeshPerFace", false))
            {
                List<Entity> res = new List<Entity>();
                for (int i = 0; i < shell.Faces.Length; i++)
                {
                    Entity mesh = ExportFace(shell.Faces[i]);
                    if (mesh != null) res.Add(mesh);
                }
                return res.ToArray();
            }
            else
            {
                List<Entity> res = new List<Entity>();
                Dictionary<int, (List<XYZ>, List<short[]>)> mesh = new Dictionary<int, (List<XYZ>, List<short[]>)>();
                for (int i = 0; i < shell.Faces.Length; i++)
                    CollectMeshByColor(mesh, shell.Faces[i]);
                foreach (var item in mesh)
                {
                    Entity pfm = BuildPolyfaceMesh(item.Value.Item1, item.Value.Item2);
                    SetColorOnEntity(pfm, item.Key);
                    res.Add(pfm);
                }
                return res.ToArray();
            }
        }

        private Entity ExportFace(GeoObject.Face face)
        {
            if (Settings.GlobalSettings.GetBoolValue("DxfImport.UseMesh", false))
            {
                if (face.Surface is PlaneSurface && face.OutlineEdges.Length == 4 &&
                    face.OutlineEdges[0].Curve3D is GeoObject.Line &&
                    face.OutlineEdges[1].Curve3D is GeoObject.Line &&
                    face.OutlineEdges[2].Curve3D is GeoObject.Line &&
                    face.OutlineEdges[3].Curve3D is GeoObject.Line)
                {
                    var mesh = new ACadSharp.Entities.Mesh();
                    for (int i = 0; i < 4; i++)
                        mesh.Vertices.Add(ToXYZ(face.OutlineEdges[i].StartVertex(face).Position));
                    mesh.Faces.Add(new int[] { 0, 1, 2, 3 });
                    SetAttributes(mesh, face);
                    return mesh;
                }
                else
                {
                    face.GetTriangulation(triangulationPrecision, out GeoPoint[] pts, out GeoPoint2D[] uvPts,
                        out int[] triIdx, out BoundingCube ext);
                    var mesh = new ACadSharp.Entities.Mesh();
                    foreach (var pt in pts) mesh.Vertices.Add(ToXYZ(pt));
                    for (int i = 0; i + 2 < triIdx.Length; i += 3)
                        mesh.Faces.Add(new int[] { triIdx[i], triIdx[i + 1], triIdx[i + 2] });
                    SetAttributes(mesh, face);
                    return mesh;
                }
            }
            else
            {
                if (face.Surface is PlaneSurface && face.OutlineEdges.Length == 4 &&
                    face.OutlineEdges[0].Curve3D is GeoObject.Line &&
                    face.OutlineEdges[1].Curve3D is GeoObject.Line &&
                    face.OutlineEdges[2].Curve3D is GeoObject.Line &&
                    face.OutlineEdges[3].Curve3D is GeoObject.Line)
                {
                    List<XYZ> verts = new List<XYZ>();
                    for (int i = 0; i < 4; i++)
                        verts.Add(ToXYZ(face.OutlineEdges[i].StartVertex(face).Position));
                    Entity pfm = BuildPolyfaceMesh(verts, new List<short[]> { new short[] { 1, 2, 3, 4 } });
                    SetAttributes(pfm, face);
                    return pfm;
                }
                else
                {
                    face.GetTriangulation(triangulationPrecision, out GeoPoint[] pts, out GeoPoint2D[] uvPts,
                        out int[] triIdx, out BoundingCube ext);
                    List<short[]> idxList = new List<short[]>();
                    for (int i = 0; i + 2 < triIdx.Length; i += 3)
                        idxList.Add(new short[] {
                            (short)(triIdx[i] + 1),
                            (short)(triIdx[i + 1] + 1),
                            (short)(triIdx[i + 2] + 1)
                        });
                    List<XYZ> verts = new List<XYZ>();
                    foreach (var pt in pts) verts.Add(ToXYZ(pt));
                    Entity pfm = BuildPolyfaceMesh(verts, idxList);
                    SetAttributes(pfm, face);
                    return pfm;
                }
            }
        }

        private static ACadSharp.Entities.PolyfaceMesh BuildPolyfaceMesh(List<XYZ> vertices, List<short[]> faceIndices)
        {
            var pfm = new ACadSharp.Entities.PolyfaceMesh();
            foreach (var v in vertices)
                pfm.Vertices.Add(new VertexFaceMesh(v));
            foreach (var idx in faceIndices)
            {
                var rec = new VertexFaceRecord
                {
                    Index1 = idx.Length > 0 ? idx[0] : (short)0,
                    Index2 = idx.Length > 1 ? idx[1] : (short)0,
                    Index3 = idx.Length > 2 ? idx[2] : (short)0,
                    Index4 = idx.Length > 3 ? idx[3] : (short)0,
                };
                pfm.Faces.Add(rec);
            }
            return pfm;
        }

        private void CollectMeshByColor(Dictionary<int, (List<XYZ>, List<short[]>)> mesh, Face face)
        {
            int argb = face.ColorDef?.Color.ToArgb() ?? Color.White.ToArgb();
            if (!mesh.TryGetValue(argb, out var mc))
                mesh[argb] = mc = (new List<XYZ>(), new List<short[]>());
            short offset = (short)(mc.Item1.Count + 1);
            face.GetTriangulation(triangulationPrecision, out GeoPoint[] pts, out GeoPoint2D[] uvPts,
                out int[] triIdx, out BoundingCube ext);
            for (int i = 0; i + 2 < triIdx.Length; i += 3)
                mc.Item2.Add(new short[] {
                    (short)(triIdx[i] + offset),
                    (short)(triIdx[i + 1] + offset),
                    (short)(triIdx[i + 2] + offset)
                });
            foreach (var pt in pts) mc.Item1.Add(ToXYZ(pt));
        }

        private Entity ExportText(GeoObject.Text text)
        {
            var textString = text.TextString.Replace("\r\n", " ");
            System.Drawing.FontStyle fs = System.Drawing.FontStyle.Regular;
            if (text.Bold) fs |= System.Drawing.FontStyle.Bold;
            if (text.Italic) fs |= System.Drawing.FontStyle.Italic;

            double height = text.TextSize;
            try
            {
                using (var font = new System.Drawing.Font(text.Font, 1000.0f, fs))
                {
                    height = text.TextSize * 1000.0 / font.Height;
                }
            }
            catch { /* use text.TextSize as-is in headless environments */ }

            string fontName = text.Font ?? "Standard";
            if (!createdTextStyles.TryGetValue(fontName, out ACadSharp.Tables.TextStyle textStyle))
            {
                foreach (ACadSharp.Tables.TextStyle existing in doc.TextStyles)
                    if (string.Equals(existing.Name, fontName, StringComparison.OrdinalIgnoreCase)) { textStyle = existing; break; }
                if (textStyle == null)
                {
                    textStyle = new ACadSharp.Tables.TextStyle(fontName) { Filename = fontName + ".ttf" };
                    doc.TextStyles.Add(textStyle);
                }
                createdTextStyles[fontName] = textStyle;
            }

            var res = new ACadSharp.Entities.TextEntity
            {
                Value = textString,
                Height = height,
                Style = textStyle,
                InsertPoint = ToXYZ(text.Location),
            };

            GeoVector lineDir = text.LineDirection.Normalized;
            GeoVector glyphDir = text.GlyphDirection.Normalized;
            GeoVector normal = lineDir ^ glyphDir;
            res.Normal = ToXYZ(normal);

            // Compute rotation in OCS using the Arbitrary Axis Algorithm
            Plane ocsPlane = Import.Plane(ToXYZ(text.Location), ToXYZ(normal));
            GeoVector2D dir2D = ocsPlane.Project(lineDir);
            res.Rotation = Math.Atan2(dir2D.y, dir2D.x) * (180.0 / Math.PI);
            return res;
        }

        private ACadSharp.Entities.Insert ExportBlock(GeoObject.Block blk)
        {
            List<Entity> entities = new List<Entity>();
            for (int i = 0; i < blk.Children.Count; i++)
            {
                Entity[] ents = GeoObjectToEntity(blk.Child(i));
                if (ents != null) entities.AddRange(ents);
            }
            string name = blk.Name;
            if (name == null || createdBlockNames.Contains(name) || !IsValidBlockName(name))
                name = GetNextAnonymousBlockName();
            createdBlockNames.Add(name);
            var blockRec = new BlockRecord(name);
            foreach (var e in entities) blockRec.Entities.Add(e);
            doc.BlockRecords.Add(blockRec);
            return new ACadSharp.Entities.Insert(blockRec);
        }

        private ACadSharp.Entities.Insert ExportPath(GeoObject.Path path)
        {
            List<Entity> entities = new List<Entity>();
            for (int i = 0; i < path.Curves.Length; i++)
            {
                Entity[] ents = GeoObjectToEntity(path.Curves[i] as IGeoObject);
                if (ents != null) entities.AddRange(ents);
            }
            string name = GetNextAnonymousBlockName();
            var blockRec = new BlockRecord(name);
            foreach (var e in entities) blockRec.Entities.Add(e);
            doc.BlockRecords.Add(blockRec);
            return new ACadSharp.Entities.Insert(blockRec);
        }

        private Entity[] ExportPathWithoutBlock(GeoObject.Path path)
        {
            List<Entity> entities = new List<Entity>();
            for (int i = 0; i < path.Curves.Length; i++)
            {
                Entity[] ents = GeoObjectToEntity(path.Curves[i] as IGeoObject);
                if (ents != null) entities.AddRange(ents);
            }
            return entities.ToArray();
        }

        private ACadSharp.Entities.Spline ExportBSpline(BSpline bspline)
        {
            List<XYZ> poles = new List<XYZ>(bspline.Poles.Length);
            for (int i = 0; i < bspline.Poles.Length; i++)
                poles.Add(ToXYZ(bspline.Poles[i]));

            List<double> knots = new List<double>();
            for (int i = 0; i < bspline.Knots.Length; i++)
                for (int j = 0; j < bspline.Multiplicities[i]; j++)
                    knots.Add(bspline.Knots[i]);

            var spline = new ACadSharp.Entities.Spline();
            spline.Degree = bspline.Degree;
            spline.IsClosed = bspline.IsClosed;
            foreach (var pt in poles) spline.ControlPoints.Add(pt);
            foreach (var k in knots) spline.Knots.Add(k);
            if (bspline.HasWeights)
                foreach (var w in bspline.Weights) spline.Weights.Add(w);
            return spline;
        }

        private ACadSharp.Entities.Polyline3D ExportPolyline(GeoObject.Polyline polyline)
        {
            var poly = new ACadSharp.Entities.Polyline3D();
            for (int i = 0; i < polyline.Vertices.Length; i++)
                poly.Vertices.Add(new Vertex3D(ToXYZ(polyline.Vertices[i])));
            if (polyline.IsClosed)
                poly.Vertices.Add(new Vertex3D(ToXYZ(polyline.Vertices[0])));
            return poly;
        }

        private ACadSharp.Entities.Point ExportPoint(GeoObject.Point point)
        {
            return new ACadSharp.Entities.Point(ToXYZ(point.Location));
        }

        private ACadSharp.Entities.Line ExportLine(GeoObject.Line line)
        {
            return new ACadSharp.Entities.Line
            {
                StartPoint = ToXYZ(line.StartPoint),
                EndPoint = ToXYZ(line.EndPoint)
            };
        }

        private Entity ExportEllipse(GeoObject.Ellipse elli)
        {
            if (elli.IsCircle)
            {
                if (elli.IsArc)
                {
                    // Always keep the arc's own normal (never flip for CW arcs).
                    // CW arcs are represented as CCW by swapping start/end endpoints,
                    // so all exported arcs use Normal=(0,0,1) and work in viewers that
                    // don't implement the OCS transformation.
                    GeoVector normal = elli.Plane.Normal;
                    Plane dxfPlane = Import.Plane(ToXYZ(elli.Center), ToXYZ(normal));
                    GeoObject.Ellipse aligned = GeoObject.Ellipse.Construct();
                    if (elli.CounterClockWise)
                    {
                        aligned.SetArcPlaneCenterStartEndPoint(dxfPlane, dxfPlane.Project(elli.Center),
                            dxfPlane.Project(elli.StartPoint), dxfPlane.Project(elli.EndPoint), dxfPlane, true);
                    }
                    else
                    {
                        // Swap start/end to get the equivalent CCW arc covering the same geometric portion
                        aligned.SetArcPlaneCenterStartEndPoint(dxfPlane, dxfPlane.Project(elli.Center),
                            dxfPlane.Project(elli.EndPoint), dxfPlane.Project(elli.StartPoint), dxfPlane, true);
                    }
                    if (Math.Abs(elli.SweepParameter) > Math.PI && Precision.IsEqual(elli.StartPoint, elli.EndPoint))
                    {
                        return new ACadSharp.Entities.Circle
                        {
                            Center = ToXYZ(aligned.Center),
                            Radius = aligned.Radius,
                            Normal = ToXYZ(normal)
                        };
                    }
                    else
                    {
                        return new ACadSharp.Entities.Arc
                        {
                            Center = ToXYZ(aligned.Center),
                            Radius = aligned.Radius,
                            StartAngle = aligned.StartParameter,
                            EndAngle = aligned.StartParameter + aligned.SweepParameter,
                            Normal = ToXYZ(normal)
                        };
                    }
                }
                else
                {
                    return new ACadSharp.Entities.Circle
                    {
                        Center = ToXYZ(elli.Center),
                        Radius = elli.Radius,
                        Normal = ToXYZ(elli.Plane.Normal)
                    };
                }
            }
            else
            {
                // True ellipse
                GeoVector normal;
                double startParam, endParam;
                GeoVector majorDir;

                if (elli.IsArc && elli.SweepParameter < 0)
                {
                    // Clockwise arc: flip normal and swap start/end
                    normal = -elli.Plane.Normal;
                    majorDir = elli.Plane.DirectionX;
                    double start = elli.StartParameter + elli.SweepParameter;
                    while (start < 0) start += 2.0 * Math.PI;
                    startParam = start;
                    endParam = elli.StartParameter;
                    if (endParam < startParam) endParam += 2.0 * Math.PI;
                }
                else
                {
                    normal = elli.Plane.Normal;
                    majorDir = elli.Plane.DirectionX;
                    startParam = elli.IsArc ? elli.StartParameter : 0.0;
                    endParam = elli.IsArc ? elli.StartParameter + elli.SweepParameter : 2.0 * Math.PI;
                }

                GeoVector majorAxisEnd = elli.MajorRadius * majorDir.Normalized;
                return new ACadSharp.Entities.Ellipse
                {
                    Center = ToXYZ(elli.Center),
                    MajorAxisEndPoint = ToXYZ(majorAxisEnd),
                    RadiusRatio = elli.MinorRadius / elli.MajorRadius,
                    Normal = ToXYZ(normal),
                    StartParameter = startParam,
                    EndParameter = endParam
                };
            }
        }

        private void SetColorOnEntity(Entity entity, int argb)
        {
            System.Drawing.Color clr = System.Drawing.Color.FromArgb(argb);
            if (argb == System.Drawing.Color.White.ToArgb() || argb == System.Drawing.Color.Black.ToArgb())
                entity.Color = ACadSharp.Color.ByLayer;
            else
                entity.Color = new ACadSharp.Color(clr.R, clr.G, clr.B);
        }

        private void SetAttributes(Entity entity, IGeoObject go)
        {
            if (go is IColorDef cd && cd.ColorDef != null)
            {
                System.Drawing.Color clr = cd.ColorDef.Color;
                if (clr.ToArgb() == System.Drawing.Color.White.ToArgb() ||
                    clr.ToArgb() == System.Drawing.Color.Black.ToArgb())
                    entity.Color = ACadSharp.Color.ByLayer;
                else
                    entity.Color = new ACadSharp.Color(clr.R, clr.G, clr.B);
            }
            if (go.Layer != null)
                entity.Layer = GetOrCreateLayer(go.Layer);
            if (go is ILinePattern ilp && ilp.LinePattern != null)
                entity.LineType = GetOrCreateLineType(ilp.LinePattern);
            if (go is ILineWidth lw && lw.LineWidth != null)
            {
                LineWeightType found = LineWeightType.Default;
                double minError = double.MaxValue;
                foreach (LineWeightType lwe in Enum.GetValues(typeof(LineWeightType)))
                {
                    int val = (int)lwe;
                    if (val < 0) continue; // skip Default, ByLayer, ByBlock
                    double err = Math.Abs(val / 100.0 - lw.LineWidth.Width);
                    if (err < minError) { minError = err; found = lwe; }
                }
                entity.LineWeight = found;
            }
        }

        private static XYZ ToXYZ(GeoPoint p) => new XYZ(p.x, p.y, p.z);
        private static XYZ ToXYZ(GeoVector v) => new XYZ(v.x, v.y, v.z);

        private static bool IsValidBlockName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            foreach (char c in name)
                if (c < 0x20 || "<>/\\\":;?*|,=`".IndexOf(c) >= 0) return false;
            return true;
        }

        private string GetNextAnonymousBlockName() => "AnonymousBlock" + (++anonymousBlockCounter);
    }
}
