using CADability.Attribute;
using CADability.Curve2D;
using CADability.GeoObject;
using CADability.Shapes;
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
using System.Linq;
using System.IO;
using Color = System.Drawing.Color;
using PlaneSurface = CADability.GeoObject.PlaneSurface;

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
            if (version <= ACadVersion.AC1015)
                RemovePostR2000Objects();
        }

        /// <summary>
        /// ACadSharp's CadDocument always creates default objects for features that were
        /// introduced after AutoCAD 2000 (scale list, multileader/table styles, visual styles,
        /// book colors, field lists, PDF definitions), regardless of the target version.
        /// Strict readers based on the ODA libraries (DraftSight, QCAD Professional, many CAM
        /// systems) refuse AC1015 files containing these objects, while AutoCAD 2000 itself
        /// never writes them. Remove them for AC1015 and older targets. The corresponding
        /// CLASS declarations are harmless to those readers and are left untouched.
        /// </summary>
        private void RemovePostR2000Objects()
        {
            string[] postR2000Entries = new string[]
            {
                ACadSharp.Objects.CadDictionary.AcadColor,          //DBCOLOR, AutoCAD 2004
                ACadSharp.Objects.CadDictionary.AcadMaterial,       //MATERIAL, AutoCAD 2007
                ACadSharp.Objects.CadDictionary.AcadMLeaderStyle,   //MLEADERSTYLE, AutoCAD 2008
                ACadSharp.Objects.CadDictionary.AcadScaleList,      //SCALE, AutoCAD 2008
                ACadSharp.Objects.CadDictionary.AcadTableStyle,     //TABLESTYLE, AutoCAD 2005
                ACadSharp.Objects.CadDictionary.AcadVisualStyle,    //VISUALSTYLE, AutoCAD 2007
                ACadSharp.Objects.CadDictionary.AcadFieldList,      //FIELDLIST, AutoCAD 2005
                ACadSharp.Objects.CadDictionary.AcadPdfDefinitions, //PDFDEFINITION, AutoCAD 2010
            };
            foreach (string key in postR2000Entries)
                doc.RootDictionary.Remove(key);
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
            {
                ConfigureWriter(writer);
                writer.Write();
            }
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
            {
                ConfigureWriter(writer);
                writer.Write();
            }
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

        /// <summary>
        /// ACadSharp only writes a small default set of header variables, which does not
        /// include the drawing extents. Without them a reader has no zoom-to-extents
        /// information and has to derive it from the geometry — and an infinite construction
        /// line (XLINE) throws that off completely, so unrelated geometry appears to have
        /// moved or shows up where nothing was visible before.
        /// </summary>
        private static void ConfigureWriter(DxfWriter writer)
        {
            string[] variables = new string[] { "$EXTMIN", "$EXTMAX", "$LIMMIN", "$LIMMAX", "$TILEMODE" };
            foreach (string variable in variables)
            {
                try { writer.Configuration.AddHeaderVariable(variable); }
                catch (ArgumentException) { } // not known to this ACadSharp version
            }
        }

        private Entity[] GeoObjectToEntity(IGeoObject geoObject)
        {
            Entity entity = null;
            Entity[] entities = null;
            switch (geoObject)
            {
                case GeoObject.Point point: entity = ExportPoint(point); break;
                case GeoObject.ConstructionLine constructionLine: entity = ExportConstructionLine(constructionLine); break;
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
                case GeoObject.Hatch hatch: entity = ExportHatch(hatch); break;
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
                    entities[i].IsInvisible = !geoObject.IsVisible;
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
                    {
                        // Several UserData entries may carry XData of the same application, e.g. an
                        // "ACAD" entry from an ACadSharp import next to an "ACAD:ACAD" entry from an
                        // older netDxf import, or "CADABILITY" next to plain values that were already
                        // written to that application. An entity holds only one XData group per
                        // application, so the records are merged as netDxf did.
                        if (entity.ExtendedData.TryGet(appId, out ExtendedData existing))
                            existing.Records.AddRange(extData.Records);
                        else
                            entity.ExtendedData.Add(appId, extData);
                    }
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
            // AppId names are case-insensitive in DXF, like the AppIds table itself
            if (doc.AppIds.TryGetValue(name, out AppId existing)) return existing;
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

        private Entity ExportHatch(GeoObject.Hatch hatch)
        {
            if (hatch.CompoundShape == null || hatch.CompoundShape.SimpleShapes.Length == 0)
                return null;
            if (!(hatch.HatchStyle is HatchStyleSolid)) return null;

            Plane plane = hatch.Plane;
            GeoVector normal = plane.Normal;
            // A filled area has no preferred side, and a downward normal spans a mirrored
            // OCS: its X axis is (-1,0,0). Readers that take a hatch boundary for world
            // coordinates — CreateHatch below does — then place the area mirrored about the
            // Y axis. Flipping the normal up keeps OCS and world identical for the usual
            // drawing plane and costs nothing, since the fill looks the same from either side.
            if (normal.z < 0) normal = -normal;
            // Both SOLID corners and HATCH boundary points are read in the OCS that the
            // normal spans, not in world coordinates.
            Plane ocs = Import.Plane(new XYZ(0, 0, 0), ToXYZ(normal));
            SimpleShape[] shapes = hatch.CompoundShape.SimpleShapes;

            // A single outline of three or four straight segments with no holes is what a
            // DXF SOLID can represent exactly; keep writing those back as SOLID.
            if (shapes.Length == 1 && shapes[0].Holes.Length == 0)
            {
                ICurve2D[] segs = shapes[0].Outline.Segments;
                if (segs.Length >= 3 && segs.Length <= 4 && segs.All(c => c is CADability.Curve2D.Line2D))
                {
                    GeoPoint[] pts = new GeoPoint[4];
                    for (int i = 0; i < segs.Length; i++)
                        pts[i] = plane.ToGlobal(segs[i].StartPoint);
                    // DXF SOLID requires 4 corners; duplicate last for triangles
                    if (segs.Length == 3) pts[3] = pts[2];
                    XYZ[] c4 = new XYZ[4];
                    for (int i = 0; i < 4; i++)
                    {
                        GeoPoint2D flat = ocs.Project(pts[i]);
                        c4[i] = new XYZ(flat.x, flat.y, ocs.Distance(pts[i]));
                    }
                    return new ACadSharp.Entities.Solid
                    {
                        FirstCorner = c4[0],
                        SecondCorner = c4[1],
                        // A SOLID is drawn c1->c2->c4->c3, so the third and fourth corners
                        // are swapped against the outline order. Writing them in plain
                        // order makes the area come back as a self-intersecting bowtie.
                        ThirdCorner = c4[3],
                        FourthCorner = c4[2],
                        Normal = ToXYZ(normal)
                    };
                }
            }

            // Anything else — holes, several outlines, curved or many-segment boundaries —
            // does not fit into a SOLID. Reducing it to its outer rectangle loses the
            // cut-outs (a logo's lettering, for instance), so write a real HATCH.
            var res = new ACadSharp.Entities.Hatch
            {
                IsSolid = true,
                Pattern = ACadSharp.Entities.HatchPattern.Solid,
                Normal = ToXYZ(normal),
                Elevation = ocs.Distance(plane.Location),
            };
            foreach (SimpleShape ss in shapes)
            {
                AddHatchPath(res, ss.Outline, plane, ocs, BoundaryPathFlags.External);
                foreach (Border hole in ss.Holes)
                    AddHatchPath(res, hole, plane, ocs, BoundaryPathFlags.Outermost);
            }
            return res.Paths.Count > 0 ? res : null;
        }

        /// <summary>
        /// Appends one closed boundary of a hatch as a path of line edges, converted from the
        /// hatch plane into the OCS that the written normal spans.
        /// </summary>
        private static void AddHatchPath(ACadSharp.Entities.Hatch target, Border border,
            Plane plane, Plane ocs, BoundaryPathFlags flags)
        {
            if (border == null || border.Segments == null || border.Segments.Length == 0) return;
            var path = new ACadSharp.Entities.Hatch.BoundaryPath { Flags = flags };
            foreach (ICurve2D seg in border.Segments)
            {
                // Straight segments map one to one; anything curved is approximated, which
                // is invisible in a solid fill.
                int steps = seg is CADability.Curve2D.Line2D ? 1 : 16;
                GeoPoint2D from = ocs.Project(plane.ToGlobal(seg.StartPoint));
                for (int i = 1; i <= steps; i++)
                {
                    GeoPoint2D raw = i == steps ? seg.EndPoint : seg.PointAt((double)i / steps);
                    GeoPoint2D to = ocs.Project(plane.ToGlobal(raw));
                    path.Edges.Add(new ACadSharp.Entities.Hatch.BoundaryPath.Line
                    {
                        Start = new XY(from.x, from.y),
                        End = new XY(to.x, to.y)
                    });
                    from = to;
                }
            }
            if (path.Edges.Count > 0) target.Paths.Add(path);
        }

        private Entity ExportText(GeoObject.Text text)
        {
            // Keep the line breaks: a multi-line text is exported as MTEXT further down.
            // Collapsing them into spaces (or leaving a raw '\n' in a TEXT value, which the
            // DXF writer escapes to "^J") turns multi-line texts into unreadable single lines.
            var textString = text.TextString.Replace("\r\n", "\n").Replace("\r", "\n");
            System.Drawing.FontStyle fs = System.Drawing.FontStyle.Regular;
            if (text.Bold) fs |= System.Drawing.FontStyle.Bold;
            if (text.Italic) fs |= System.Drawing.FontStyle.Italic;

            // TextSize is the raw DXF cap-height (group 40) stored unchanged during import.
            // Do not apply any font-metric scaling here — it would corrupt the value.
            double height = text.TextSize;

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

            // Map CADability alignment to DXF group 72/73
            TextHorizontalAlignment hAlign = TextHorizontalAlignment.Left;
            TextVerticalAlignmentType vAlign = TextVerticalAlignmentType.Baseline;
            switch (text.LineAlignment)
            {
                case GeoObject.Text.LineAlignMode.Center: hAlign = TextHorizontalAlignment.Center; break;
                case GeoObject.Text.LineAlignMode.Right:  hAlign = TextHorizontalAlignment.Right; break;
            }
            switch (text.Alignment)
            {
                case GeoObject.Text.AlignMode.Bottom:   vAlign = TextVerticalAlignmentType.Bottom; break;
                case GeoObject.Text.AlignMode.Center:   vAlign = TextVerticalAlignmentType.Middle; break;
                case GeoObject.Text.AlignMode.Top:      vAlign = TextVerticalAlignmentType.Top; break;
            }
            bool defaultAlign = hAlign == TextHorizontalAlignment.Left
                             && vAlign == TextVerticalAlignmentType.Baseline;

            GeoVector lineDir = text.LineDirection.Normalized;
            GeoVector glyphDir = text.GlyphDirection.Normalized;
            GeoVector normal = lineDir ^ glyphDir;

            // Compute rotation in OCS using the Arbitrary Axis Algorithm.
            // ACadSharp stores angles in radians and converts them to degrees when writing
            // (DxfReferenceType.IsAngle), so hand it radians — not degrees.
            Plane ocsPlane = Import.Plane(ToXYZ(text.Location), ToXYZ(normal));
            GeoVector2D dir2D = ocsPlane.Project(lineDir);
            double rotation = Math.Atan2(dir2D.y, dir2D.x);

            // A DXF TEXT is single-line by definition. Anything with explicit line breaks or
            // an automatic wrapping width has to go out as MTEXT, otherwise the line structure
            // is lost and the remaining lines end up merged into the first one.
            if (textString.IndexOf('\n') >= 0 || text.ColumnWidth > 0)
                return BuildMText(text, textString, height, textStyle, hAlign, vAlign, normal, lineDir);

            // Group 10 (InsertPoint) always holds the text anchor so viewers that ignore
            // group 11 still render text at the correct position.
            // For non-default alignment, group 11 (AlignmentPoint) is also set to the
            // same anchor — that is the convention real DXF writers use.
            // Both points are read in the OCS that the normal spans, not in world coordinates.
            XYZ anchor = WcsToOcs(text.Location, normal);
            var res = new ACadSharp.Entities.TextEntity
            {
                Value = textString,
                Height = height,
                Style = textStyle,
                HorizontalAlignment = hAlign,
                VerticalAlignment = vAlign,
                InsertPoint = anchor,
                Normal = ToXYZ(normal),
                Rotation = rotation,
            };
            // Set group 11 whenever it is meaningful. Leaving it at its (0,0,0) default while
            // groups 72/73 say it is authoritative would place the text at the origin.
            if (!defaultAlign)
                res.AlignmentPoint = anchor;
            return res;
        }

        /// <summary>
        /// Builds an MTEXT for a text that a DXF TEXT cannot represent: one with explicit line
        /// breaks or an automatic wrapping width.
        /// </summary>
        private ACadSharp.Entities.MText BuildMText(GeoObject.Text text, string textString, double height,
            ACadSharp.Tables.TextStyle textStyle, TextHorizontalAlignment hAlign,
            TextVerticalAlignmentType vAlign, GeoVector normal, GeoVector lineDir)
        {
            // MTEXT anchors the whole text block at one of nine attachment points, which is
            // exactly what CADability's Alignment/LineAlignment pair describes.
            int column;
            switch (hAlign)
            {
                case TextHorizontalAlignment.Center: column = 1; break;
                case TextHorizontalAlignment.Right: column = 2; break;
                default: column = 0; break;
            }
            int row;
            switch (vAlign)
            {
                case TextVerticalAlignmentType.Top: row = 0; break;
                case TextVerticalAlignmentType.Middle: row = 1; break;
                default: row = 2; break; // Bottom and Baseline
            }
            var res = new ACadSharp.Entities.MText
            {
                Value = ToMTextValue(textString),
                Height = height,
                Style = textStyle,
                InsertPoint = ToXYZ(text.Location),
                Normal = ToXYZ(normal),
                AttachmentPoint = (AttachmentPointType)(row * 3 + column + 1),
            };
            // For MTEXT group 11 is the X-axis direction vector, not a point, and it is what
            // carries the rotation (group 50 is not written for MTEXT). Unlike the TEXT
            // rotation it is a world vector; building it from the OCS angle mirrors the text
            // direction of every MTEXT whose normal is not (0,0,1). The insertion point of an
            // MTEXT is a world point as well, so it needs no OCS conversion.
            res.AlignmentPoint = ToXYZ(lineDir.Normalized);
            if (text.ColumnWidth > 0) res.RectangleWidth = text.ColumnWidth;
            if (text.LineSpacing > 0)
            {
                // Import stores the DXF spacing factor as 5/3 * factor (AutoCAD's MTEXT
                // baseline distance); invert that here and keep it inside the DXF range.
                double factor = text.LineSpacing * 3.0 / 5.0;
                res.LineSpacing = Math.Max(0.25, Math.Min(4.0, factor));
                // Group 73 must name a real spacing style, otherwise the factor in group 44
                // is ignored; the default (None/0) is not one the DXF reference allows here.
                res.LineSpacingStyle = LineSpacingStyleType.AtLeast;
            }
            return res;
        }

        /// <summary>
        /// Escapes a plain string for use as an MTEXT value: the MTEXT format characters have
        /// to be escaped and line breaks become the paragraph code "\P".
        /// </summary>
        private static string ToMTextValue(string value)
        {
            var sb = new System.Text.StringBuilder(value.Length + 8);
            foreach (char c in value)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '{': sb.Append("\\{"); break;
                    case '}': sb.Append("\\}"); break;
                    case '\n': sb.Append("\\P"); break;
                    case '\r': break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
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
                Entity[] ents = GeoObjectToEntity(path.Curves[i]);
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
                Entity[] ents = GeoObjectToEntity(path.Curves[i]);
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
            // The flags are what follows from the curve: closed when it ends where it starts, planar (with the normal of
            // its plane) or even linear, rational with weights. What the writing program merely declared, e.g. periodic
            // for a spline with clamped knots, is not part of the curve and cannot be written back (issue 173).
            ICurve curve = bspline;
            spline.IsClosed = bspline.IsClosed || Precision.IsEqual(curve.StartPoint, curve.EndPoint);
            if (bspline.HasWeights) spline.Flags |= SplineFlags.Rational;
            switch (curve.GetPlanarState())
            {
                case PlanarState.Planar:
                    spline.Flags |= SplineFlags.Planar;
                    GeoVector normal = curve.GetPlane().Normal;
                    // the sign is arbitrary, prefer the one pointing to positive z (then y, then x)
                    if (normal.z < 0 || (normal.z == 0 && (normal.y < 0 || (normal.y == 0 && normal.x < 0)))) normal = -normal;
                    spline.Normal = ToXYZ(normal);
                    break;
                case PlanarState.UnderDetermined: // all poles on a line
                    spline.Flags |= SplineFlags.Planar | SplineFlags.Linear;
                    break;
            }
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

        private Entity ExportConstructionLine(GeoObject.ConstructionLine constructionLine)
        {
            if (constructionLine.IsRay)
            {
                return new ACadSharp.Entities.Ray
                {
                    StartPoint = ToXYZ(constructionLine.BasePoint),
                    Direction = ToXYZ(constructionLine.Direction)
                };
            }
            return new ACadSharp.Entities.XLine
            {
                FirstPoint = ToXYZ(constructionLine.BasePoint),
                Direction = ToXYZ(constructionLine.Direction)
            };
        }

        // ARC and CIRCLE centers and TEXT points are stored in OCS, unlike ELLIPSE centers and
        // the MTEXT insertion point (WCS).
        private static XYZ WcsToOcs(GeoPoint point, GeoVector normal)
        {
            GeoVector n = normal.Normalized;
            GeoVector x = (Math.Abs(n.x) < 1.0 / 64 && Math.Abs(n.y) < 1.0 / 64)
                ? GeoVector.YAxis ^ n
                : GeoVector.ZAxis ^ n;
            x.Norm();
            GeoVector y = n ^ x;
            return new XYZ(point.x * x.x + point.y * x.y + point.z * x.z,
                point.x * y.x + point.y * y.y + point.z * y.z,
                point.x * n.x + point.y * n.y + point.z * n.z);
        }
        private Entity ExportEllipse(GeoObject.Ellipse elli)
        {
            if (elli.IsCircle)
            {
                if (elli.IsArc)
                {
                    // Always keep the arc's own normal (never flip for CW arcs).
                    // CW arcs are represented as CCW by swapping start/end endpoints,
                    // so an arc drawn in the XY plane keeps Normal=(0,0,1) and works in
                    // viewers that don't implement the OCS transformation.
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
                            Center = WcsToOcs(aligned.Center, normal),
                            Radius = aligned.Radius,
                            Normal = ToXYZ(normal)
                        };
                    }
                    else
                    {
                        return new ACadSharp.Entities.Arc
                        {
                            Center = WcsToOcs(aligned.Center, normal),
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
                        Center = WcsToOcs(elli.Center, elli.Plane.Normal),
                        Radius = elli.Radius,
                        Normal = ToXYZ(elli.Plane.Normal)
                    };
                }
            }
            else
            {
                // True ellipse. DXF group 11 must carry the real major axis (RadiusRatio ≤ 1),
                // readers derive the minor axis direction as Normal × MajorAxis, and the arc
                // always runs counterclockwise around the normal from StartParameter to
                // EndParameter.
                double majorRadius = elli.MajorRadius;
                double minorRadius = elli.MinorRadius;
                GeoVector majorDir = elli.Plane.DirectionX;
                double paramOffset = 0.0;
                if (minorRadius > majorRadius)
                {
                    // The longer radius sits on DirectionY: use that axis as the DXF major
                    // axis. In the DXF parameterization this shifts every parameter by -π/2
                    // (cos(t-π/2) = sin(t), sin(t-π/2) = -cos(t), minor dir = Normal × Y = -X).
                    majorDir = elli.Plane.DirectionY;
                    double tmp = majorRadius; majorRadius = minorRadius; minorRadius = tmp;
                    paramOffset = -Math.PI / 2.0;
                }

                double startParam, endParam;
                if (elli.IsArc)
                {
                    // A clockwise arc (negative sweep) covers the same point set as the
                    // counterclockwise arc from its end parameter to its start parameter, so
                    // only the parameters are exchanged. The normal must stay the ellipse's
                    // own normal: writing the flipped normal (as done before) mirrors the arc
                    // across the major axis, which shattered logos made of elliptical arcs.
                    startParam = elli.SweepParameter < 0
                        ? elli.StartParameter + elli.SweepParameter
                        : elli.StartParameter;
                    endParam = startParam + Math.Abs(elli.SweepParameter);
                    startParam += paramOffset;
                    endParam += paramOffset;
                    while (startParam < 0.0) { startParam += 2.0 * Math.PI; endParam += 2.0 * Math.PI; }
                    while (startParam >= 2.0 * Math.PI) { startParam -= 2.0 * Math.PI; endParam -= 2.0 * Math.PI; }
                }
                else
                {
                    startParam = 0.0;
                    endParam = 2.0 * Math.PI;
                }

                GeoVector majorAxisEnd = majorRadius * majorDir.Normalized;
                return new ACadSharp.Entities.Ellipse
                {
                    Center = ToXYZ(elli.Center),
                    MajorAxisEndPoint = ToXYZ(majorAxisEnd),
                    RadiusRatio = minorRadius / majorRadius,
                    Normal = ToXYZ(elli.Plane.Normal),
                    StartParameter = startParam,
                    EndParameter = endParam
                };
            }
        }

        private void SetColorOnEntity(Entity entity, int argb)
        {
            entity.Color = ToAcadColor(System.Drawing.Color.FromArgb(argb));
        }

        /// <summary>
        /// Converts a GDI color to an ACadSharp color. White and black become ByLayer.
        /// Colors that exactly match an entry of the AutoCAD Color Index palette are written
        /// as indexed colors (DXF group code 62, e.g. yellow -> 2): indexed colors are
        /// understood by every DXF reader, while true colors (group code 420) are ignored
        /// by older readers and many CAM systems (e.g. Trumpf TruTops), which would then
        /// fall back to the layer color and lose the technological meaning of the color.
        /// When the target version is AC1015 or older - a format that does not support true
        /// colors at all - colors without an exact palette match are converted to the nearest
        /// ACI color (squared RGB distance), exactly as AutoCAD 2000 itself stores them. This
        /// matters for GDI named colors like Color.Green (0,128,0), which are close to but not
        /// identical with a palette entry and would otherwise be invisible to ACI-only readers.
        /// Newer target versions keep the exact RGB value as a true color.
        /// </summary>
        private ACadSharp.Color ToAcadColor(Color clr)
        {
            if (clr.ToArgb() == Color.White.ToArgb() || clr.ToArgb() == Color.Black.ToArgb())
                return ACadSharp.Color.ByLayer;
            int bestIndex = 7;
            int bestDist = int.MaxValue;
            for (short i = 1; i < 256; i++)
            {
                ReadOnlySpan<byte> rgb = ACadSharp.Color.GetIndexRGB((byte)i);
                int dr = rgb[0] - clr.R;
                int dg = rgb[1] - clr.G;
                int db = rgb[2] - clr.B;
                int dist = dr * dr + dg * dg + db * db;
                if (dist == 0)
                    return new ACadSharp.Color(i);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIndex = i;
                }
            }
            if (doc.Header.Version <= ACadVersion.AC1015)
                return new ACadSharp.Color((short)bestIndex);
            return new ACadSharp.Color(clr.R, clr.G, clr.B);
        }

        private void SetAttributes(Entity entity, IGeoObject go)
        {
            // DXF group 60 carries the visibility flag. Import maps it to IsVisible, so it
            // has to be written back here — otherwise objects that were deliberately hidden
            // in the source drawing (construction lines, reference points) reappear on export.
            entity.IsInvisible = !go.IsVisible;
            if (go is IColorDef cd && cd.ColorDef != null)
            {
                entity.Color = ToAcadColor(cd.ColorDef.Color);
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
