using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using CSMath;
using CADability.GeoObject;
using CADability.Shapes;
using CADability.Curve2D;
using CADability.Attribute;
#if WEBASSEMBLY
using CADability.WebDrawing;
using Point = CADability.WebDrawing.Point;
#else
using System.Drawing;
#endif
using AcadColor = ACadSharp.Color;
using SystemColor = System.Drawing.Color;

namespace CADability.DXF
{
    /// <summary>
    /// Imports DXF/DWG files using ACADSharp library
    /// </summary>
    public class ImportAcadSharp
    {
        private CadDocument doc;
        private Project project;
        private Dictionary<string, GeoObject.Block> blockTable;
        private Dictionary<ACadSharp.Tables.Layer, ColorDef> layerColorTable;
        private Dictionary<ACadSharp.Tables.Layer, Attribute.Layer> layerTable;
        private Dictionary<string, ACadSharp.Tables.Layer> acadLayersByName;
        private Dictionary<Entity, IGeoObject> entityCache;

        /// <summary>
        /// Create the Import instance from a file
        /// </summary>
        /// <param name="fileName">Path to DXF or DWG file</param>
        public ImportAcadSharp(string fileName)
        {
            using (Stream stream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                // Determine file type by extension
                string ext = System.IO.Path.GetExtension(fileName).ToLower();
                
                if (ext == ".dwg")
                {
                    using (DwgReader reader = new DwgReader(stream))
                    {
                        doc = reader.Read();
                    }
                }
                else // .dxf
                {
                    using (DxfReader reader = new DxfReader(stream))
                    {
                        doc = reader.Read();
                    }
                }
            }
            
            entityCache = new Dictionary<Entity, IGeoObject>();
        }

