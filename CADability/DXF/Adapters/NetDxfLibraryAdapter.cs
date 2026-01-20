using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using netDxf;
using netDxf.Blocks;
using netDxf.Entities;
using netDxf.Tables;

namespace CADability.DXF.Adapters
{
    /// <summary>
    /// netDxf library implementation of IDxfLibrary.
    /// </summary>
    public class NetDxfLibraryAdapter : IDxfLibrary
    {
        public string LibraryName => "netDxf";

        public bool CanImportVersion(string fileName)
        {
            try
            {
                netDxf.Header.DxfVersion ver = DxfDocument.CheckDxfFileVersion(fileName, out bool _);
                return ver >= netDxf.Header.DxfVersion.AutoCad2000;
            }
            catch
            {
                return false;
            }
        }

        public IDxfDocument LoadFromStream(Stream stream)
        {
            MathHelper.Epsilon = 1e-8;
            netDxf.DxfDocument doc = DxfDocument.Load(stream);
            return new NetDxfDocumentAdapter(doc);
        }

        public IDxfDocument LoadFromFile(string fileName)
        {
            using (Stream stream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return LoadFromStream(stream);
            }
        }

        public IDxfDocument CreateDocument()
        {
            return new NetDxfDocumentAdapter(new DxfDocument(netDxf.Header.DxfVersion.AutoCad2000));
        }
    }

    /// <summary>
    /// netDxf DxfDocument adapter.
    /// </summary>
    internal class NetDxfDocumentAdapter : IDxfDocument
    {
        private readonly DxfDocument doc;

        public NetDxfDocumentAdapter(DxfDocument doc)
        {
            this.doc = doc;
        }

        public IDxfBlockCollection Blocks => new NetDxfBlockCollectionAdapter(doc.Blocks);

        public IEnumerable<IDxfLayer> Layers => doc.Layers.Select(l => new NetDxfLayerAdapter(l));

        public IEnumerable<IDxfLineType> LineTypes => doc.Linetypes.Select(lt => new NetDxfLineTypeAdapter(lt));

        public string Name
        {
            get { return doc.Name; }
            set { doc.Name = value; }
        }

        public IEnumerable<IDxfEntity> Entities
        {
            get
            {
                var result = new List<IDxfEntity>();
                // Get all entities from the model space block
                var modelSpace = doc.Blocks["*Model_Space"];
                if (modelSpace != null)
                {
                    foreach (var e in modelSpace.Entities)
                    {
                        result.Add(WrapEntity(e));
                    }
                }
                return result;
            }
        }

        public void SaveToFile(string fileName)
        {
            doc.Save(fileName);
        }

        public void SaveToStream(Stream stream)
        {
            doc.Save(stream);
        }

        public void AddEntity(IDxfEntity entity)
        {
            if (entity is NetDxfEntityAdapter adapter)
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

        private IDxfEntity WrapEntity(EntityObject entity)
        {
            if (entity is netDxf.Entities.Line line)
                return new NetDxfLineAdapter(line);
            else if (entity is netDxf.Entities.Ray ray)
                return new NetDxfRayAdapter(ray);
            else if (entity is netDxf.Entities.Arc arc)
                return new NetDxfArcAdapter(arc);
            else if (entity is netDxf.Entities.Circle circle)
                return new NetDxfCircleAdapter(circle);
            else if (entity is netDxf.Entities.Ellipse ellipse)
                return new NetDxfEllipseAdapter(ellipse);
            else if (entity is netDxf.Entities.Spline spline)
                return new NetDxfSplineAdapter(spline);
            else if (entity is netDxf.Entities.Polyline2D poly2d)
                return new NetDxfPolyline2DAdapter(poly2d);
            else if (entity is netDxf.Entities.Polyline3D poly3d)
                return new NetDxfPolyline3DAdapter(poly3d);
            else if (entity is netDxf.Entities.Text text)
                return new NetDxfTextAdapter(text);
            else if (entity is netDxf.Entities.MText mtext)
                return new NetDxfMTextAdapter(mtext);
            else if (entity is netDxf.Entities.Hatch hatch)
                return new NetDxfHatchAdapter(hatch);
            else if (entity is netDxf.Entities.Insert insert)
                return new NetDxfInsertAdapter(insert, this);
            else if (entity is netDxf.Entities.Face3D face3d)
                return new NetDxfFace3DAdapter(face3d);
            else if (entity is netDxf.Entities.PolyfaceMesh pfm)
                return new NetDxfPolyfaceMeshAdapter(pfm);
            else if (entity is netDxf.Entities.Solid solid)
                return new NetDxfSolidAdapter(solid);
            else if (entity is netDxf.Entities.Point point)
                return new NetDxfPointAdapter(point);
            else if (entity is netDxf.Entities.MLine mline)
                return new NetDxfMLineAdapter(mline);
            else if (entity is netDxf.Entities.Mesh mesh)
                return new NetDxfMeshAdapter(mesh);
            else if (entity is netDxf.Entities.Dimension dim)
                return new NetDxfDimensionAdapter(dim, this);
            else if (entity is netDxf.Entities.Leader leader)
                return new NetDxfLeaderAdapter(leader, this);
            else
                return new NetDxfEntityAdapter(entity);
        }
    }

