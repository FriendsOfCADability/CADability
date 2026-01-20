using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using ACadSharp.Tables.Collections;
using ACadSharp.XData;
using AciColor = ACadSharp.Color;
using DrawingColor = System.Drawing.Color;

namespace CADability.DXF.Adapters
{
    /// <summary>
    /// ACadSharp library implementation of IDxfLibrary.
    /// Supports both DXF and DWG file formats.
    /// </summary>
    public class ACadSharpLibraryAdapter : IDxfLibrary
    {
        private readonly ACadSharpEntityFactory entityFactory;

        public ACadSharpLibraryAdapter()
        {
            entityFactory = new ACadSharpEntityFactory();
        }

        public string LibraryName => "ACadSharp";

        public IDxfEntityFactory EntityFactory => entityFactory;

        public bool CanImportVersion(string fileName)
        {
            try
            {
                if (!File.Exists(fileName))
                    return false;

                // ACadSharp supports both DXF and DWG
                string ext = Path.GetExtension(fileName).ToLowerInvariant();
                return ext == ".dxf" || ext == ".dwg";
            }
            catch
            {
                return false;
            }
        }

        public IDxfDocument LoadFromStream(Stream stream)
        {
            CadDocument doc;
            using (var reader = new DxfReader(stream))
            {
                doc = reader.Read();
            }
            
            return new ACadSharpDocumentAdapter(doc);
        }

        public IDxfDocument LoadFromFile(string fileName)
        {
            string ext = Path.GetExtension(fileName).ToLowerInvariant();
            
            CadDocument doc;
            if (ext == ".dwg")
            {
                doc = DwgReader.Read(fileName, notification: null);
            }
            else // .dxf
            {
                doc = DxfReader.Read(fileName, notification: null);
            }
            
            return new ACadSharpDocumentAdapter(doc);
        }

        public IDxfDocument CreateDocument()
        {
            var doc = new CadDocument();
            doc.CreateDefaults();
            return new ACadSharpDocumentAdapter(doc);
        }
    }

    /// <summary>
    /// ACadSharp CadDocument adapter.
    /// </summary>
    internal class ACadSharpDocumentAdapter : IDxfDocument
    {
        private readonly CadDocument doc;

        public ACadSharpDocumentAdapter(CadDocument doc)
        {
            this.doc = doc;
        }

        public IDxfBlockCollection Blocks => new ACadSharpBlockCollectionAdapter(doc.BlockRecords, this);

        public IEnumerable<IDxfLayer> Layers => doc.Layers.Select(l => new ACadSharpLayerAdapter(l));

        public IEnumerable<IDxfLineType> LineTypes => doc.LineTypes.Select(lt => new ACadSharpLineTypeAdapter(lt));

        public string Name
        {
            get { return null; } // ACadSharp CadHeader doesn't have a DrawingName property
            set { /* No equivalent in ACadSharp */ }
        }

        public IEnumerable<IDxfEntity> Entities
        {
            get
            {
                var result = new List<IDxfEntity>();
                // Get all entities from the model space
                if (doc.ModelSpace != null)
                {
                    foreach (var e in doc.ModelSpace.Entities)
                    {
                        if (e is Entity entity)
                        {
                            result.Add(WrapEntity(entity));
                        }
                    }
                }
                return result;
            }
        }

        public void SaveToFile(string fileName)
        {
            string ext = Path.GetExtension(fileName).ToLowerInvariant();
            
            if (ext == ".dwg")
            {
                using (var writer = new DwgWriter(fileName, doc))
                {
                    writer.Write();
                }
            }
            else // .dxf
            {
                DxfWriter.Write(fileName, doc, binary: false, configuration: null, notification: null);
            }
        }

        public void SaveToStream(Stream stream)
        {
            // ACadSharp DxfWriter for streams
            using (var writer = new DxfWriter(stream, doc, binary: false))
            {
                writer.Write();
            }
        }

        public void AddEntity(IDxfEntity entity)
        {
            if (entity is ACadSharpEntityAdapter adapter)
            {
                doc.Entities.Add(adapter.WrappedEntity);
            }
        }

        public void AddEntities(params IDxfEntity[] entities)
        {
            foreach (var entity in entities)
            {
                AddEntity(entity);
            }
        }