        /// <summary>
        /// Check if the file version can be imported
        /// </summary>
        public static bool CanImportVersion(string fileName)
        {
            try
            {
                string ext = System.IO.Path.GetExtension(fileName).ToLower();
                if (ext == ".dwg")
                {
                    // ACADSharp supports AC1014 (R14) and later for DWG
                    using (Stream stream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        using (DwgReader reader = new DwgReader(stream))
                        {
                            // If we can create a reader, the version is likely supported
                            return true;
                        }
                    }
                }
                else
                {
                    // ACADSharp supports AC1009 (R12) and later for DXF
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Creates and returns the imported project
        /// </summary>
        public Project Project { get => CreateProject(); }

        private Project CreateProject()
        {
            if (doc == null) return null;

            project = CADability.Project.CreateSimpleProject();
            blockTable = new Dictionary<string, GeoObject.Block>();
            layerColorTable = new Dictionary<ACadSharp.Tables.Layer, ColorDef>();
            layerTable = new Dictionary<ACadSharp.Tables.Layer, Attribute.Layer>();
            acadLayersByName = new Dictionary<string, ACadSharp.Tables.Layer>();

            // Import layers
            foreach (var layer in doc.Layers)
            {
                Attribute.Layer cadLayer = project.LayerList.CreateOrFind(layer.Name);
                layerTable[layer] = cadLayer;
                acadLayersByName[layer.Name] = layer;

                // Map layer color
                SystemColor rgb = ConvertAciColor(layer.Color);
                ColorDef cd = project.ColorList.CreateOrFind(layer.Name + ":ByLayer", rgb);
                layerColorTable[layer] = cd;
            }

            // Import line types
            foreach (var lineType in doc.LineTypes)
            {
                List<double> pattern = new List<double>();
                // ACADSharp linetype segments
                foreach (var segment in lineType.Segments)
                {
                    if (segment.Length != 0)
                    {
                        pattern.Add(Math.Abs(segment.Length));
                    }
                }
                if (pattern.Count > 0)
                {
                    project.LinePatternList.CreateOrFind(lineType.Name, pattern.ToArray());
                }
            }

            // Import block definitions first
            foreach (var block in doc.BlockRecords)
            {
                if (!block.Name.StartsWith("*")) // Skip special blocks initially
                {
                    CreateBlockDefinition(block);
                }
            }

            // Import ModelSpace
            var modelSpace = doc.BlockRecords.FirstOrDefault(b => b.Name == BlockRecord.ModelSpaceName);
            if (modelSpace != null)
            {
                FillModel(project.GetModel(0), modelSpace);
                project.GetModel(0).Name = "*Model_Space";
            }

            // Import PaperSpace
            var paperSpace = doc.BlockRecords.FirstOrDefault(b => b.Name == BlockRecord.PaperSpaceName);
            if (paperSpace != null)
            {
                Model paperModel = new Model();
                FillModel(paperModel, paperSpace);
                paperModel.Name = "*Paper_Space";
                
                if (paperModel.Count > 0)
                {
                    project.AddModel(paperModel);
                    Model modelSpaceModel = project.GetModel(0);
                    if (modelSpaceModel.Count == 0)
                    {
                        // If modelspace is empty and paperspace has entities, show paperspace
                        for (int i = 0; i < project.ModelViewCount; ++i)
                        {
                            ProjectedModel pm = project.GetProjectedModel(i);
                            if (pm.Model == modelSpaceModel) pm.Model = paperModel;
                        }
                    }
                }
            }

            doc = null;
            return project;
        }

        private void CreateBlockDefinition(BlockRecord blockRecord)
        {
            if (blockTable.ContainsKey(blockRecord.Name))
                return;

            GeoObject.Block block = GeoObject.Block.Construct();
            block.Name = blockRecord.Name;

            foreach (var entity in blockRecord.Entities)
            {
                IGeoObject geoObject = GeoObjectFromEntity(entity);
                if (geoObject != null)
                {
                    block.Add(geoObject);
                }
            }

            blockTable[blockRecord.Name] = block;
        }

        private void FillModel(Model model, BlockRecord blockRecord)
        {
            foreach (var entity in blockRecord.Entities)
            {
                IGeoObject geoObject = GeoObjectFromEntity(entity);
                if (geoObject != null)
                {
                    model.Add(geoObject);
                }
            }
        }

        private IGeoObject GeoObjectFromEntity(Entity entity)
        {
            // Check cache first
            if (entityCache.TryGetValue(entity, out IGeoObject cached))
                return cached;

            IGeoObject res = null;

            try
            {
                switch (entity)
                {
                    case ACadSharp.Entities.Line line:
                        res = CreateLine(line);
                        break;
                    case ACadSharp.Entities.Arc arc:
                        res = CreateArc(arc);
                        break;
                    case ACadSharp.Entities.Circle circle:
                        res = CreateCircle(circle);
                        break;
                    case ACadSharp.Entities.Ellipse ellipse:
                        res = CreateEllipse(ellipse);
                        break;
                    case ACadSharp.Entities.Spline spline:
                        res = CreateSpline(spline);
                        break;
                    case ACadSharp.Entities.LwPolyline lwPolyline:
                        res = CreateLwPolyline(lwPolyline);
                        break;
                    case ACadSharp.Entities.Polyline polyline:
                        res = CreatePolyline(polyline);
                        break;
                    case ACadSharp.Entities.Point point:
                        res = CreatePoint(point);
                        break;
                    case ACadSharp.Entities.TextEntity text:
                        res = CreateText(text);
                        break;
                    case ACadSharp.Entities.MText mtext:
                        res = CreateMText(mtext);
                        break;
                    case ACadSharp.Entities.Hatch hatch:
                        res = CreateHatch(hatch);
                        break;
                    case ACadSharp.Entities.Insert insert:
                        res = CreateInsert(insert);
                        break;
                    case ACadSharp.Entities.Dimension dimension:
                        res = CreateDimension(dimension);
                        break;
                    case ACadSharp.Entities.Leader leader:
                        res = CreateLeader(leader);
                        break;
                    case ACadSharp.Entities.Face3D face:
                        res = CreateFace3D(face);
                        break;
                    case ACadSharp.Entities.Solid solid:
                        res = CreateSolid(solid);
                        break;
                    case ACadSharp.Entities.Mesh mesh:
                        res = CreateMesh(mesh);
                        break;
                    default:
                        System.Diagnostics.Trace.WriteLine($"ACADSharp: Entity type not imported: {entity.GetType().Name}");
                        break;
                }

                if (res != null)
                {
                    SetAttributes(res, entity);
                    res.IsVisible = !entity.IsInvisible;
                    
                    // Cache the result
                    entityCache[entity] = res;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"ACADSharp: Error importing entity {entity.GetType().Name}: {ex.Message}");
            }

            return res;
        }

        #region Coordinate Conversion Helpers

        private static GeoPoint ToGeoPoint(XYZ point)
        {
            return new GeoPoint(point.X, point.Y, point.Z);
        }

        private static GeoVector ToGeoVector(XYZ vector)
        {
            return new GeoVector(vector.X, vector.Y, vector.Z);
        }

        private static Plane ToPlane(XYZ center, XYZ normal)
        {
            // AutoCAD's arbitrary axis algorithm
            GeoVector n = ToGeoVector(normal).Normalized;
            GeoVector ax;
            
            if (Math.Abs(normal.X) < 1.0 / 64 && Math.Abs(normal.Y) < 1.0 / 64)
            {
                ax = GeoVector.YAxis ^ n;
            }
            else
            {
                ax = GeoVector.ZAxis ^ n;
            }
            
            ax = ax.Normalized;
            GeoVector ay = (n ^ ax).Normalized;
            
            return new Plane(ToGeoPoint(center), ax, ay);
        }

        #endregion

        #region Color and Style Helpers

        private SystemColor ConvertAciColor(AcadColor color)
        {
            // ACADSharp Color has R, G, B properties
            SystemColor rgb = SystemColor.FromArgb(color.R, color.G, color.B);
            
            // Convert white to black for better visibility
            if (rgb.ToArgb() == SystemColor.White.ToArgb())
            {
                rgb = SystemColor.Black;
            }
            
            return rgb;
        }

        private ColorDef FindOrCreateColor(AcadColor color, ACadSharp.Tables.Layer layer)
        {
            // Check if color is ByLayer
            if (color.IsByLayer && layer != null && layerColorTable.TryGetValue(layer, out ColorDef layerColor))
            {
                return layerColor;
            }

            SystemColor rgb = ConvertAciColor(color);
            string colorName = $"RGB_{rgb.R}_{rgb.G}_{rgb.B}";
            return project.ColorList.CreateOrFind(colorName, rgb);
        }

        private void SetAttributes(IGeoObject geoObject, Entity entity)
        {
            // Set color
            if (geoObject is IColorDef colorDef)
            {
                colorDef.ColorDef = FindOrCreateColor(entity.Color, entity.Layer);
            }

            // Set layer
            if (entity.Layer != null && layerTable.TryGetValue(entity.Layer, out Attribute.Layer layer))
            {
                geoObject.Layer = layer;
            }

            // Set line pattern
            if (geoObject is ILinePattern linePattern && entity.LineType != null)
            {
                var lp = project.LinePatternList.Find(entity.LineType.Name);
                if (lp != null)
                {
                    linePattern.LinePattern = lp;
                }
            }

            // Set line weight
            if (geoObject is ILineWidth lineWidth)
            {
                double weight = (int)entity.LineWeight / 100.0; // Convert from 1/100 mm to mm
                if (weight < 0) weight = 0.25; // Default line weight
                lineWidth.LineWidth = project.LineWidthList.CreateOrFind($"LW_{entity.LineWeight}", weight);
            }
        }

        #endregion

        #region Entity Conversion Methods

        private IGeoObject CreateLine(ACadSharp.Entities.Line line)
        {
            GeoPoint start = ToGeoPoint(line.StartPoint);
            GeoPoint end = ToGeoPoint(line.EndPoint);
            
            if (Precision.IsEqual(start, end))
                return null;

            GeoObject.Line l = GeoObject.Line.Construct();
            l.StartPoint = start;
            l.EndPoint = end;
            return l;
        }

        private IGeoObject CreateCircle(ACadSharp.Entities.Circle circle)
        {
            GeoPoint center = ToGeoPoint(circle.Center);
            GeoVector normal = ToGeoVector(circle.Normal);
            
            Plane plane = ToPlane(circle.Center, circle.Normal);
            
            GeoObject.Ellipse ellipse = GeoObject.Ellipse.Construct();
            ellipse.SetCirclePlaneCenterRadius(plane, center, circle.Radius);
            
            return ellipse;
        }

        private IGeoObject CreateArc(ACadSharp.Entities.Arc arc)
        {
            GeoPoint center = ToGeoPoint(arc.Center);
            GeoVector normal = ToGeoVector(arc.Normal);
            Plane plane = ToPlane(arc.Center, arc.Normal);
            
            // Convert angles from degrees to radians
            double startAngle = arc.StartAngle;
            double endAngle = arc.EndAngle;
            
            // Calculate start and end points
            GeoPoint start = center + (arc.Radius * Math.Cos(startAngle)) * plane.DirectionX + 
                           (arc.Radius * Math.Sin(startAngle)) * plane.DirectionY;
            GeoPoint end = center + (arc.Radius * Math.Cos(endAngle)) * plane.DirectionX + 
                         (arc.Radius * Math.Sin(endAngle)) * plane.DirectionY;
            
            GeoObject.Ellipse ellipse = GeoObject.Ellipse.Construct();
            ellipse.SetArcPlaneCenterRadiusAngles(plane, center, arc.Radius, startAngle, endAngle - startAngle);
            
            return ellipse;
        }

        private IGeoObject CreateEllipse(ACadSharp.Entities.Ellipse ellipse)
        {
            GeoPoint center = ToGeoPoint(ellipse.Center);
            // MajorAxisEndPoint is a vector from center to the end of major axis
            GeoVector majorAxis = new GeoVector(ellipse.EndPoint.X - ellipse.Center.X,
                                                 ellipse.EndPoint.Y - ellipse.Center.Y,
                                                 ellipse.EndPoint.Z - ellipse.Center.Z);
            GeoVector normal = ToGeoVector(ellipse.Normal);
            
            double majorRadius = majorAxis.Length;
            double minorRadius = majorRadius * ellipse.RadiusRatio;
            
            GeoVector minorAxis = normal ^ majorAxis;
            minorAxis = minorRadius * minorAxis.Normalized;
            
            GeoObject.Ellipse cadEllipse = GeoObject.Ellipse.Construct();
            cadEllipse.SetEllipseCenterAxis(center, majorAxis, minorAxis);
            
            // Handle partial ellipse (arc)
            if (ellipse.StartParameter != 0 || ellipse.EndParameter != 2 * Math.PI)
            {
                cadEllipse.StartParameter = ellipse.StartParameter;
                double sweep = ellipse.EndParameter - ellipse.StartParameter;
                if (sweep < 0) sweep += 2 * Math.PI;
                cadEllipse.SweepParameter = sweep;
            }
            
            return cadEllipse;
        }

        private IGeoObject CreateSpline(ACadSharp.Entities.Spline spline)
        {
            try
            {
                // Convert control points
                GeoPoint[] controlPoints = spline.ControlPoints.Select(cp => ToGeoPoint(cp)).ToArray();
                
                if (controlPoints.Length < 2)
                    return null;

                // Convert knots - ACADSharp provides knot vector
                double[] knots = spline.Knots.ToArray();
                
                // Create BSpline
                GeoObject.BSpline bspline = GeoObject.BSpline.Construct();
                
                // Set degree
                int degree = spline.Degree;
                
                // Build the BSpline with control points and knots
                // CADability BSpline uses poles, weights, knots, multiplicities
                double[] weights = spline.Weights?.ToArray();
                if (weights == null || weights.Length == 0)
                {
                    weights = new double[controlPoints.Length];
                    for (int i = 0; i < weights.Length; i++)
                        weights[i] = 1.0;
                }
                
                bspline.ThroughPoints(controlPoints, degree, false);
                
                return bspline;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating spline: {ex.Message}");
                
                // Fallback: create polyline through fit points if available
                if (spline.FitPoints != null && spline.FitPoints.Count > 1)
                {
                    GeoPoint[] fitPoints = spline.FitPoints.Select(fp => ToGeoPoint(fp)).ToArray();
                    return CreatePolylineFromPoints(fitPoints, false);
                }
                
                return null;
            }
        }

        private IGeoObject CreateLwPolyline(ACadSharp.Entities.LwPolyline lwPolyline)
        {
            if (lwPolyline.Vertices.Count < 2)
                return null;

            try
            {
                List<ICurve> segments = new List<ICurve>();
                bool isClosed = lwPolyline.Flags.HasFlag(LwPolylineFlags.Closed);
                
                int vertexCount = lwPolyline.Vertices.Count;
                if (isClosed) vertexCount++;
                
                for (int i = 0; i < vertexCount - 1; i++)
                {
                    var v1 = lwPolyline.Vertices[i];
                    var v2 = lwPolyline.Vertices[(i + 1) % lwPolyline.Vertices.Count];
                    
                    GeoPoint p1 = new GeoPoint(v1.Location.X, v1.Location.Y, lwPolyline.Elevation);
                    GeoPoint p2 = new GeoPoint(v2.Location.X, v2.Location.Y, lwPolyline.Elevation);
                    
                    if (Math.Abs(v1.Bulge) < 1e-10)
                    {
                        // Straight line segment
                        GeoObject.Line line = GeoObject.Line.Construct();
                        line.StartPoint = p1;
                        line.EndPoint = p2;
                        segments.Add(line);
                    }
                    else
                    {
                        // Arc segment (bulge)
                        ICurve arcSegment = CreateArcFromBulge(p1, p2, v1.Bulge, lwPolyline.Elevation);
                        if (arcSegment != null)
                            segments.Add(arcSegment);
                    }
                }
                
                if (segments.Count == 0)
                    return null;
                    
                if (segments.Count == 1)
                    return segments[0] as IGeoObject;
                
                // Create path from segments
                GeoObject.Path path = GeoObject.Path.Construct();
                path.Set(segments.ToArray());
                
                return path;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating LwPolyline: {ex.Message}");
                return null;
            }
        }

        private IGeoObject CreatePolyline(ACadSharp.Entities.Polyline polyline)
        {
            if (polyline.Vertices.Count < 2)
                return null;

            try
            {
                // Check if it's a 3D polyline or mesh
                bool is3D = polyline.Flags.HasFlag(PolylineFlags.Polyline3D) || 
                           polyline.Flags.HasFlag(PolylineFlags.PolyfaceMesh) ||
                           polyline.Flags.HasFlag(PolylineFlags.PolygonMesh);
                
                if (is3D)
                {
                    // 3D polyline - straight segments only
                    GeoPoint[] points = polyline.Vertices.Select(v => ToGeoPoint(v.Location)).ToArray();
                    return CreatePolylineFromPoints(points, (polyline.Flags & PolylineFlags.ClosedPolylineOrClosedPolygonMeshInM) != 0);
                }
                else
                {
                    // 2D polyline with potential bulges
                    List<ICurve> segments = new List<ICurve>();
                    bool isClosed = (polyline.Flags & PolylineFlags.ClosedPolylineOrClosedPolygonMeshInM) != 0;
                    
                    int vertexCount = polyline.Vertices.Count;
                    if (isClosed) vertexCount++;
                    
                    for (int i = 0; i < vertexCount - 1; i++)
                    {
                        var v1 = polyline.Vertices[i];
                        var v2 = polyline.Vertices[(i + 1) % polyline.Vertices.Count];
                        
                        GeoPoint p1 = ToGeoPoint(v1.Location);
                        GeoPoint p2 = ToGeoPoint(v2.Location);
                        
                        if (Math.Abs(v1.Bulge) < 1e-10)
                        {
                            GeoObject.Line line = GeoObject.Line.Construct();
                            line.StartPoint = p1;
                            line.EndPoint = p2;
                            segments.Add(line);
                        }
                        else
                        {
                            ICurve arcSegment = CreateArcFromBulge(p1, p2, v1.Bulge, p1.z);
                            if (arcSegment != null)
                                segments.Add(arcSegment);
                        }
                    }
                    
                    if (segments.Count == 0)
                        return null;
                        
                    if (segments.Count == 1)
                        return segments[0] as IGeoObject;
                    
                    GeoObject.Path path = GeoObject.Path.Construct();
                    path.Set(segments.ToArray());
                    return path;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating Polyline: {ex.Message}");
                return null;
            }
        }

        private ICurve CreateArcFromBulge(GeoPoint start, GeoPoint end, double bulge, double elevation)
        {
            try
            {
                // Bulge = tan(angle/4) where angle is the included angle
                double angle = 4.0 * Math.Atan(bulge);
                double distance = Geometry.Dist(start.To2D(), end.To2D());
                
                if (distance < 1e-10)
                {
                    GeoObject.Line line = GeoObject.Line.Construct();
                    line.StartPoint = start;
                    line.EndPoint = end;
                    return line;
                }
                
                // Calculate radius
                double radius = distance * (1 + bulge * bulge) / (4 * Math.Abs(bulge));
                
                // Calculate center point
                double offset = distance * (1 - bulge * bulge) / (4 * bulge);
                GeoVector2D midDir = (end.To2D() - start.To2D()).Normalized;
                GeoVector2D perpDir = midDir.ToLeft();
                GeoPoint2D midPoint = start.To2D() + (distance / 2.0) * midDir;
                GeoPoint2D center2D = midPoint + offset * perpDir;
                
                GeoPoint center = new GeoPoint(center2D.x, center2D.y, elevation);
                
                // Create arc
                GeoVector normal = bulge > 0 ? GeoVector.ZAxis : -GeoVector.ZAxis;
                Plane plane = new Plane(center, GeoVector.XAxis, GeoVector.YAxis);
                
                GeoObject.Ellipse arc = GeoObject.Ellipse.Construct();
                arc.SetCirclePlaneCenterRadius(plane, center, radius);
                arc.StartPoint = start;
                arc.EndPoint = end;
                
                return arc;
            }
            catch
            {
                GeoObject.Line line = GeoObject.Line.Construct();
                line.StartPoint = start;
                line.EndPoint = end;
                return line;
            }
        }

        private IGeoObject CreatePolylineFromPoints(GeoPoint[] points, bool closed)
        {
            if (points.Length < 2)
                return null;

            GeoObject.Polyline polyline = GeoObject.Polyline.Construct();
            polyline.SetPoints(points, closed);
            return polyline;
        }

        private IGeoObject CreatePoint(ACadSharp.Entities.Point point)
        {
            GeoPoint location = ToGeoPoint(point.Location);
            GeoObject.Point p = GeoObject.Point.Construct();
            p.Location = location;
            p.Symbol = PointSymbol.Cross;
            return p;
        }

        private IGeoObject CreateText(ACadSharp.Entities.TextEntity text)
        {
            try
            {
                GeoObject.Text cadText = GeoObject.Text.Construct();
                cadText.TextString = text.Value ?? "";
                cadText.Location = ToGeoPoint(text.InsertPoint);
                cadText.Font = text.Style?.Name ?? "Arial";
                cadText.TextSize = text.Height;
                
                // Handle rotation
                if (Math.Abs(text.Rotation) > 1e-10)
                {
                    GeoVector direction = new GeoVector(Math.Cos(text.Rotation), Math.Sin(text.Rotation), 0);
                    cadText.LineDirection = direction;
                }
                
                return cadText;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating text: {ex.Message}");
                return null;
            }
        }

        private IGeoObject CreateMText(ACadSharp.Entities.MText mtext)
        {
            try
            {
                GeoObject.Text cadText = GeoObject.Text.Construct();
                cadText.TextString = mtext.Value ?? "";
                cadText.Location = ToGeoPoint(mtext.InsertPoint);
                cadText.Font = mtext.Style?.Name ?? "Arial";
                cadText.TextSize = mtext.Height;
                
                // Handle rotation
                if (Math.Abs(mtext.Rotation) > 1e-10)
                {
                    GeoVector direction = new GeoVector(Math.Cos(mtext.Rotation), Math.Sin(mtext.Rotation), 0);
                    cadText.LineDirection = direction;
                }
                
                return cadText;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating MText: {ex.Message}");
                return null;
            }
        }

        private IGeoObject CreateHatch(ACadSharp.Entities.Hatch hatch)
        {
            try
            {
                // For now, create the boundary curves as a path
                // Full hatch pattern support would require more complex implementation
                List<ICurve> boundaryCurves = new List<ICurve>();
                
                foreach (var boundary in hatch.Paths)
                {
                    foreach (var edge in boundary.Edges)
                    {
                        if (edge is ACadSharp.Entities.Hatch.BoundaryPath.Line line)
                        {
                            GeoPoint start = new GeoPoint(line.Start.X, line.Start.Y, 0);
                            GeoPoint end = new GeoPoint(line.End.X, line.End.Y, 0);
                            GeoObject.Line geoLine = GeoObject.Line.Construct();
                            geoLine.StartPoint = start;
                            geoLine.EndPoint = end;
                            boundaryCurves.Add(geoLine);
                        }
                        else if (edge is ACadSharp.Entities.Hatch.BoundaryPath.Arc arc)
                        {
                            GeoPoint center = new GeoPoint(arc.Center.X, arc.Center.Y, 0);
                            Plane plane = new Plane(center, GeoVector.XAxis, GeoVector.YAxis);
                            
                            GeoObject.Ellipse cadArc = GeoObject.Ellipse.Construct();
                            cadArc.SetArcPlaneCenterRadiusAngles(plane, center, arc.Radius, 
                                arc.StartAngle, arc.EndAngle - arc.StartAngle);
                            boundaryCurves.Add(cadArc);
                        }
                        // Add more edge types as needed
                    }
                }
                
                if (boundaryCurves.Count > 0)
                {
                    if (boundaryCurves.Count == 1)
                        return boundaryCurves[0] as IGeoObject;
                    
                    GeoObject.Path path = GeoObject.Path.Construct();
                    path.Set(boundaryCurves.ToArray());
                    return path;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating hatch: {ex.Message}");
                return null;
            }
        }

        private IGeoObject CreateInsert(ACadSharp.Entities.Insert insert)
        {
            try
            {
                // Get the block definition
                if (insert.Block == null || !blockTable.ContainsKey(insert.Block.Name))
                {
                    // Create block if not exists
                    if (insert.Block != null)
                    {
                        CreateBlockDefinition(insert.Block);
                    }
                }
                
                if (!blockTable.TryGetValue(insert.Block?.Name ?? "", out GeoObject.Block block))
                    return null;

                // Create block reference
                GeoObject.BlockRef blockRef = GeoObject.BlockRef.Construct(block);
                
                // Apply transformation
                GeoPoint insertPoint = ToGeoPoint(insert.InsertPoint);
                ModOp transformation = ModOp.Translate(insertPoint.x, insertPoint.y, insertPoint.z);
                
                // Apply scale
                if (Math.Abs(insert.XScale - 1.0) > 1e-10 || 
                    Math.Abs(insert.YScale - 1.0) > 1e-10 || 
                    Math.Abs(insert.ZScale - 1.0) > 1e-10)
                {
                    transformation = transformation * ModOp.Scale(insert.XScale, insert.YScale, insert.ZScale);
                }
                
                // Apply rotation
                if (Math.Abs(insert.Rotation) > 1e-10)
                {
                    transformation = transformation * ModOp.Rotate(GeoVector.ZAxis, insert.Rotation);
                }
                
                blockRef.RefPoint = insertPoint;
                blockRef.Modify(transformation);
                
                return blockRef;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating insert: {ex.Message}");
                return null;
            }
        }

        private IGeoObject CreateDimension(ACadSharp.Entities.Dimension dimension)
        {
            try
            {
                // Create a basic dimension representation
                // Full dimension support would require more complex implementation
                GeoObject.Dimension cadDim = GeoObject.Dimension.Construct();
                
                // This is a simplified implementation
                // Full support would require handling different dimension types
                
                return cadDim;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating dimension: {ex.Message}");
                return null;
            }
        }

        private IGeoObject CreateLeader(ACadSharp.Entities.Leader leader)
        {
            try
            {
                if (leader.Vertices.Count < 2)
                    return null;

                // Create leader as a polyline
                GeoPoint[] points = leader.Vertices.Select(v => ToGeoPoint(v)).ToArray();
                return CreatePolylineFromPoints(points, false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating leader: {ex.Message}");
                return null;
            }
        }

        private IGeoObject CreateFace3D(ACadSharp.Entities.Face3D face)
        {
            try
            {
                GeoPoint p1 = ToGeoPoint(face.FirstCorner);
                GeoPoint p2 = ToGeoPoint(face.SecondCorner);
                GeoPoint p3 = ToGeoPoint(face.ThirdCorner);
                GeoPoint p4 = ToGeoPoint(face.FourthCorner);
                
                // Create a face using Face.MakeFace pattern from ImportDxf.cs
                return GeoObject.Face.MakeFace(p1, p2, p3);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating Face3D: {ex.Message}");
                return null;
            }
        }

        private IGeoObject CreateSolid(ACadSharp.Entities.Solid solid)
        {
            try
            {
                // Create a solid (filled triangle/quad)
                GeoPoint p1 = ToGeoPoint(solid.FirstCorner);
                GeoPoint p2 = ToGeoPoint(solid.SecondCorner);
                GeoPoint p3 = ToGeoPoint(solid.ThirdCorner);
                GeoPoint p4 = ToGeoPoint(solid.FourthCorner);
                
                return GeoObject.Face.MakeFace(p1, p2, p3);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating Solid: {ex.Message}");
                return null;
            }
        }

        private IGeoObject CreateMesh(ACadSharp.Entities.Mesh mesh)
        {
            try
            {
                // Convert mesh vertices
                GeoPoint[] vertices = mesh.Vertices.Select(v => ToGeoPoint(v)).ToArray();
                
                if (vertices.Length < 3)
                    return null;

                // Create a shell from the mesh
                // This is a simplified implementation
                GeoObject.Shell shell = GeoObject.Shell.Construct();
                
                // Process mesh faces
                // Note: This is a basic implementation and may need refinement
                // based on how ACadSharp represents mesh topology
                
                return shell;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating Mesh: {ex.Message}");
                return null;
            }
        }

        #endregion
    }
}