    /// <summary>
    /// netDxf block collection adapter.
    /// </summary>
    internal class NetDxfBlockCollectionAdapter : IDxfBlockCollection
    {
        private readonly netDxf.Collections.BlockRecords blocks;

        public NetDxfBlockCollectionAdapter(netDxf.Collections.BlockRecords blocks)
        {
            this.blocks = blocks;
        }

        public IDxfBlock GetBlock(string name)
        {
            var block = blocks[name];
            return block != null ? new NetDxfBlockAdapter(block) : null;
        }

        public IEnumerable<IDxfEntity> GetBlockEntities(string blockName)
        {
            var block = blocks[blockName];
            if (block != null)
            {
                return block.Entities.Select(e => ConvertEntity(e));
            }
            return Enumerable.Empty<IDxfEntity>();
        }

        public void AddBlock(IDxfBlock block)
        {
            if (block is NetDxfBlockAdapter adapter)
            {
                blocks.Add(adapter.WrappedBlock);
            }
        }

        private IDxfEntity ConvertEntity(EntityObject entity)
        {
            if (entity is netDxf.Entities.Line line)
                return new NetDxfLineAdapter(line);
            else if (entity is netDxf.Entities.Arc arc)
                return new NetDxfArcAdapter(arc);
            else if (entity is netDxf.Entities.Circle circle)
                return new NetDxfCircleAdapter(circle);
            else if (entity is netDxf.Entities.Ellipse ellipse)
                return new NetDxfEllipseAdapter(ellipse);
            else if (entity is netDxf.Entities.Spline spline)
                return new NetDxfSplineAdapter(spline);
            else
                return new NetDxfEntityAdapter(entity);
        }
    }

    /// <summary>
    /// netDxf block adapter.
    /// </summary>
    internal class NetDxfBlockAdapter : IDxfBlock
    {
        private readonly Block block;

        public NetDxfBlockAdapter(Block block)
        {
            this.block = block;
        }

        public Block WrappedBlock => block;

        public string Name => block.Name;
        public (double X, double Y, double Z) Origin => (block.Origin.X, block.Origin.Y, block.Origin.Z);
        public IEnumerable<IDxfEntity> Entities => block.Entities.Select(e => ConvertEntity(e));
        public string Handle => block.Handle;

        private IDxfEntity ConvertEntity(EntityObject entity)
        {
            if (entity is netDxf.Entities.Line line)
                return new NetDxfLineAdapter(line);
            else if (entity is netDxf.Entities.Arc arc)
                return new NetDxfArcAdapter(arc);
            else if (entity is netDxf.Entities.Circle circle)
                return new NetDxfCircleAdapter(circle);
            else
                return new NetDxfEntityAdapter(entity);
        }
    }

    /// <summary>
    /// netDxf layer adapter.
    /// </summary>
    internal class NetDxfLayerAdapter : IDxfLayer
    {
        private readonly Layer layer;

        public NetDxfLayerAdapter(Layer layer)
        {
            this.layer = layer;
        }