        internal IDxfEntity WrapEntity(Entity entity)
        {
            if (entity is Line line)
                return new ACadSharpLineAdapter(line);
            else if (entity is Arc arc)
                return new ACadSharpArcAdapter(arc);
            else if (entity is Circle circle)
                return new ACadSharpCircleAdapter(circle);
            else if (entity is Ellipse ellipse)
                return new ACadSharpEllipseAdapter(ellipse);
            else if (entity is Spline spline)
                return new ACadSharpSplineAdapter(spline);
            else if (entity is LwPolyline lwpoly)
                return new ACadSharpPolyline2DAdapter(lwpoly);
            else if (entity is Polyline3D poly3d)
                return new ACadSharpPolyline3DAdapter(poly3d);
            else if (entity is TextEntity text)
                return new ACadSharpTextAdapter(text);
            else if (entity is MText mtext)
                return new ACadSharpMTextAdapter(mtext);
            else if (entity is Hatch hatch)
                return new ACadSharpHatchAdapter(hatch);
            else if (entity is Insert insert)
                return new ACadSharpInsertAdapter(insert, this);
            else if (entity is Face3D face3d)
                return new ACadSharpFace3DAdapter(face3d);
            else if (entity is PolyfaceMesh pfm)
                return new ACadSharpPolyfaceMeshAdapter(pfm);
            else if (entity is Solid solid)
                return new ACadSharpSolidAdapter(solid);
            else if (entity is Point point)
                return new ACadSharpPointAdapter(point);
            else if (entity is MLine mline)
                return new ACadSharpMLineAdapter(mline);
            else if (entity is Mesh mesh)
                return new ACadSharpMeshAdapter(mesh);
            else if (entity is Dimension dim)
                return new ACadSharpDimensionAdapter(dim, this);
            else if (entity is Leader leader)
                return new ACadSharpLeaderAdapter(leader, this);
            else if (entity is Ray ray)
                return new ACadSharpRayAdapter(ray);
            else
                return new ACadSharpEntityAdapter(entity);
        }
    }

    /// <summary>
    /// ACadSharp block collection adapter.
    /// </summary>
    internal class ACadSharpBlockCollectionAdapter : IDxfBlockCollection
    {
        private readonly BlockRecordsTable blocks;
        private readonly ACadSharpDocumentAdapter document;

        public ACadSharpBlockCollectionAdapter(BlockRecordsTable blocks, ACadSharpDocumentAdapter document)
        {
            this.blocks = blocks;
            this.document = document;
        }

        public IDxfBlock GetBlock(string name)
        {
            if (blocks.TryGetValue(name, out BlockRecord block))
            {
                return new ACadSharpBlockAdapter(block, document);
            }
            return null;
        }

        public IEnumerable<IDxfEntity> GetBlockEntities(string blockName)
        {
            if (blocks.TryGetValue(blockName, out BlockRecord block))
            {
                return block.Entities.OfType<Entity>().Select(e => document.WrapEntity(e));
            }
            return Enumerable.Empty<IDxfEntity>();
        }

        public void AddBlock(IDxfBlock block)
        {
            if (block is ACadSharpBlockAdapter adapter)
            {
                blocks.Add(adapter.WrappedBlock);
            }
        }
    }

    /// <summary>
    /// ACadSharp block adapter.
    /// </summary>
    internal class ACadSharpBlockAdapter : IDxfBlock
    {
        private readonly BlockRecord block;
        private readonly ACadSharpDocumentAdapter document;

        public ACadSharpBlockAdapter(BlockRecord block, ACadSharpDocumentAdapter document)
        {
            this.block = block;
            this.document = document;
        }

        public BlockRecord WrappedBlock => block;

        public string Name => block.Name;
        
        public (double X, double Y, double Z) Origin
        {
            get
            {
                if (block.BlockEntity != null)
                {
                    var origin = block.BlockEntity.BasePoint;
                    return (origin.X, origin.Y, origin.Z);
                }
                return (0, 0, 0);
            }
        }
        
        public IEnumerable<IDxfEntity> Entities => block.Entities.OfType<Entity>().Select(e => document.WrapEntity(e));
        
        public string Handle => block.Handle.ToString();
    }

    /// <summary>
    /// ACadSharp layer adapter.
    /// </summary>
    internal class ACadSharpLayerAdapter : IDxfLayer
    {
        private readonly Layer layer;

        public ACadSharpLayerAdapter(Layer layer)
        {
            this.layer = layer;
        }

        public string Name => layer.Name;
        
        public int ColorArgb
        {
            get
            {
                if (layer.Color.IsTrueColor)
                {
                    return layer.Color.TrueColor;
                }
                else
                {
                    // Convert ACI color to RGB
                    return GetAciColor(layer.Color.Index).ToArgb();
                }
            }
        }
        
        public int LineWeight => (int)layer.LineWeight;
        
        public string LineTypeName => layer.LineType?.Name ?? "Continuous";

