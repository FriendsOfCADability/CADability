using System;
using System.Collections.Generic;
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
using System.Text;
using System.IO;
using System.Linq;

namespace CADability.DXF
{
    // ODAFileConverter "C:\Zeichnungen\DxfDwg\Stahl" "C:\Zeichnungen\DxfDwg\StahlConverted" "ACAD2010" "DWG" "0" "0"
    // only converts whole directories.
    /// <summary>
    /// Imports a DXF file, converts it to a project
    /// </summary>
    public class Import
    {
        private IDxfDocument doc;
        private IDxfLibrary dxfLibrary;
        private Project project;
        private Dictionary<string, GeoObject.Block> blockTable;
        private Dictionary<string, ColorDef> layerColorTable;
        private Dictionary<string, Attribute.Layer> layerTable;
        /// <summary>
        /// Create the Import instance. The document is being read and converted using the DXF abstraction layer.
        /// </summary>
        /// <param name="fileName"></param>
        public Import(string fileName)
        {
            dxfLibrary = DxfLibraryFactory.GetLibrary();
            doc = dxfLibrary.LoadFromFile(fileName);
        }
        public static bool CanImportVersion(string fileName)
        {
            IDxfLibrary library = DxfLibraryFactory.GetLibrary();
            return library.CanImportVersion(fileName);
        }
        private void FillModelSpace(Model model)
        {
            IDxfBlock modelSpace = doc.Blocks.GetBlock("*Model_Space");
            if (modelSpace != null)
            {
                foreach (IDxfEntity item in modelSpace.Entities)
                {
                    IGeoObject geoObject = GeoObjectFromEntity(item);
                    if (geoObject != null) model.Add(geoObject);
                }
            }
            model.Name = "*Model_Space";
        }
        private void FillPaperSpace(Model model)
        {
            IDxfBlock paperSpace = doc.Blocks.GetBlock("*Paper_Space");
            if (paperSpace != null)
            {
                foreach (IDxfEntity item in paperSpace.Entities)
                {
                    IGeoObject geoObject = GeoObjectFromEntity(item);
                    if (geoObject != null) model.Add(geoObject);
                }
            }
            model.Name = "*Paper_Space";
        }
        /// <summary>
        /// creates and returns the project
        /// </summary>
        public Project Project { get => CreateProject(); }
        private Project CreateProject()
        {
            if (doc == null) return null;
            project = Project.CreateSimpleProject();
            blockTable = new Dictionary<string, GeoObject.Block>();
            layerColorTable = new Dictionary<string, ColorDef>();
            layerTable = new Dictionary<string, Attribute.Layer>();
            foreach (var item in doc.Layers)
            {
                Attribute.Layer layer = project.LayerList.CreateOrFind(item.Name);
                layerTable[item.Name] = layer;
                Color rgb = Color.FromArgb(item.ColorArgb);
                if (rgb.ToArgb() == Color.White.ToArgb()) rgb = Color.Black;
                ColorDef cd = project.ColorList.CreateOrFind(item.Name + ":ByLayer", rgb);
                layerColorTable[item.Name] = cd;
            }
            foreach (var item in doc.LineTypes)
            {
                List<double> pattern = new List<double>();
                foreach (double segment in item.Segments)
                {
                    pattern.Add(Math.Abs(segment));
                }
                project.LinePatternList.CreateOrFind(item.Name, pattern.ToArray());
            }
            FillModelSpace(project.GetModel(0));
            Model paperSpace = new Model();
            FillPaperSpace(paperSpace);
            if (paperSpace.Count > 0)
            {
                project.AddModel(paperSpace);
                Model modelSpace = project.GetModel(0);
                if (modelSpace.Count == 0)
                {   // if the modelSpace is empty and the paperSpace contains entities, then show the paperSpace
                    for (int i = 0; i < project.ModelViewCount; ++i)
                    {
                        ProjectedModel pm = project.GetProjectedModel(i);
                        if (pm.Model == modelSpace) pm.Model = paperSpace;
                    }
                }
            }
            doc = null;
            return project;
        }
        private IGeoObject GeoObjectFromEntity(IDxfEntity item)
        {
            IGeoObject res = null;
            switch (item.EntityType)
        {
                case DxfEntityType.Line: res = CreateLine((IDxfLine)item); break;
                case DxfEntityType.Ray: res = CreateRay((IDxfRay)item); break;
                case DxfEntityType.Arc: res = CreateArc((IDxfArc)item); break;
                case DxfEntityType.Circle: res = CreateCircle((IDxfCircle)item); break;
                case DxfEntityType.Ellipse: res = CreateEllipse((IDxfEllipse)item); break;
                case DxfEntityType.Spline: res = CreateSpline((IDxfSpline)item); break;
                case DxfEntityType.Face3D: res = CreateFace((IDxfFace3D)item); break;
                case DxfEntityType.PolyfaceMesh: res = CreatePolyfaceMesh((IDxfPolyfaceMesh)item); break;
                case DxfEntityType.Hatch: res = CreateHatch((IDxfHatch)item); break;
                case DxfEntityType.Solid: res = CreateSolid((IDxfSolid)item); break;
                case DxfEntityType.Insert: res = CreateInsert((IDxfInsert)item); break;
                case DxfEntityType.Polyline2D: res = CreatePolyline2D((IDxfPolyline2D)item); break;
                case DxfEntityType.MLine: res = CreateMLine((IDxfMLine)item); break;
                case DxfEntityType.Text: res = CreateText((IDxfText)item); break;
                case DxfEntityType.Dimension: res = CreateDimension((IDxfDimension)item); break;
                case DxfEntityType.MText: res = CreateMText((IDxfMText)item); break;
                case DxfEntityType.Leader: res = CreateLeader((IDxfLeader)item); break;
                case DxfEntityType.Polyline3D: res = CreatePolyline3D((IDxfPolyline3D)item); break;
                case DxfEntityType.Point: res = CreatePoint((IDxfPoint)item); break;
                case DxfEntityType.Mesh: res = CreateMesh((IDxfMesh)item); break;
                default:
                    System.Diagnostics.Trace.WriteLine("dxf: not imported: " + item.ToString());
                    break;
            }
            if (res != null)
            {
                SetAttributes(res, item);
                SetUserData(res, item);
                res.IsVisible = item.IsVisible;
            }
            return res;
        }
        private static GeoPoint GeoPoint((double X, double Y, double Z) p)
        {
            return new GeoPoint(p.X, p.Y, p.Z);
        }
        private static GeoVector GeoVector((double X, double Y, double Z) p)
        {
            return new GeoVector(p.X, p.Y, p.Z);
        }
        internal static Plane Plane((double X, double Y, double Z) center, (double X, double Y, double Z) normal)
        {
            // this is AutoCADs arbitrary axis algorithm we must use here to get the correct plane
            // because sometimes we need the correct x-axis, y-axis orientation
            //Let the world Y axis be called Wy, which is always(0, 1, 0).
            //Let the world Z axis be called Wz, which is always(0, 0, 1).
            //If(abs(Nx) < 1 / 64) and(abs(Ny) < 1 / 64) then
            //     Ax = Wy X N(where “X” is the cross - product operator).
            //Otherwise,
            //     Ax = Wz X N.
            //Scale Ax to unit length.

            GeoVector n = GeoVector(normal);
            GeoVector ax = (Math.Abs(normal.X) < 1.0 / 64 && Math.Abs(normal.Y) < 1.0 / 64) ? CADability.GeoVector.YAxis ^ n : CADability.GeoVector.ZAxis ^ n;
            GeoVector ay = n ^ ax;
            return new Plane(GeoPoint(center), ax, ay);
        }
        private HatchStyleSolid FindOrCreateSolidHatchStyle(Color clr)
        {
            for (int i = 0; i < project.HatchStyleList.Count; i++)
            {
                if (project.HatchStyleList[i] is HatchStyleSolid hss)
                {
                    if (hss.Color.Color.ToArgb() == clr.ToArgb()) return hss;
                }
            }
            HatchStyleSolid nhss = new HatchStyleSolid();
            nhss.Name = "Solid_" + clr.ToString();
            nhss.Color = project.ColorList.CreateOrFind(clr.ToString(), clr);
            project.HatchStyleList.Add(nhss);
            return nhss;
        }
        private HatchStyleLines FindOrCreateHatchStyleLines(IDxfEntity entity, double lineAngle, double lineDistance, double[] dashes)
        {
            for (int i = 0; i < project.HatchStyleList.Count; i++)
            {
                if (project.HatchStyleList[i] is HatchStyleLines hsl)
                {
                    if (hsl.ColorDef.Color.ToArgb() == Color.FromArgb(layerTable[entity.LayerName] != null ? layerColorTable[entity.LayerName].Color.ToArgb() : Color.Black.ToArgb()).ToArgb() && hsl.LineAngle == lineAngle && hsl.LineDistance == lineDistance) return hsl;
                }
            }
            HatchStyleLines nhsl = new HatchStyleLines();
            string name = NewName(entity.Layer.Name, project.HatchStyleList);
            nhsl.Name = name;
            nhsl.LineAngle = lineAngle;
            nhsl.LineDistance = lineDistance;
            nhsl.ColorDef = project.ColorList.CreateOrFind(Color.FromArgb(layerTable[entity.LayerName] != null ? layerColorTable[entity.LayerName].Color.ToArgb() : Color.Black.ToArgb()).ToString(), Color.FromArgb(layerTable[entity.LayerName] != null ? layerColorTable[entity.LayerName].Color.ToArgb() : Color.Black.ToArgb()));
            Lineweight lw = entity.Lineweight;
            if (lw == Lineweight.ByLayer) lw = entity.Layer.Lineweight;
            if (lw == Lineweight.ByBlock && entity.Owner != null) lw = entity.Owner.Layer.Lineweight; // not sure, but Block doesn't seem to have a lineweight
            if (lw < 0) lw = 0;
            nhsl.LineWidth = project.LineWidthList.CreateOrFind("DXF_" + lw.ToString(), ((int)lw) / 100.0);
            nhsl.LinePattern = FindOrcreateLinePattern(dashes);
            project.HatchStyleList.Add(nhsl);
            return nhsl;
        }
        private ColorDef FindOrCreateColor(int? colorArgb, string layerName)
        {
            if (colorArgb == null && layerName != null && layerColorTable.TryGetValue(layerName, out ColorDef layerColor))
            {
                return layerColor;
            }
            if (colorArgb == null) colorArgb = Color.White.ToArgb();
            
            Color rgb = Color.FromArgb(colorArgb.Value);
            if (rgb.ToArgb() == Color.White.ToArgb())
            {
                rgb = Color.Black;
            }
            string colorname = rgb.ToString();
            return project.ColorList.CreateOrFind(colorname, rgb);
        }
        private string NewName(string prefix, IAttributeList list)
        {
            string name = prefix;
            while (list.Find(name) != null)
            {
                string[] parts = name.Split('_');
                if (parts.Length >= 2 && int.TryParse(parts[parts.Length - 1], out int nn))
                {
                    parts[parts.Length - 1] = (nn + 1).ToString();
                    name = parts[0];
                    for (int j = 1; j < parts.Length; j++) name += parts[j];
                }
                else
                {
                    name += "_1";
                }
            }
            return name;
        }
        private LinePattern FindOrcreateLinePattern(double[] dashes, string name = null)
        {
            // in CADability a line pattern always starts with a stroke (dash) followed by a gap (space). In DXF positiv is stroke, negative is gap
            if (dashes.Length == 0)
            {
                for (int i = 0; i < project.LinePatternList.Count; i++)
                {
                    if (project.LinePatternList[i].Pattern == null || project.LinePatternList[i].Pattern.Length == 0) return project.LinePatternList[i];
                }
                return new LinePattern(NewName("DXFpattern", project.LinePatternList));
            }
            if (dashes[0] < 0)
            {
                List<double> pattern = new List<double>(dashes);
                if (pattern[pattern.Count - 1] > 0)
                {
                    pattern.Insert(0, pattern[pattern.Count - 1]);
                    pattern.RemoveAt(pattern.Count - 1);
                }
                else
                {   // a pattern that starts with a gap and ends with a gap, what does this mean?
                    pattern.Insert(0, 0.0);
                }
                if ((pattern.Count & 0x01) != 0) pattern.Add(0.0); // there must be an even number (stroke-gap appear in pairs)
                dashes = pattern.ToArray();
            }
            else if ((dashes.Length & 0x01) != 0)
            {
                List<double> pattern = new List<double>(dashes);
                pattern.Add(0.0);
                dashes = pattern.ToArray();
            }
            return new LinePattern(NewName("DXFpattern", project.LinePatternList), dashes);
        }
        private void SetAttributes(IGeoObject go, IDxfEntity entity)
        {
            if (go is IColorDef cd) cd.ColorDef = FindOrCreateColor(entity.ColorArgb, entity.LayerName);
            if (entity.LayerName != null && layerTable.TryGetValue(entity.LayerName, out var layer))
                go.Layer = layer;
            if (go is ILinePattern lp && entity.LineTypeName != null) 
                lp.LinePattern = project.LinePatternList.Find(entity.LineTypeName);
            if (go is ILineWidth ld && entity.LineWeight.HasValue)
            {
                int lw = entity.LineWeight.Value;
                if (lw < 0) lw = 0;
                ld.LineWidth = project.LineWidthList.CreateOrFind("DXF_" + lw.ToString(), lw / 100.0);
            }
        }
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
        private GeoObject.Block FindBlock(IDxfBlock entity)
        {
            if (!blockTable.TryGetValue(entity.Handle, out GeoObject.Block found))
            {
                found = GeoObject.Block.Construct();
                found.Name = entity.Name;
                found.RefPoint = GeoPoint(entity.Origin);
                foreach (IDxfEntity item in entity.Entities)
                {
                    IGeoObject go = GeoObjectFromEntity(item);
                    if (go != null) found.Add(go);
                }
                blockTable[entity.Handle] = found;
            }
            return found;
        }
        private IGeoObject CreateLine(IDxfLine line)
        {
            GeoObject.Line l = GeoObject.Line.Construct();
            var sp = line.StartPoint;
            var ep = line.EndPoint;
            {
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
        }
        private IGeoObject CreateRay(IDxfRay ray)
        {
            GeoObject.Line l = GeoObject.Line.Construct();
            var sp = ray.Origin;
            var dir = ray.Direction;
            l.StartPoint = GeoPoint(sp);
            l.EndPoint = l.StartPoint + GeoVector(dir);
            return l;
        }
        private IGeoObject CreateArc(IDxfArc arc)
        {
            GeoObject.Ellipse e = GeoObject.Ellipse.Construct();
            GeoVector nor = GeoVector(arc.Normal);
            GeoPoint cnt = GeoPoint(arc.Center);
            Plane plane = Plane(arc.Center, arc.Normal);
            double start = Angle.Deg(arc.StartAngle);
            double end = Angle.Deg(arc.EndAngle);
            double sweep = end - start;
            if (sweep < 0.0) sweep += Math.PI * 2.0;
            //if (sweep < Precision.epsa) sweep = Math.PI * 2.0;
            if (start == end) sweep = 0.0;
            if (start == Math.PI * 2.0 && end == 0.0) sweep = 0.0; // see in modena.dxf
            // Arcs are always counterclockwise, but maybe the normal is (0,0,-1) in 2D drawings.
            e.SetArcPlaneCenterRadiusAngles(plane, GeoPoint(arc.Center), arc.Radius, start, sweep);

            //If an arc is a full circle don't import as ellipse as this will be discarded later by Ellipse.HasValidData() 
            if (e.IsCircle && sweep == 0.0d && Precision.IsEqual(e.StartPoint, e.EndPoint))
            {
                GeoObject.Ellipse circle = GeoObject.Ellipse.Construct();
                circle.SetCirclePlaneCenterRadius(plane, GeoPoint(arc.Center), arc.Radius);
                e = circle;
            }

            double th = arc.Thickness;
            if (th != 0.0 && !nor.IsNullVector())
            {
                return Make3D.Extrude(e, th * nor, null);
            }
            return e;
        }

        private IGeoObject CreateCircle(IDxfCircle circle)
        {
            GeoObject.Ellipse e = GeoObject.Ellipse.Construct();
            Plane plane = Plane(circle.Center, circle.Normal);
            e.SetCirclePlaneCenterRadius(plane, GeoPoint(circle.Center), circle.Radius);
            double th = circle.Thickness;
            GeoVector no = GeoVector(circle.Normal);
            if (th != 0.0 && !no.IsNullVector())
            {
                return Make3D.Extrude(e, th * no, null);
            }
            return e;
        }
        private IGeoObject CreateEllipse(IDxfEllipse ellipse)
        {
            GeoObject.Ellipse e = GeoObject.Ellipse.Construct();
            Plane plane = Plane(ellipse.Center, ellipse.Normal);
            ModOp2D rot = ModOp2D.Rotate(Angle.Deg(ellipse.Rotation));
            GeoVector2D majorAxis = 0.5 * ellipse.MajorAxisEnd.X * 2 * (rot * GeoVector2D.XAxis);
            GeoVector2D minorAxis = 0.5 * ellipse.MinorAxisRatio * ellipse.MajorAxisEnd.X * 2 * (rot * GeoVector2D.YAxis);
            e.SetEllipseCenterAxis(GeoPoint(ellipse.Center), plane.ToGlobal(majorAxis), plane.ToGlobal(minorAxis));

            // Vector2 startPoint calculation removed - using angles directly
            double sp = ellipse.StartAngle;

            // Vector2 endPoint calculation removed - using angles directly
            double ep = ellipse.EndAngle;

            e.StartParameter = sp;
            e.SweepParameter = ep - sp;
            if (e.SweepParameter == 0.0) e.SweepParameter = Math.PI * 2.0;
            if (e.SweepParameter < 0.0) e.SweepParameter += Math.PI * 2.0; // seems it is always counterclockwise
            // it looks like clockwise 2d ellipses are defined with normal vector (0, 0, -1)
            return e;
        }

        private double CalcStartEndParameter(Vector2 startEndPoint, double majorAxis, double minorAxis)
        {
            double a = 1 / (0.5 * majorAxis);
            double b = 1 / (0.5 * minorAxis);
            double parameter = Math.Atan2(startEndPoint.Y * b, startEndPoint.X * a);
            return parameter;
        }

        private IGeoObject CreateSpline(IDxfSpline spline)
        {
            int degree = spline.Degree;
            if (spline.ControlPoints.Length == 0 && spline.FitPoints.Count > 0)
            {
                BSpline bsp = BSpline.Construct();
                GeoPoint[] fp = new GeoPoint[spline.FitPoints.Count];
                for (int i = 0; i < fp.Length; i++)
                {
                    fp[i] = GeoPoint(spline.FitPoints[i]);
                }
                bsp.ThroughPoints(fp, spline.Degree, spline.IsClosed);
                return bsp;
            }
            else
            {
                bool forcePolyline2D = false;
                GeoPoint[] poles = new GeoPoint[spline.ControlPoints.Length];
                double[] weights = new double[spline.ControlPoints.Length];
                for (int i = 0; i < poles.Length; i++)
                {
                    poles[i] = GeoPoint(spline.ControlPoints[i]);
                    weights[i] = spline.Weights[i];

                    if (i > 0 && (poles[i] | poles[i - 1]) < Precision.eps)
                    {
                        forcePolyline2D = true;
                    }
                }
                double[] kn = new double[spline.Knots.Length];
                for (int i = 0; i < kn.Length; ++i)
                {
                    kn[i] = spline.Knots[i];
                }
                if (poles.Length == 2 && degree > 1)
                {   // damit geht kein vernünftiger Spline, höchstens mit degree=1
                    GeoObject.Line l = GeoObject.Line.Construct();
                    l.StartPoint = poles[0];
                    l.EndPoint = poles[1];
                    return l;
                }
                BSpline bsp = BSpline.Construct();
                //TODO: Can Periodic spline be not closed?
                if (bsp.SetData(degree, poles, weights, kn, null, spline.IsClosedPeriodic))
                {
                    // BSplines with inner knots of multiplicity degree+1 make problems, because the spline have no derivative at these points
                    // so we split these splines
                    List<int> splitKnots = new List<int>();
                    for (int i = degree + 1; i < kn.Length - degree - 1; i++)
                    {
                        if (kn[i] == kn[i - 1])
                        {
                            bool sameKnot = true;
                            for (int j = 0; j < degree; j++)
                            {
                                if (kn[i - 1] != kn[i + j]) sameKnot = false;
                            }
                            if (sameKnot) splitKnots.Add(i - 1);
                        }
                    }
                    if (splitKnots.Count > 0)
                    {
                        List<ICurve> parts = new List<ICurve>();
                        BSpline part = bsp.TrimParam(kn[0], kn[splitKnots[0]]);
                        if (CADability.GeoPoint.Distance(part.Poles) > Precision.eps && (part as ICurve).Length > Precision.eps) parts.Add(part);
                        for (int i = 1; i < splitKnots.Count; i++)
                        {
                            part = bsp.TrimParam(kn[splitKnots[i - 1]], kn[splitKnots[i]]);
                            if (CADability.GeoPoint.Distance(part.Poles) > Precision.eps && (part as ICurve).Length > Precision.eps) parts.Add(part);
                        }
                        part = bsp.TrimParam(kn[splitKnots[splitKnots.Count - 1]], kn[kn.Length - 1]);

                        if (CADability.GeoPoint.Distance(part.Poles) > Precision.eps && (part as ICurve).Length > Precision.eps) parts.Add(part);
                        GeoObject.Path path = GeoObject.Path.Construct();
                        path.Set(parts.ToArray());
                        return path;
                    }
                    // if (spline.IsPeriodic) bsp.IsClosed = true; // to remove strange behavior in hünfeld.dxf

                    if (forcePolyline2D)
                    {
                        //Look at https://github.com/SOFAgh/CADability/issues/173 to see why this is done.

                        ICurve curve = (ICurve)bsp;
                        //Use approximate to get the count of lines that will be needed to convert the spline into a Polyline2D
                        double maxError = Settings.GlobalSettings.GetDoubleValue("Approximate.Precision", 0.01);
                        ICurve approxCurve = curve.Approximate(true, maxError);

                        int usedCurves = 0;
                        if (approxCurve is GeoObject.Line || approxCurve.SubCurves.Length == 1 && approxCurve.SubCurves[0] is GeoObject.Line)
                            usedCurves = 2;
                        else
                            usedCurves = approxCurve.SubCurves.Length;

                        // Polyline2D conversion not available in abstraction
                        // var p2d = CreatePolyline2DFromSpline(spline, usedCurves);
                        // return bsp; // fallback to spline
                        
                        return res;
                    }

                    return bsp;
                }
                // strange spline in "bspline-closed-periodic.dxf"
            }
            return null;
        }

        private IGeoObject CreateFace(IDxfFace3D face)
        {
            List<GeoPoint> points = new List<GeoPoint>();
            GeoPoint p = GeoPoint(face.FirstVertex);
            points.Add(p);
            p = GeoPoint(face.SecondVertex);
            if (points[points.Count - 1] != p) points.Add(p);
            p = GeoPoint(face.ThirdVertex);
            if (points[points.Count - 1] != p) points.Add(p);
            p = GeoPoint(face.FourthVertex);
            if (points[points.Count - 1] != p) points.Add(p);
            if (points.Count == 3)
            {
                Plane pln = new Plane(points[0], points[1], points[2]);
                PlaneSurface surf = new PlaneSurface(pln);
                Border bdr = new Border(new GeoPoint2D[] { new GeoPoint2D(0.0, 0.0), pln.Project(points[1]), pln.Project(points[2]) });
                SimpleShape ss = new SimpleShape(bdr);
                Face fc = Face.MakeFace(surf, ss);
                return fc;
            }
            else if (points.Count == 4)
            {
                Plane pln = CADability.Plane.FromPoints(points.ToArray(), out double maxDist, out bool isLinear);
                if (!isLinear)
                {
                    if (maxDist > Precision.eps)
                    {
                        Face fc1 = Face.MakeFace(points[0], points[1], points[2]);
                        Face fc2 = Face.MakeFace(points[0], points[2], points[3]);
                        GeoObject.Block blk = GeoObject.Block.Construct();
                        blk.Set(new GeoObjectList(fc1, fc2));
                        return blk;
                    }
                    else
                    {
                        PlaneSurface surf = new PlaneSurface(pln);
                        Border bdr = new Border(new GeoPoint2D[] { pln.Project(points[0]), pln.Project(points[1]), pln.Project(points[2]), pln.Project(points[3]) });
                        double[] sis = bdr.GetSelfIntersection(Precision.eps);
                        if (sis.Length > 0)
                        {
                            // multiple of three values: parameter1, parameter2, crossproduct of intersection direction
                            // there can only be one intersection
                            Border[] splitted = bdr.Split(new double[] { sis[0], sis[1] });
                            for (int i = 0; i < splitted.Length; i++)
                            {
                                if (splitted[i].IsClosed) bdr = splitted[i];
                            }
                        }
                        SimpleShape ss = new SimpleShape(bdr);
                        Face fc = Face.MakeFace(surf, ss);
                        return fc;
                    }
                }
            }
            return null;

        }
        private IGeoObject CreatePolyfaceMesh(IDxfPolyfaceMesh polyfacemesh)
        {
            polyfacemesh.Explode();

            GeoPoint[] vertices = new GeoPoint[polyfacemesh.Vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = GeoPoint(System.Linq.Enumerable.ElementAt(polyfacemesh.Vertices, i)); // there is more information, I would need a good example
            }

            List<Face> faces = new List<Face>();
            for (int i = 0; i < polyfaceSystem.Linq.Enumerable.Count(mesh.Faces); i++)
            {
                short[] indices = polyfaceSystem.Linq.Enumerable.ElementAt(mesh.Faces, i).VertexIndexes;
                for (int j = 0; j < indices.Length; j++)
                {
                    indices[j] = (short)(Math.Abs(indices[j]) - 1); // why? what does it mean?
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
        }
        private IGeoObject CreateHatch(IDxfHatch hatch)
        {
            CompoundShape cs = null;
            bool ok = true;
            List<ICurve2D> allCurves = new List<ICurve2D>();
            Plane pln = CADability.Plane.XYPlane;
            for (int i = 0; i < hatch.BoundaryPaths.Count; i++)
            {

                // System.Diagnostics.Trace.WriteLine("Loop: " + i.ToString());
                //OdDbHatch.HatchLoopType.kExternal
                // hatch.BoundaryPaths[i].PathType
                List<ICurve> boundaryEntities = new List<ICurve>();
                for (int j = 0; j < hatch.BoundaryPaths[i].Edges.Count; j++)
                {
                    IGeoObject ent = GeoObjectFromEntity(hatch.BoundaryPaths[i].Edges[j].ConvertTo());
                    if (ent is ICurve crv) boundaryEntities.Add(crv);
                }
                //for (int j = 0; j < hatch.BoundaryPaths[i].Entities.Count; j++)
                //{
                //    IGeoObject ent = GeoObjectFromEntity(hatch.BoundaryPaths[i].Entities[j]);
                //    if (ent is ICurve crv) boundaryEntities.Add(crv);
                //}
                if (i == 0)
                {
                    if (!Curves.GetCommonPlane(boundaryEntities, out pln)) return null; // there must be a common plane
                }
                ICurve2D[] bdr = new ICurve2D[boundaryEntities.Count];
                for (int j = 0; j < bdr.Length; j++)
                {
                    bdr[j] = boundaryEntities[j].GetProjectedCurve(pln);
                }
                try
                {
                    Border border = Border.FromUnorientedList(bdr, true);
                    HatchBoundaryPathTypeFlags flag = hatch.BoundaryPaths[i].PathType;
                    allCurves.AddRange(bdr);
                    if (border != null)
                    {
                        SimpleShape ss = new SimpleShape(border);
                        if (cs == null)
                        {
                            cs = new CompoundShape(ss);
                        }
                        else
                        {
                            CompoundShape cs1 = new CompoundShape(ss);
                            double a = cs.Area;
                            cs = cs - new CompoundShape(ss); // assuming the first border is the outer bound followed by holes
                            if (cs.Area >= a) ok = false; // don't know how to descriminate between outer bounds and holes
                        }
                    }
                }
                catch (BorderException)
                {
                }
            }
            if (cs != null)
            {
                if (cs.Area == 0.0 || !ok)
                {   // try to make something usefull from the curves
                    cs = CompoundShape.CreateFromList(allCurves.ToArray(), Precision.eps);
                    if (cs == null || cs.Area == 0.0) return null;
                }
                GeoObject.Hatch res = GeoObject.Hatch.Construct();
                res.CompoundShape = cs;
                res.Plane = pln;
                if (hatch.Pattern.FillType == HatchFillType.SolidFill)
                {
                    HatchStyleSolid hst = FindOrCreateSolidHatchStyle(hatch.Layer.Color.ToColor());
                    res.HatchStyle = hst;
                    return res;
                }
                else
                {
                    GeoObjectList list = new GeoObjectList();
                    for (int i = 0; i < hatch.Pattern.LineDefinitions.Count; i++)
                    {
                        if (i > 0) res = res.Clone() as GeoObject.Hatch;
                        double lineAngle = Angle.Deg(hatch.Pattern.LineDefinitions[i].Angle);
                        double baseX = hatch.Pattern.LineDefinitions[i].Origin.X;
                        double baseY = hatch.Pattern.LineDefinitions[i].Origin.Y;
                        double offsetX = hatch.Pattern.LineDefinitions[i].Delta.X;
                        double offsetY = hatch.Pattern.LineDefinitions[i].Delta.Y;
                        double[] dashes = hatch.Pattern.LineDefinitions[i].DashPattern.ToArray();
                        HatchStyleLines hsl = FindOrCreateHatchStyleLines(hatch, lineAngle, Math.Sqrt(offsetX * offsetX + offsetY * offsetY), dashes);
                        res.HatchStyle = hsl;
                        list.Add(res);
                    }
                    if (list.Count > 1)
                    {
                        GeoObject.Block block = GeoObject.Block.Construct();
                        block.Set(new GeoObjectList(list));
                        return block;
                    }
                    else return res;
                }
            }
            else
            {
                return null;
            }
        }
        private IGeoObject CreateSolid(IDxfSolid solid)
        {
            var normal = solid.Normal;
            Plane ocs = Plane((solid.Elevation * normal.X, solid.Elevation * normal.Y, solid.Elevation * normal.Z), normal);
            // not sure, whether the ocs is correct, maybe the position is (0,0,solid.Elevation)

            HatchStyleSolid hst = FindOrCreateSolidHatchStyle(solid.Color.ToColor());
            List<GeoPoint> points = new List<GeoPoint>();
            points.Add(ocs.ToGlobal(new GeoPoint2D(solid.FirstVertex.X, solid.FirstVertex.Y)));
            points.Add(ocs.ToGlobal(new GeoPoint2D(solid.SecondVertex.X, solid.SecondVertex.Y)));
            points.Add(ocs.ToGlobal(new GeoPoint2D(solid.ThirdVertex.X, solid.ThirdVertex.Y)));
            points.Add(ocs.ToGlobal(new GeoPoint2D(solid.FourthVertex.X, solid.FourthVertex.Y)));
            for (int i = 3; i > 0; --i)
            {   // gleiche Punkte wegmachen
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
        }
        private IGeoObject CreateInsert(IDxfInsert insert)
        {
            // could also use insert.Explode()
            GeoObject.Block block = FindBlock(insert.Block);
            if (block != null)
            {
                IGeoObject res = block.Clone();
                ModOp tranform = ModOp.Translate(GeoVector(insert.Position)) *
                    //ModOp.Translate(block.RefPoint.ToVector()) *
                    ModOp.Rotate(CADability.GeoVector.ZAxis, SweepAngle.Deg(insert.Rotation)) *
                    ModOp.Scale(insert.Scale.X, insert.Scale.Y, insert.Scale.Z) *
                    ModOp.Translate(CADability.GeoPoint.Origin - block.RefPoint);
                res.Modify(tranform);
                return res;
            }
            return null;
        }
        private IGeoObject CreatePolyline2D(IDxfPolyline2D polyline2D)
        {
            var exploded = polyline2D.Explode();
            List<IGeoObject> path = new List<IGeoObject>();
            for (int i = 0; i < exploded.Count; i++)
            {
                IGeoObject ent = GeoObjectFromEntity(exploded[i]);
                if (ent != null) path.Add(ent);
            }
            GeoObject.Path go = GeoObject.Path.Construct();
            go.Set(new GeoObjectList(path), false, 1e-6);
            if (go.CurveCount > 0) return go;
            return null;
        }
        private IGeoObject CreateMLine(IDxfMLine mLine)
        {
            var exploded = mLine.Explode();
            List<IGeoObject> path = new List<IGeoObject>();
            for (int i = 0; i < exploded.Count; i++)
            {
                IGeoObject ent = GeoObjectFromEntity(exploded[i]);
                if (ent != null) path.Add(ent);
            }
            GeoObjectList list = new GeoObjectList(path);
            GeoObjectList res = new GeoObjectList();
            while (list.Count > 0)
            {
                GeoObject.Path go = GeoObject.Path.Construct();
                if (go.Set(list, true, 1e-6))
                {
                    res.Add(go);
                }
                else
                {
                    break;
                }
            }
            if (res.Count > 1)
            {
                GeoObject.Block blk = GeoObject.Block.Construct();
                blk.Name = "MLINE " + mLine.Handle;
                blk.Set(res);
                return blk;
            }
            
            if (res.Count == 1) 
                return res[0];
             
            return null;
        }
        private string processAcadString(string acstr)
        {
            StringBuilder sb = new StringBuilder(acstr);
            sb.Replace("%%153", "Ø");
            sb.Replace("%%127", "°");
            sb.Replace("%%214", "Ö");
            sb.Replace("%%220", "Ü");
            sb.Replace("%%228", "ä");
            sb.Replace("%%246", "ö");
            sb.Replace("%%223", "ß");
            sb.Replace("%%u", ""); // underline
            sb.Replace("%%U", "");
            sb.Replace("%%D", "°");
            sb.Replace("%%d", "°");
            sb.Replace("%%P", "±");
            sb.Replace("%%p", "±");
            sb.Replace("%%C", "Ø");
            sb.Replace("%%c", "Ø");
            sb.Replace("%%%", "%");
            // and maybe some more, is there a documentation?
            return sb.ToString();
        }
        private IGeoObject CreateText(IDxfText txt)
        {
            GeoObject.Text text = GeoObject.Text.Construct();
            string txtstring = processAcadString(txt.Value);
            if (txtstring.Trim().Length == 0) return null;
            string filename;
            string name;
            string typeface;
            bool bold;
            bool italic;
            filename = txt.FontName;
            if (string.IsNullOrEmpty(filename)) filename = txt.FontName;
            name = txt.StyleName;
            typeface = "";
            bold = txt.IsBold;
            italic = txt.IsItalic;
            GeoPoint pos = GeoPoint(txt.Position);
            Angle a = Angle.Deg(txt.Rotation);
            double h = txt.Height;
            Plane plane = Plane(txt.Position, txt.Normal);

            if (typeface.Length > 0)
            {
                text.Font = typeface;
            }
            else
            {
                if (filename.EndsWith(".shx") || filename.EndsWith(".SHX"))
                {
                    filename = filename.Substring(0, filename.Length - 4);
                }
                if (filename.EndsWith(".ttf") || filename.EndsWith(".TTF"))
                {
                    if (name != null && name.Length > 1) filename = name;
                    else filename = filename.Substring(0, filename.Length - 4);
                }
                text.Font = filename;
            }
            text.Bold = bold;
            text.Italic = italic;
            text.TextString = txtstring;
            text.Location = CADability.GeoPoint.Origin;
            text.LineDirection = h * CADability.GeoVector.XAxis;
            text.GlyphDirection = h * CADability.GeoVector.YAxis;
            text.TextSize = h;
            text.Alignment = GeoObject.Text.AlignMode.Bottom;
            text.LineAlignment = GeoObject.Text.LineAlignMode.Left;
            // Note: TextAlignment is not available in the abstraction layer, using default alignment
            text.Location = GeoPoint(txt.Position);
            GeoVector2D dir2d = new GeoVector2D(a);
            GeoVector linedir = plane.ToGlobal(dir2d);
            GeoVector glyphdir = plane.ToGlobal(dir2d.ToLeft());
            text.LineDirection = linedir;
            text.GlyphDirection = glyphdir;
            text.TextSize = h;
            //if (isShx) h *= AdditionalShxFactor(text.Font);
            linedir.Length = h * txt.WidthFactor;
            if (!linedir.IsNullVector()) text.LineDirection = linedir;
            if (text.TextSize < 1e-5) return null;
            return text;
        }
        private IGeoObject CreateDimension(IDxfDimension dimension)
        {
            // we could create a CADability Dimension object usind the dimension data and setting the block with the FindBlock values.
            // but then we would need a "CustomBlock" flag in the CADability Dimension object and also save this Block
            if (dimension.DimensionBlock != null)
            {
                GeoObject.Block block = FindBlock(dimension.DimensionBlock);
                if (block != null)
                {
                    IGeoObject res = block.Clone();
                    return res;
                }
            }
            else
            {
                // make a dimension from the dimension data
            }
            return null;
        }
                private IGeoObject CreateMText(IDxfMText mText)
        {
            GeoObject.Text text = GeoObject.Text.Construct();
            string txtstring = processAcadString(mText.PlainText);
            if (txtstring.Trim().Length == 0) return null;
            
            string filename = mText.FontName ?? mText.StyleName ?? "Arial";
            bool bold = false;
            bool italic = false;
            
            GeoPoint pos = GeoPoint(mText.Position);
            Angle a = Angle.Deg(mText.Rotation);
            double h = mText.Height;
            Plane plane = Plane(mText.Position, mText.Normal);

            text.Font = filename;
            text.Bold = bold;
            text.Italic = italic;
            text.TextString = txtstring;
            text.Location = pos;
            text.LineDirection = h * plane.ToGlobal(new GeoVector2D(a));
            text.GlyphDirection = h * plane.ToGlobal(new GeoVector2D(a + SweepAngle.ToLeft));
            text.TextSize = h;
            text.Alignment = GeoObject.Text.AlignMode.Bottom;
            text.LineAlignment = GeoObject.Text.LineAlignMode.Left;
            
            if (text.TextSize < 1e-5) return null;
            return text;
        }
        private IGeoObject CreateLeader(IDxfLeader leader)
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
            GeoPoint[] vtx = new GeoPoint[leader.Vertexes.Count];
            for (int i = 0; i < vtx.Length; i++)
            {
                vtx[i] = ocs.ToGlobal(new GeoPoint2D(leader.Vertexes[i].X, leader.Vertexes[i].Y));
            }
            GeoObject.Polyline pln = GeoObject.Polyline.Construct();
            pln.SetPoints(vtx, false);
            blk.Add(pln);
            return blk;
        }
        private IGeoObject CreatePolyline3D(IDxfPolyline3D polyline3D)
        {
            // polyline.Explode();
            bool hasWidth = false, hasBulges = false;
            for (int i = 0; i < polyline3D.Vertexes.Count; i++)
            {
                //hasBulges |= polyline.Vertexes[i].Bulge != 0.0;
                //hasWidth |= (polyline.Vertexes[i].StartWidth != 0.0) || (polyline.Vertexes[i].EndWidth != 0.0);
            }
            if (hasWidth && !hasBulges)
            {

            }
            else
            {
                if (hasBulges)
                {   // must be in a single plane

                }
                else
                {
                    GeoObject.Polyline res = GeoObject.Polyline.Construct();
                    for (int i = 0; i < polyline3D.Vertexes.Count; ++i)
                    {
                        res.AddPoint(GeoPoint(polyline3D.Vertexes[i]));
                    }
                    res.IsClosed = polyline3D.IsClosed;
                    if (res.GetExtent(0.0).Size < 1e-6) return null; // only identical points
                    return res;
                }
            }
            return null;
        }
        private IGeoObject CreatePoint(IDxfPoint point)
        {
            CADability.GeoObject.Point p = CADability.GeoObject.Point.Construct();
            p.Location = GeoPoint(point.Position);
            p.Symbol = PointSymbol.Cross;
            return p;
        }
        private IGeoObject CreateMesh(IDxfMesh mesh)
        {
            GeoPoint[] vertices = new GeoPoint[System.Linq.Enumerable.Count(mesh.Vertices)];
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = GeoPoint(System.Linq.Enumerable.ElementAt(mesh.Vertices, i));
            }
            List<Face> faces = new List<Face>();
            for (int i = 0; i < System.Linq.Enumerable.Count(mesh.Faces); i++)
            {
                int[] indices = System.Linq.Enumerable.ElementAt(mesh.Faces, i);
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
        }
    }
}