        public string Name => layer.Name;
        public int ColorArgb => layer.Color.ToColor().ToArgb();
        public int LineWeight => (int)layer.Lineweight;
        public string LineTypeName => layer.Linetype.Name;
    }

    /// <summary>
    /// netDxf line type adapter.
    /// </summary>
    internal class NetDxfLineTypeAdapter : IDxfLineType
    {
        private readonly Linetype linetype;

        public NetDxfLineTypeAdapter(Linetype linetype)
        {
            this.linetype = linetype;
        }

        public string Name => linetype.Name;

        public double[] Segments
        {
            get
            {
                var segments = new List<double>();
                foreach (var segment in linetype.Segments)
                {
                    if (segment.Type == LinetypeSegmentType.Simple)
                    {
                        segments.Add(Math.Abs(segment.Length));
                    }
                }
                return segments.ToArray();
            }
        }
    }

    /// <summary>
    /// Base netDxf entity adapter.
    /// </summary>
    internal class NetDxfEntityAdapter : IDxfEntity
    {
        protected readonly EntityObject entity;

        public NetDxfEntityAdapter(EntityObject entity)
        {
            this.entity = entity;
        }

        public EntityObject WrappedEntity => entity;

        public virtual DxfEntityType EntityType => DxfEntityType.Unknown;
        public virtual string LayerName
        {
            get { return entity.Layer?.Name; }
            set { if (entity.Layer != null) entity.Layer = new Layer(value); }
        }

        public virtual int? ColorArgb
        {
            get { return entity.Color?.ToColor().ToArgb(); }
            set { if (value.HasValue) entity.Color = AciColor.FromTrueColor(value.Value); }
        }

        public virtual string LineTypeName
        {
            get { return entity.Linetype?.Name; }
            set { if (value != null) entity.Linetype = new Linetype(value); }
        }

        public virtual int? LineWeight
        {
            get { return (int)entity.Lineweight; }
            set { if (value.HasValue) entity.Lineweight = (Lineweight)value.Value; }
        }

        public virtual string Handle => entity.Handle;
        public virtual bool IsVisible
        {
            get { return entity.IsVisible; }
            set { entity.IsVisible = value; }
        }

        public virtual IEnumerable<IDxfXData> XData => entity.XData.Select(x => new NetDxfXDataAdapter(x.Value));
    }

    internal class NetDxfXDataAdapter : IDxfXData
    {
        private readonly XData xdata;

        public NetDxfXDataAdapter(XData xdata)
        {
            this.xdata = xdata;
        }

        public string ApplicationName => xdata.ApplicationRegistry.Name;

        public IEnumerable<(int Code, object Value)> Records =>
            xdata.XDataRecord.Select(r => ((int)r.Code, r.Value));
    }

    internal class NetDxfLineAdapter : NetDxfEntityAdapter, IDxfLine
    {
        private readonly netDxf.Entities.Line line;

        public NetDxfLineAdapter(netDxf.Entities.Line line) : base(line)
        {
            this.line = line;
        }

        public override DxfEntityType EntityType => DxfEntityType.Line;
        public (double X, double Y, double Z) StartPoint => (line.StartPoint.X, line.StartPoint.Y, line.StartPoint.Z);
        public (double X, double Y, double Z) EndPoint => (line.EndPoint.X, line.EndPoint.Y, line.EndPoint.Z);
        public double Thickness => line.Thickness;
        public (double X, double Y, double Z) Normal => (line.Normal.X, line.Normal.Y, line.Normal.Z);
    }

    internal class NetDxfRayAdapter : NetDxfEntityAdapter, IDxfRay
    {
        private readonly netDxf.Entities.Ray ray;

        public NetDxfRayAdapter(netDxf.Entities.Ray ray) : base(ray)
        {
            this.ray = ray;
        }

        public override DxfEntityType EntityType => DxfEntityType.Ray;
        public (double X, double Y, double Z) Origin => (ray.Origin.X, ray.Origin.Y, ray.Origin.Z);
        public (double X, double Y, double Z) Direction => (ray.Direction.X, ray.Direction.Y, ray.Direction.Z);
    }

    internal class NetDxfArcAdapter : NetDxfEntityAdapter, IDxfArc
    {
        private readonly netDxf.Entities.Arc arc;