        private DrawingColor GetAciColor(short index)
        {
            // Basic ACI color mapping (simplified)
            if (index == 0) return DrawingColor.FromArgb(255, 255, 255, 255); // ByBlock
            if (index == 256) return DrawingColor.FromArgb(255, 255, 255, 255); // ByLayer
            if (index == 7) return DrawingColor.FromArgb(255, 255, 255, 255); // White/Black
            if (index == 1) return DrawingColor.FromArgb(255, 255, 0, 0); // Red
            if (index == 2) return DrawingColor.FromArgb(255, 255, 255, 0); // Yellow
            if (index == 3) return DrawingColor.FromArgb(255, 0, 255, 0); // Green
            if (index == 4) return DrawingColor.FromArgb(255, 0, 255, 255); // Cyan
            if (index == 5) return DrawingColor.FromArgb(255, 0, 0, 255); // Blue
            if (index == 6) return DrawingColor.FromArgb(255, 255, 0, 255); // Magenta
            
            return DrawingColor.FromArgb(255, 255, 255, 255); // Default to white
        }
    }

    /// <summary>
    /// ACadSharp line type adapter.
    /// </summary>
    internal class ACadSharpLineTypeAdapter : IDxfLineType
    {
        private readonly LineType linetype;

        public ACadSharpLineTypeAdapter(LineType linetype)
        {
            this.linetype = linetype;
        }

        public string Name => linetype.Name;

        public double[] Segments
        {
            get
            {
                // ACadSharp LineType has Segments property with pattern data
                // For now, return empty array - would need to extract from PatternLength
                return Array.Empty<double>();
            }
        }
    }

    /// <summary>
    /// Base ACadSharp entity adapter.
    /// </summary>
    internal class ACadSharpEntityAdapter : IDxfEntity
    {
        protected readonly Entity entity;

        public ACadSharpEntityAdapter(Entity entity)
        {
            this.entity = entity;
        }

        public Entity WrappedEntity => entity;

        public virtual DxfEntityType EntityType => DxfEntityType.Unknown;
        
        public virtual string LayerName
        {
            get { return entity.Layer?.Name; }
            set { /* Setting layer requires finding or creating layer in document */ }
        }

        public virtual int? ColorArgb
        {
            get
            {
                if (entity.Color.IsTrueColor)
                {
                    return entity.Color.TrueColor;
                }
                return null; // ByLayer or ByBlock
            }
            set { /* Would need to set entity.Color */ }
        }

        public virtual string LineTypeName
        {
            get { return entity.LineType?.Name; }
            set { /* Would need to set entity.LineType */ }
        }

        public virtual int? LineWeight
        {
            get { return (int)entity.LineWeight; }
            set { /* Would need to set entity.LineWeight */ }
        }

        public virtual string Handle => entity.Handle.ToString();
        
        public virtual bool IsVisible
        {
            get { return !entity.IsInvisible; }
            set { entity.IsInvisible = !value; }
        }

        public virtual IEnumerable<IDxfXData> XData
        {
            get
            {
                // ACadSharp has ExtendedData property
                if (entity.ExtendedData != null)
                {
                    return entity.ExtendedData.Select(kvp => new ACadSharpXDataAdapter(kvp.Key.Name, kvp.Value));
                }
                return Enumerable.Empty<IDxfXData>();
            }
        }
    }

    internal class ACadSharpXDataAdapter : IDxfXData
    {
        private readonly string appName;
        private readonly ExtendedData xdata;

        public ACadSharpXDataAdapter(string appName, ExtendedData xdata)
        {
            this.appName = appName;
            this.xdata = xdata;
        }

        public string ApplicationName => appName;

        public IEnumerable<(int Code, object Value)> Records
        {
            get
            {
                // ExtendedData would need to be enumerated - simplified for now
                return Enumerable.Empty<(int, object)>();
            }
        }
    }

    internal class ACadSharpLineAdapter : ACadSharpEntityAdapter, IDxfLine
    {
        private readonly Line line;

        public ACadSharpLineAdapter(Line line) : base(line)
        {
            this.line = line;
        }

        public override DxfEntityType EntityType => DxfEntityType.Line;
        public (double X, double Y, double Z) StartPoint => (line.StartPoint.X, line.StartPoint.Y, line.StartPoint.Z);
        public (double X, double Y, double Z) EndPoint => (line.EndPoint.X, line.EndPoint.Y, line.EndPoint.Z);
        public double Thickness => line.Thickness;
        public (double X, double Y, double Z) Normal => (line.Normal.X, line.Normal.Y, line.Normal.Z);
    }

    internal class ACadSharpRayAdapter : ACadSharpEntityAdapter, IDxfRay
    {
        private readonly Ray ray;

        public ACadSharpRayAdapter(Ray ray) : base(ray)
        {
            this.ray = ray;
        }

        public override DxfEntityType EntityType => DxfEntityType.Ray;
        public (double X, double Y, double Z) Origin => (ray.StartPoint.X, ray.StartPoint.Y, ray.StartPoint.Z);
        public (double X, double Y, double Z) Direction => (ray.Direction.X, ray.Direction.Y, ray.Direction.Z);
    }

    internal class ACadSharpArcAdapter : ACadSharpEntityAdapter, IDxfArc
    {
        private readonly Arc arc;

        public ACadSharpArcAdapter(Arc arc) : base(arc)
        {
            this.arc = arc;
        }

        public override DxfEntityType EntityType => DxfEntityType.Arc;
        public (double X, double Y, double Z) Center => (arc.Center.X, arc.Center.Y, arc.Center.Z);
        public double Radius => arc.Radius;
        public double StartAngle => arc.StartAngle;
        public double EndAngle => arc.EndAngle;
        public (double X, double Y, double Z) Normal => (arc.Normal.X, arc.Normal.Y, arc.Normal.Z);
        public double Thickness => arc.Thickness;
    }

    internal class ACadSharpCircleAdapter : ACadSharpEntityAdapter, IDxfCircle
    {
        private readonly Circle circle;

        public ACadSharpCircleAdapter(Circle circle) : base(circle)
        {
            this.circle = circle;
        }

        public override DxfEntityType EntityType => DxfEntityType.Circle;
        public (double X, double Y, double Z) Center => (circle.Center.X, circle.Center.Y, circle.Center.Z);
        public double Radius => circle.Radius;
        public (double X, double Y, double Z) Normal => (circle.Normal.X, circle.Normal.Y, circle.Normal.Z);
        public double Thickness => circle.Thickness;
    }

    internal class ACadSharpEllipseAdapter : ACadSharpEntityAdapter, IDxfEllipse
    {
        private readonly Ellipse ellipse;

        public ACadSharpEllipseAdapter(Ellipse ellipse) : base(ellipse)
        {
            this.ellipse = ellipse;
        }

        public override DxfEntityType EntityType => DxfEntityType.Ellipse;
        public (double X, double Y, double Z) Center => (ellipse.Center.X, ellipse.Center.Y, ellipse.Center.Z);
        
        public (double X, double Y, double Z) MajorAxisEnd
        {
            get
            {
                var endPoint = ellipse.MajorAxisEndPoint;
                return (endPoint.X, endPoint.Y, endPoint.Z);
            }
        }
        
        public double MinorAxisRatio => ellipse.RadiusRatio;
        public double StartAngle => ellipse.StartParameter;
        public double EndAngle => ellipse.EndParameter;
        public (double X, double Y, double Z) Normal => (ellipse.Normal.X, ellipse.Normal.Y, ellipse.Normal.Z);
    }

    internal class ACadSharpSplineAdapter : ACadSharpEntityAdapter, IDxfSpline
    {
        private readonly Spline spline;

        public ACadSharpSplineAdapter(Spline spline) : base(spline)
        {
            this.spline = spline;
        }

        public override DxfEntityType EntityType => DxfEntityType.Spline;
        public int Degree => spline.Degree;

        public (double X, double Y, double Z)[] ControlPoints =>
            spline.ControlPoints.Select(cp => (cp.X, cp.Y, cp.Z)).ToArray();

        public double[] Weights => spline.Weights.ToArray();
        public double[] Knots => spline.Knots.ToArray();

        public IEnumerable<(double X, double Y, double Z)> FitPoints =>
            spline.FitPoints.Select(fp => (fp.X, fp.Y, fp.Z));

        public bool IsClosed => spline.IsClosed;
        public bool IsClosedPeriodic => spline.IsPeriodic;
    }

    internal class ACadSharpPolyline2DAdapter : ACadSharpEntityAdapter, IDxfPolyline2D
    {
        private readonly LwPolyline polyline;

        public ACadSharpPolyline2DAdapter(LwPolyline polyline) : base(polyline)
        {
            this.polyline = polyline;
        }

        public override DxfEntityType EntityType => DxfEntityType.Polyline2D;
        
        public IEnumerable<IDxfVertex> Vertices
        {
            get
            {
                foreach (var vertex in polyline.Vertices)
                {
                    yield return new ACadSharpVertexAdapter(vertex);
                }
            }
        }
        
        public bool IsClosed => polyline.Flags.HasFlag(LwPolylineFlags.Closed);