        public NetDxfArcAdapter(netDxf.Entities.Arc arc) : base(arc)
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

    internal class NetDxfCircleAdapter : NetDxfEntityAdapter, IDxfCircle
    {
        private readonly netDxf.Entities.Circle circle;

        public NetDxfCircleAdapter(netDxf.Entities.Circle circle) : base(circle)
        {
            this.circle = circle;
        }

        public override DxfEntityType EntityType => DxfEntityType.Circle;
        public (double X, double Y, double Z) Center => (circle.Center.X, circle.Center.Y, circle.Center.Z);
        public double Radius => circle.Radius;
        public (double X, double Y, double Z) Normal => (circle.Normal.X, circle.Normal.Y, circle.Normal.Z);
        public double Thickness => circle.Thickness;
    }

    internal class NetDxfEllipseAdapter : NetDxfEntityAdapter, IDxfEllipse
    {
        private readonly netDxf.Entities.Ellipse ellipse;

        public NetDxfEllipseAdapter(netDxf.Entities.Ellipse ellipse) : base(ellipse)
        {
            this.ellipse = ellipse;
        }

        public override DxfEntityType EntityType => DxfEntityType.Ellipse;
        public (double X, double Y, double Z) Center => (ellipse.Center.X, ellipse.Center.Y, ellipse.Center.Z);
        public (double X, double Y, double Z) MajorAxisEnd
        {
            get
            {
                double majorLen = ellipse.MajorAxis * 0.5;
                return (ellipse.Center.X + majorLen, ellipse.Center.Y, ellipse.Center.Z);
            }
        }
        public double MinorAxisRatio => ellipse.MinorAxis / ellipse.MajorAxis;
        public double StartAngle => ellipse.StartAngle;
        public double EndAngle => ellipse.EndAngle;
        public (double X, double Y, double Z) Normal => (ellipse.Normal.X, ellipse.Normal.Y, ellipse.Normal.Z);
    }

    internal class NetDxfSplineAdapter : NetDxfEntityAdapter, IDxfSpline
    {
        private readonly netDxf.Entities.Spline spline;

        public NetDxfSplineAdapter(netDxf.Entities.Spline spline) : base(spline)
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
        public bool IsClosedPeriodic => spline.IsClosedPeriodic;
    }

    internal class NetDxfPolyline2DAdapter : NetDxfEntityAdapter, IDxfPolyline2D
    {
        private readonly netDxf.Entities.Polyline2D polyline2d;

        public NetDxfPolyline2DAdapter(netDxf.Entities.Polyline2D polyline2d) : base(polyline2d)
        {
            this.polyline2d = polyline2d;
        }

        public override DxfEntityType EntityType => DxfEntityType.Polyline2D;
        public IEnumerable<IDxfVertex> Vertices => polyline2d.Vertexes.Select(v => new NetDxfVertexAdapter(v));
        public bool IsClosed => polyline2d.IsClosed;

        public IEnumerable<IDxfEntity> Explode()
        {
            return polyline2d.Explode().Select(e => ConvertEntity(e));
        }

        private IDxfEntity ConvertEntity(EntityObject e)
        {
            if (e is netDxf.Entities.Line line)
                return new NetDxfLineAdapter(line);
            else if (e is netDxf.Entities.Arc arc)
                return new NetDxfArcAdapter(arc);
            else
                return new NetDxfEntityAdapter(e);
        }
    }

    internal class NetDxfVertexAdapter : IDxfVertex
    {
        private readonly netDxf.Entities.Polyline2DVertex vertex;

        public NetDxfVertexAdapter(netDxf.Entities.Polyline2DVertex vertex)
        {
            this.vertex = vertex;
        }

        public (double X, double Y) Position => (vertex.Position.X, vertex.Position.Y);
        public double Bulge => vertex.Bulge;
    }

    internal class NetDxfTextAdapter : NetDxfEntityAdapter, IDxfText
    {
        private readonly netDxf.Entities.Text text;

        public NetDxfTextAdapter(netDxf.Entities.Text text) : base(text)
        {
            this.text = text;
        }