        public IEnumerable<IDxfEntity> Explode()
        {
            // ACadSharp doesn't have built-in explode, would need to implement
            // For now, return empty - could create line/arc segments from vertices
            return Enumerable.Empty<IDxfEntity>();
        }
    }

    internal class ACadSharpVertexAdapter : IDxfVertex
    {
        private readonly LwPolyline.Vertex vertex;

        public ACadSharpVertexAdapter(LwPolyline.Vertex vertex)
        {
            this.vertex = vertex;
        }

        public (double X, double Y) Position => (vertex.Location.X, vertex.Location.Y);
        public double Bulge => vertex.Bulge;
    }

    internal class ACadSharpTextAdapter : ACadSharpEntityAdapter, IDxfText
    {
        private readonly TextEntity text;

        public ACadSharpTextAdapter(TextEntity text) : base(text)
        {
            this.text = text;
        }

        public override DxfEntityType EntityType => DxfEntityType.Text;
        public string Value => text.Value;
        public (double X, double Y, double Z) Position => (text.InsertPoint.X, text.InsertPoint.Y, text.InsertPoint.Z);
        public double Height => text.Height;
        public double Rotation => text.Rotation;
        public double WidthFactor => text.WidthFactor;
        public (double X, double Y, double Z) Normal => (text.Normal.X, text.Normal.Y, text.Normal.Z);
        public string StyleName => text.Style?.Name;
        public string FontName => text.Style?.Filename ?? text.Style?.Name;
        public bool IsBold => text.Style?.TrueType.HasFlag(FontFlags.Bold) ?? false;
        public bool IsItalic => text.Style?.TrueType.HasFlag(FontFlags.Italic) ?? false;
    }

    internal class ACadSharpMTextAdapter : ACadSharpEntityAdapter, IDxfMText
    {
        private readonly MText mtext;

        public ACadSharpMTextAdapter(MText mtext) : base(mtext)
        {
            this.mtext = mtext;
        }

        public override DxfEntityType EntityType => DxfEntityType.MText;
        public string PlainText => mtext.PlainText;
        public (double X, double Y, double Z) Position => (mtext.InsertPoint.X, mtext.InsertPoint.Y, mtext.InsertPoint.Z);
        public double Height => mtext.Height;
        public double Rotation => mtext.Rotation;
        public (double X, double Y, double Z) Normal => (mtext.Normal.X, mtext.Normal.Y, mtext.Normal.Z);
        public string StyleName => mtext.Style?.Name;
    }

    internal class ACadSharpHatchAdapter : ACadSharpEntityAdapter, IDxfHatch
    {
        private readonly Hatch hatch;

        public ACadSharpHatchAdapter(Hatch hatch) : base(hatch)
        {
            this.hatch = hatch;
        }

        public override DxfEntityType EntityType => DxfEntityType.Hatch;
        
        public IEnumerable<IDxfHatchBoundaryPath> BoundaryPaths =>
            hatch.Paths.Select(p => new ACadSharpHatchBoundaryPathAdapter(p));
        
        public IDxfHatchPattern Pattern => new ACadSharpHatchPatternAdapter(hatch);
        
        public (double X, double Y, double Z) Normal => (hatch.Normal.X, hatch.Normal.Y, hatch.Normal.Z);
    }

    internal class ACadSharpHatchBoundaryPathAdapter : IDxfHatchBoundaryPath
    {
        private readonly Hatch.BoundaryPath path;

        public ACadSharpHatchBoundaryPathAdapter(Hatch.BoundaryPath path)
        {
            this.path = path;
        }

        public IEnumerable<IDxfEntity> Edges
        {
            get
            {
                // Would need to convert boundary edges to entities
                // Simplified for now
                return Enumerable.Empty<IDxfEntity>();
            }
        }
    }

    internal class ACadSharpHatchPatternAdapter : IDxfHatchPattern
    {
        private readonly Hatch hatch;

        public ACadSharpHatchPatternAdapter(Hatch hatch)
        {
            this.hatch = hatch;
        }

        public HatchFillType FillType
        {
            get
            {
                if (hatch.IsSolid)
                    return HatchFillType.SolidFill;
                else if (hatch.PatternType == HatchPatternType.PatternFill || hatch.PatternType == HatchPatternType.Custom)
                    return HatchFillType.PatternFill;
                else
                    return HatchFillType.PatternFill;
            }
        }

        public IEnumerable<IDxfHatchLineDefinition> LineDefinitions
        {
            get
            {
                // Would need to extract pattern lines from hatch.Pattern
                // Simplified for now
                return Enumerable.Empty<IDxfHatchLineDefinition>();
            }
        }
    }

    internal class ACadSharpInsertAdapter : ACadSharpEntityAdapter, IDxfInsert
    {
        private readonly Insert insert;
        private readonly ACadSharpDocumentAdapter document;

        public ACadSharpInsertAdapter(Insert insert, ACadSharpDocumentAdapter document) : base(insert)
        {
            this.insert = insert;
            this.document = document;
        }

        public override DxfEntityType EntityType => DxfEntityType.Insert;
        public string BlockName => insert.Block?.Name;
        public (double X, double Y, double Z) Position => (insert.InsertPoint.X, insert.InsertPoint.Y, insert.InsertPoint.Z);
        public double Rotation => insert.Rotation;
        public (double X, double Y, double Z) Scale => (insert.XScale, insert.YScale, insert.ZScale);
        public IDxfBlock Block => insert.Block != null ? new ACadSharpBlockAdapter(insert.Block, document) : null;
    }

    internal class ACadSharpFace3DAdapter : ACadSharpEntityAdapter, IDxfFace3D
    {
        private readonly Face3D face;

        public ACadSharpFace3DAdapter(Face3D face) : base(face)
        {
            this.face = face;
        }

        public override DxfEntityType EntityType => DxfEntityType.Face3D;
        public (double X, double Y, double Z) FirstVertex => (face.FirstCorner.X, face.FirstCorner.Y, face.FirstCorner.Z);
        public (double X, double Y, double Z) SecondVertex => (face.SecondCorner.X, face.SecondCorner.Y, face.SecondCorner.Z);
        public (double X, double Y, double Z) ThirdVertex => (face.ThirdCorner.X, face.ThirdCorner.Y, face.ThirdCorner.Z);
        public (double X, double Y, double Z) FourthVertex => (face.FourthCorner.X, face.FourthCorner.Y, face.FourthCorner.Z);
    }

    internal class ACadSharpPolyline3DAdapter : ACadSharpEntityAdapter, IDxfPolyline3D
    {
        private readonly Polyline3D polyline3d;

        public ACadSharpPolyline3DAdapter(Polyline3D polyline3d) : base(polyline3d)
        {
            this.polyline3d = polyline3d;
        }

        public override DxfEntityType EntityType => DxfEntityType.Polyline3D;
        
        public IEnumerable<(double X, double Y, double Z)> Vertices =>
            polyline3d.Vertices.Select(v => (v.Location.X, v.Location.Y, v.Location.Z));
        
        public bool IsClosed => polyline3d.Flags.HasFlag(PolylineFlags.ClosedPolylineOrClosedPolygonMeshInM);
    }

    internal class ACadSharpSolidAdapter : ACadSharpEntityAdapter, IDxfSolid
    {
        private readonly Solid solid;

        public ACadSharpSolidAdapter(Solid solid) : base(solid)
        {
            this.solid = solid;
        }

        public override DxfEntityType EntityType => DxfEntityType.Solid;
        public (double X, double Y) FirstVertex => (solid.FirstCorner.X, solid.FirstCorner.Y);
        public (double X, double Y) SecondVertex => (solid.SecondCorner.X, solid.SecondCorner.Y);
        public (double X, double Y) ThirdVertex => (solid.ThirdCorner.X, solid.ThirdCorner.Y);
        public (double X, double Y) FourthVertex => (solid.FourthCorner.X, solid.FourthCorner.Y);
        public (double X, double Y, double Z) Normal => (solid.Normal.X, solid.Normal.Y, solid.Normal.Z);
        public double Elevation => solid.Thickness; // Using thickness as elevation approximation
    }

    internal class ACadSharpPointAdapter : ACadSharpEntityAdapter, IDxfPoint
    {
        private readonly Point point;

        public ACadSharpPointAdapter(Point point) : base(point)
        {
            this.point = point;
        }

        public override DxfEntityType EntityType => DxfEntityType.Point;
        public (double X, double Y, double Z) Position => (point.Location.X, point.Location.Y, point.Location.Z);
    }

    internal class ACadSharpMLineAdapter : ACadSharpEntityAdapter, IDxfMLine
    {
        private readonly MLine mline;

        public ACadSharpMLineAdapter(MLine mline) : base(mline)
        {
            this.mline = mline;
        }

        public override DxfEntityType EntityType => DxfEntityType.MLine;

        public IEnumerable<IDxfEntity> Explode()
        {
            // ACadSharp doesn't have built-in explode for MLine
            return Enumerable.Empty<IDxfEntity>();
        }
    }