        public override DxfEntityType EntityType => DxfEntityType.Text;
        public string Value => text.Value;
        public (double X, double Y, double Z) Position => (text.Position.X, text.Position.Y, text.Position.Z);
        public double Height => text.Height;
        public double Rotation => text.Rotation;
        public double WidthFactor => text.WidthFactor;
        public (double X, double Y, double Z) Normal => (text.Normal.X, text.Normal.Y, text.Normal.Z);
        public string StyleName => text.Style?.Name;
        public string FontName => text.Style?.FontFamilyName ?? text.Style?.FontFile;
        public bool IsBold => text.Style?.FontStyle.HasFlag(netDxf.Tables.FontStyle.Bold) ?? false;
        public bool IsItalic => text.Style?.FontStyle.HasFlag(netDxf.Tables.FontStyle.Italic) ?? false;
    }

    internal class NetDxfMTextAdapter : NetDxfEntityAdapter, IDxfMText
    {
        private readonly netDxf.Entities.MText mtext;

        public NetDxfMTextAdapter(netDxf.Entities.MText mtext) : base(mtext)
        {
            this.mtext = mtext;
        }

        public override DxfEntityType EntityType => DxfEntityType.MText;
        public string PlainText => mtext.PlainText();
        public (double X, double Y, double Z) Position => (mtext.Position.X, mtext.Position.Y, mtext.Position.Z);
        public double Height => mtext.Height;
        public double Rotation => mtext.Rotation;
        public (double X, double Y, double Z) Normal => (mtext.Normal.X, mtext.Normal.Y, mtext.Normal.Z);
        public string StyleName => mtext.Style?.Name;
    }

    internal class NetDxfHatchAdapter : NetDxfEntityAdapter, IDxfHatch
    {
        private readonly netDxf.Entities.Hatch hatch;

        public NetDxfHatchAdapter(netDxf.Entities.Hatch hatch) : base(hatch)
        {
            this.hatch = hatch;
        }

        public override DxfEntityType EntityType => DxfEntityType.Hatch;
        public IEnumerable<IDxfHatchBoundaryPath> BoundaryPaths =>
            hatch.BoundaryPaths.Select(bp => new NetDxfHatchBoundaryPathAdapter(bp));
        public IDxfHatchPattern Pattern => new NetDxfHatchPatternAdapter(hatch.Pattern);
        public (double X, double Y, double Z) Normal => (hatch.Normal.X, hatch.Normal.Y, hatch.Normal.Z);
    }

    internal class NetDxfHatchBoundaryPathAdapter : IDxfHatchBoundaryPath
    {
        private readonly HatchBoundaryPath path;

        public NetDxfHatchBoundaryPathAdapter(HatchBoundaryPath path)
        {
            this.path = path;
        }

        public IEnumerable<IDxfEntity> Edges => path.Edges.Select(e => ConvertEntity(e.ConvertTo()));

        private IDxfEntity ConvertEntity(EntityObject e)
        {
            if (e is netDxf.Entities.Line line)
                return new NetDxfLineAdapter(line);
            else if (e is netDxf.Entities.Arc arc)
                return new NetDxfArcAdapter(arc);
            else
                return new NetDxfEntityAdapter(e);
        }
    }

    internal class NetDxfHatchPatternAdapter : IDxfHatchPattern
    {
        private readonly netDxf.Entities.HatchPattern pattern;

        public NetDxfHatchPatternAdapter(netDxf.Entities.HatchPattern pattern)
        {
            this.pattern = pattern;
        }

        public HatchFillType FillType
        {
            get
            {
                if (pattern.Fill == netDxf.Entities.HatchFillType.SolidFill)
                    return HatchFillType.SolidFill;
                else if (pattern.Fill == netDxf.Entities.HatchFillType.PatternFill)
                    return HatchFillType.PatternFill;
                else
                    return HatchFillType.PatternFill; // default
            }
        }

        public IEnumerable<IDxfHatchLineDefinition> LineDefinitions =>
            pattern.LineDefinitions.Select(ld => new NetDxfHatchLineDefinitionAdapter(ld));
    }

    internal class NetDxfHatchLineDefinitionAdapter : IDxfHatchLineDefinition
    {
        private readonly HatchPatternLineDefinition lineDef;