    internal class ACadSharpPolyfaceMeshAdapter : ACadSharpEntityAdapter, IDxfPolyfaceMesh
    {
        private readonly PolyfaceMesh mesh;

        public ACadSharpPolyfaceMeshAdapter(PolyfaceMesh mesh) : base(mesh)
        {
            this.mesh = mesh;
        }

        public override DxfEntityType EntityType => DxfEntityType.PolyfaceMesh;
        
        public (double X, double Y, double Z)[] Vertices =>
            mesh.Vertices.Select(v => (v.Location.X, v.Location.Y, v.Location.Z)).ToArray();
        
        public IEnumerable<short[]> Faces
        {
            get
            {
                // PolyfaceMesh in ACadSharp stores faces differently
                // Would need to extract face vertex indices
                return Enumerable.Empty<short[]>();
            }
        }

        public void Explode()
        {
            // No action needed for this method
        }
    }

    internal class ACadSharpMeshAdapter : ACadSharpEntityAdapter, IDxfMesh
    {
        private readonly Mesh mesh;

        public ACadSharpMeshAdapter(Mesh mesh) : base(mesh)
        {
            this.mesh = mesh;
        }

        public override DxfEntityType EntityType => DxfEntityType.Mesh;
        
        public IEnumerable<(double X, double Y, double Z)> Vertices =>
            mesh.Vertices.Select(v => (v.X, v.Y, v.Z));
        
        public IEnumerable<int[]> Faces => mesh.Faces.Select(f => f.ToArray());
    }

    internal class ACadSharpDimensionAdapter : ACadSharpEntityAdapter, IDxfDimension
    {
        private readonly Dimension dimension;
        private readonly ACadSharpDocumentAdapter document;

        public ACadSharpDimensionAdapter(Dimension dimension, ACadSharpDocumentAdapter document)
            : base(dimension)
        {
            this.dimension = dimension;
            this.document = document;
        }

        public override DxfEntityType EntityType => DxfEntityType.Dimension;
        
        public IDxfBlock DimensionBlock
        {
            get
            {
                // ACadSharp Dimension has Block property
                if (dimension.Block != null)
                {
                    return new ACadSharpBlockAdapter(dimension.Block, document);
                }
                return null;
            }
        }
    }

    internal class ACadSharpLeaderAdapter : ACadSharpEntityAdapter, IDxfLeader
    {
        private readonly Leader leader;
        private readonly ACadSharpDocumentAdapter document;

        public ACadSharpLeaderAdapter(Leader leader, ACadSharpDocumentAdapter document)
            : base(leader)
        {
            this.leader = leader;
            this.document = document;
        }

        public override DxfEntityType EntityType => DxfEntityType.Leader;
        
        public IEnumerable<(double X, double Y)> Vertices =>
            leader.Vertices.Select(v => (v.X, v.Y));
        
        public (double X, double Y, double Z) Normal => (leader.Normal.X, leader.Normal.Y, leader.Normal.Z);
        
        public double Elevation => 0; // ACadSharp Leader doesn't have explicit elevation
        
        public IDxfEntity Annotation
        {
            get
            {
                if (leader.AssociatedAnnotation != null)
                {
                    return document.WrapEntity(leader.AssociatedAnnotation);
                }
                return null;
            }
        }
    }

    /// <summary>
    /// ACadSharp implementation of entity factory for creating DXF entities.
    /// </summary>
    internal class ACadSharpEntityFactory : IDxfEntityFactory
    {
        public IDxfEntity CreateLine((double X, double Y, double Z) start, (double X, double Y, double Z) end)
        {
            var line = new Line(new CSMath.XYZ(start.X, start.Y, start.Z), new CSMath.XYZ(end.X, end.Y, end.Z));
            return new ACadSharpLineAdapter(line);
        }

        public IDxfEntity CreateArc((double X, double Y, double Z) center, double radius, double startAngle, double endAngle, (double X, double Y, double Z) normal)
        {
            var arc = new Arc
            {
                Center = new CSMath.XYZ(center.X, center.Y, center.Z),
                Radius = radius,
                StartAngle = startAngle,
                EndAngle = endAngle,
                Normal = new CSMath.XYZ(normal.X, normal.Y, normal.Z)
            };
            return new ACadSharpArcAdapter(arc);
        }

        public IDxfEntity CreateCircle((double X, double Y, double Z) center, double radius, (double X, double Y, double Z) normal)
        {
            var circle = new Circle
            {
                Center = new CSMath.XYZ(center.X, center.Y, center.Z),
                Radius = radius,
                Normal = new CSMath.XYZ(normal.X, normal.Y, normal.Z)
            };
            return new ACadSharpCircleAdapter(circle);
        }

        public IDxfEntity CreateEllipse((double X, double Y, double Z) center, double majorAxis, double minorAxis, double rotation, (double X, double Y, double Z) normal)
        {
            // Calculate major axis end point from rotation
            double majorAxisX = (majorAxis / 2.0) * Math.Cos(rotation * Math.PI / 180.0);
            double majorAxisY = (majorAxis / 2.0) * Math.Sin(rotation * Math.PI / 180.0);
            
            var ellipse = new Ellipse
            {
                Center = new CSMath.XYZ(center.X, center.Y, center.Z),
                MajorAxisEndPoint = new CSMath.XYZ(majorAxisX, majorAxisY, 0),
                RadiusRatio = minorAxis / majorAxis,
                Normal = new CSMath.XYZ(normal.X, normal.Y, normal.Z)
            };
            return new ACadSharpEllipseAdapter(ellipse);
        }

        public IDxfEntity CreatePoint((double X, double Y, double Z) location)
        {
            var point = new Point(new CSMath.XYZ(location.X, location.Y, location.Z));
            return new ACadSharpPointAdapter(point);
        }

        public IDxfEntity CreateText(string value, (double X, double Y, double Z) position, double height)
        {
            var text = new TextEntity
            {
                Value = value,
                InsertPoint = new CSMath.XYZ(position.X, position.Y, position.Z),
                Height = height
            };
            return new ACadSharpTextAdapter(text);
        }

        public IDxfEntity CreateSpline((double X, double Y, double Z)[] controlPoints, double[] weights, double[] knots, int degree, bool isClosed)
        {
            var spline = new Spline
            {
                Degree = degree,
                IsClosed = isClosed
            };
            
            foreach (var cp in controlPoints)
            {
                spline.ControlPoints.Add(new CSMath.XYZ(cp.X, cp.Y, cp.Z));
            }
            
            if (weights != null)
            {
                foreach (var w in weights)
                {
                    spline.Weights.Add(w);
                }
            }
            
            if (knots != null)
            {
                foreach (var k in knots)
                {
                    spline.Knots.Add(k);
                }
            }
            
            return new ACadSharpSplineAdapter(spline);
        }

        public IDxfEntity CreatePolyline3D((double X, double Y, double Z)[] vertices, bool isClosed)
        {
            var polyline = new Polyline3D();
            foreach (var v in vertices)
            {
                polyline.Vertices.Add(new Vertex3D(new CSMath.XYZ(v.X, v.Y, v.Z)));
            }
            
            if (isClosed)
            {
                polyline.Flags |= PolylineFlags.ClosedPolylineOrClosedPolygonMeshInM;
            }
            
            return new ACadSharpPolyline3DAdapter(polyline);
        }

        public IDxfEntity CreatePolyfaceMesh((double X, double Y, double Z)[] vertices, short[][] faces)
        {
            // PolyfaceMesh creation in ACadSharp is complex
            // For now, create an empty mesh - would need proper ACadSharp implementation
            var mesh = new PolyfaceMesh();
            // ACadSharp PolyfaceMesh vertex creation is different from the documentation
            // This would need to be implemented based on ACadSharp's actual API
            return new ACadSharpPolyfaceMeshAdapter(mesh);
        }

        public IDxfEntity CreateMesh((double X, double Y, double Z)[] vertices, int[][] faces)
        {
            var mesh = new Mesh();
            foreach (var v in vertices)
            {
                mesh.Vertices.Add(new CSMath.XYZ(v.X, v.Y, v.Z));
            }
            
            foreach (var face in faces)
            {
                mesh.Faces.Add(face);
            }
            
            return new ACadSharpMeshAdapter(mesh);
        }

        public IDxfBlock CreateBlock(string name, IDxfEntity[] entities)
        {
            var block = new BlockRecord(name);
            foreach (var entity in entities)
            {
                if (entity is ACadSharpEntityAdapter adapter)
                {
                    block.Entities.Add(adapter.WrappedEntity);
                }
            }
            return new ACadSharpBlockAdapter(block, null);
        }

        public IDxfEntity CreateInsert(IDxfBlock block, (double X, double Y, double Z) position)
        {
            if (block is ACadSharpBlockAdapter blockAdapter)
            {
                var insert = new Insert(blockAdapter.WrappedBlock)
                {
                    InsertPoint = new CSMath.XYZ(position.X, position.Y, position.Z)
                };
                return new ACadSharpInsertAdapter(insert, null);
            }
            throw new ArgumentException("Block must be an ACadSharpBlockAdapter", nameof(block));
        }
    }
}