        public NetDxfHatchLineDefinitionAdapter(HatchPatternLineDefinition lineDef)
        {
            this.lineDef = lineDef;
        }

        public double Angle => lineDef.Angle;
        public (double X, double Y) Origin => (lineDef.Origin.X, lineDef.Origin.Y);
        public (double X, double Y) Delta => (lineDef.Delta.X, lineDef.Delta.Y);
        public double[] DashPattern => lineDef.DashPattern.ToArray();
    }

    internal class NetDxfInsertAdapter : NetDxfEntityAdapter, IDxfInsert
    {
        private readonly netDxf.Entities.Insert insert;
        private readonly NetDxfDocumentAdapter document;

        public NetDxfInsertAdapter(netDxf.Entities.Insert insert, NetDxfDocumentAdapter document) : base(insert)
        {
            this.insert = insert;
            this.document = document;
        }

        public override DxfEntityType EntityType => DxfEntityType.Insert;
        public string BlockName => insert.Block?.Name;
        public (double X, double Y, double Z) Position => (insert.Position.X, insert.Position.Y, insert.Position.Z);
        public double Rotation => insert.Rotation;
        public (double X, double Y, double Z) Scale => (insert.Scale.X, insert.Scale.Y, insert.Scale.Z);
        public IDxfBlock Block => insert.Block != null ? new NetDxfBlockAdapter(insert.Block) : null;
    }

    internal class NetDxfFace3DAdapter : NetDxfEntityAdapter, IDxfFace3D
    {
        private readonly netDxf.Entities.Face3D face;

        public NetDxfFace3DAdapter(netDxf.Entities.Face3D face) : base(face)
        {
            this.face = face;
        }

        public override DxfEntityType EntityType => DxfEntityType.Face3D;
        public (double X, double Y, double Z) FirstVertex => (face.FirstVertex.X, face.FirstVertex.Y, face.FirstVertex.Z);
        public (double X, double Y, double Z) SecondVertex => (face.SecondVertex.X, face.SecondVertex.Y, face.SecondVertex.Z);
        public (double X, double Y, double Z) ThirdVertex => (face.ThirdVertex.X, face.ThirdVertex.Y, face.ThirdVertex.Z);
        public (double X, double Y, double Z) FourthVertex => (face.FourthVertex.X, face.FourthVertex.Y, face.FourthVertex.Z);
    }

    internal class NetDxfPolyline3DAdapter : NetDxfEntityAdapter, IDxfPolyline3D
    {
        private readonly netDxf.Entities.Polyline3D polyline3d;

        public NetDxfPolyline3DAdapter(netDxf.Entities.Polyline3D polyline3d) : base(polyline3d)
        {
            this.polyline3d = polyline3d;
        }

        public override DxfEntityType EntityType => DxfEntityType.Polyline3D;
        public IEnumerable<(double X, double Y, double Z)> Vertices =>
            polyline3d.Vertexes.Select(v => (v.X, v.Y, v.Z));
        public bool IsClosed => polyline3d.IsClosed;
    }

    internal class NetDxfSolidAdapter : NetDxfEntityAdapter, IDxfSolid
    {
        private readonly netDxf.Entities.Solid solid;

        public NetDxfSolidAdapter(netDxf.Entities.Solid solid) : base(solid)
        {
            this.solid = solid;
        }

        public override DxfEntityType EntityType => DxfEntityType.Solid;
        public (double X, double Y) FirstVertex => (solid.FirstVertex.X, solid.FirstVertex.Y);
        public (double X, double Y) SecondVertex => (solid.SecondVertex.X, solid.SecondVertex.Y);
        public (double X, double Y) ThirdVertex => (solid.ThirdVertex.X, solid.ThirdVertex.Y);
        public (double X, double Y) FourthVertex => (solid.FourthVertex.X, solid.FourthVertex.Y);
        public (double X, double Y, double Z) Normal => (solid.Normal.X, solid.Normal.Y, solid.Normal.Z);
        public double Elevation => solid.Elevation;
    }

    internal class NetDxfPointAdapter : NetDxfEntityAdapter, IDxfPoint
    {
        private readonly netDxf.Entities.Point point;

        public NetDxfPointAdapter(netDxf.Entities.Point point) : base(point)
        {
            this.point = point;
        }

        public override DxfEntityType EntityType => DxfEntityType.Point;
        public (double X, double Y, double Z) Position => (point.Position.X, point.Position.Y, point.Position.Z);
    }

    internal class NetDxfMLineAdapter : NetDxfEntityAdapter, IDxfMLine
    {
        private readonly netDxf.Entities.MLine mline;

        public NetDxfMLineAdapter(netDxf.Entities.MLine mline) : base(mline)
        {
            this.mline = mline;
        }

        public override DxfEntityType EntityType => DxfEntityType.MLine;

        public IEnumerable<IDxfEntity> Explode()
        {
            return mline.Explode().Select(e => ConvertEntity(e));
        }

        private IDxfEntity ConvertEntity(EntityObject e)
        {
            if (e is netDxf.Entities.Line line)
                return new NetDxfLineAdapter(line);
            else
                return new NetDxfEntityAdapter(e);
        }
    }

    internal class NetDxfPolyfaceMeshAdapter : NetDxfEntityAdapter, IDxfPolyfaceMesh
    {
        private readonly netDxf.Entities.PolyfaceMesh mesh;

        public NetDxfPolyfaceMeshAdapter(netDxf.Entities.PolyfaceMesh mesh) : base(mesh)
        {
            this.mesh = mesh;
        }

        public override DxfEntityType EntityType => DxfEntityType.PolyfaceMesh;
        public (double X, double Y, double Z)[] Vertices =>
            mesh.Vertexes.Select(v => (v.X, v.Y, v.Z)).ToArray();
        public IEnumerable<short[]> Faces => mesh.Faces.Select(f => f.VertexIndexes);

        public void Explode()
        {
            mesh.Explode();
        }
    }

    internal class NetDxfMeshAdapter : NetDxfEntityAdapter, IDxfMesh
    {
        private readonly netDxf.Entities.Mesh mesh;

        public NetDxfMeshAdapter(netDxf.Entities.Mesh mesh) : base(mesh)
        {
            this.mesh = mesh;
        }

        public override DxfEntityType EntityType => DxfEntityType.Mesh;
        public IEnumerable<(double X, double Y, double Z)> Vertices =>
            mesh.Vertexes.Select(v => (v.X, v.Y, v.Z));
        public IEnumerable<int[]> Faces => mesh.Faces;
    }

    internal class NetDxfDimensionAdapter : NetDxfEntityAdapter, IDxfDimension
    {
        private readonly netDxf.Entities.Dimension dimension;
        private readonly NetDxfDocumentAdapter document;

        public NetDxfDimensionAdapter(netDxf.Entities.Dimension dimension, NetDxfDocumentAdapter document)
            : base(dimension)
        {
            this.dimension = dimension;
            this.document = document;
        }

        public override DxfEntityType EntityType => DxfEntityType.Dimension;
        public IDxfBlock DimensionBlock => dimension.Block != null ? new NetDxfBlockAdapter(dimension.Block) : null;
    }

    internal class NetDxfLeaderAdapter : NetDxfEntityAdapter, IDxfLeader
    {
        private readonly netDxf.Entities.Leader leader;
        private readonly NetDxfDocumentAdapter document;

        public NetDxfLeaderAdapter(netDxf.Entities.Leader leader, NetDxfDocumentAdapter document)
            : base(leader)
        {
            this.leader = leader;
            this.document = document;
        }

        public override DxfEntityType EntityType => DxfEntityType.Leader;
        public IEnumerable<(double X, double Y)> Vertices =>
            leader.Vertexes.Select(v => (v.X, v.Y));
        public (double X, double Y, double Z) Normal => (leader.Normal.X, leader.Normal.Y, leader.Normal.Z);
        public double Elevation => leader.Elevation;
        public IDxfEntity Annotation => leader.Annotation != null ? ConvertEntity(leader.Annotation) : null;

        private IDxfEntity ConvertEntity(EntityObject e)
        {
            if (e is netDxf.Entities.Text text)
                return new NetDxfTextAdapter(text);
            else
                return new NetDxfEntityAdapter(e);
        }
    }
}
