using System;
using System.Collections.Generic;
using netDxf;
using netDxf.Entities;
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
using netDxf.Tables;
using System.Text;
using System.IO;

namespace CADability.DXF
{
    // ODAFileConverter "C:\Zeichnungen\DxfDwg\Stahl" "C:\Zeichnungen\DxfDwg\StahlConverted" "ACAD2010" "DWG" "0" "0"
    // only converts whole directories.
    /// <summary>
    /// Imports a DXF file, converts it to a project
    /// </summary>
    public class Import
    {
        private DxfDocument doc;
        private Project project;
        private Dictionary<string, GeoObject.Block> blockTable;
        private Dictionary<netDxf.Tables.Layer, ColorDef> layerColorTable;
        private Dictionary<netDxf.Tables.Layer, Attribute.Layer> layerTable;
        /// <summary>
        /// Create the Import instance. The document is being read and converted to netDXF objects.
        /// </summary>
        /// <param name="fileName"></param>
        public Import(string fileName)
        {
            using (Stream stream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                MathHelper.Epsilon = 1e-8;
                doc = DxfDocument.Load(stream);
            }
        }
        public static bool CanImportVersion(string fileName)
        {
            netDxf.Header.DxfVersion ver = DxfDocument.CheckDxfFileVersion(fileName, out bool isBinary);
            return ver >= netDxf.Header.DxfVersion.AutoCad2000;
        }
        private void FillModelSpace(Model model)
        {
            netDxf.Blocks.Block modelSpace = doc.Blocks["*Model_Space"];
            foreach (EntityObject item in modelSpace.Entities)
            {
                IGeoObject geoObject = GeoObjectFromEntity(item);
                if (geoObject != null) model.Add(geoObject);
            }
            model.Name = "*Model_Space";
        }
        private void FillPaperSpace(Model model)
        {
            netDxf.Blocks.Block modelSpace = doc.Blocks["*Paper_Space"];
            foreach (EntityObject item in modelSpace.Entities)
            {
                IGeoObject geoObject = GeoObjectFromEntity(item);
                if (geoObject != null) model.Add(geoObject);
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
            layerColorTable = new Dictionary<netDxf.Tables.Layer, ColorDef>();
            layerTable = new Dictionary<netDxf.Tables.Layer, Attribute.Layer>();
            foreach (var item in doc.Layers)
            {
                Attribute.Layer layer = project.LayerList.CreateOrFind(item.Name);
                layerTable[item] = layer;
                Color rgb = item.Color.ToColor();
                if (rgb.ToArgb() == Color.White.ToArgb()) rgb = Color.Black;
                ColorDef cd = project.ColorList.CreateOrFind(item.Name + ":ByLayer", rgb);
                layerColorTable[item] = cd;
            }
            foreach (var item in doc.Linetypes)
            {
                List<double> pattern = new List<double>();
                for (int i = 0; i < item.Segments.Count; i++)
                {
                    if (item.Segments[i].Type == LinetypeSegmentType.Simple)
                    {
                        pattern.Add(Math.Abs(item.Segments[i].Length));
                    }
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
        private IGeoObject GeoObjectFromEntity(EntityObject item)
        {
            IGeoObject res = null;
            switch (item)
            {
                case netDxf.Entities.Line dxfLine: res = CreateLine(dxfLine); break;
                case netDxf.Entities.Ray dxfRay: res = CreateRay(dxfRay); break;
                case netDxf.Entities.Arc dxfArc: res = CreateArc(dxfArc); break;
                case netDxf.Entities.Circle dxfCircle: res = CreateCircle(dxfCircle); break;
                case netDxf.Entities.Ellipse dxfEllipse: res = CreateEllipse(dxfEllipse); break;
                case netDxf.Entities.Spline dxfSpline: res = CreateSpline(dxfSpline); break;
                case netDxf.Entities.Face3D dxfFace: res = CreateFace(dxfFace); break;
                case netDxf.Entities.PolyfaceMesh dxfPolyfaceMesh: res = CreatePolyfaceMesh(dxfPolyfaceMesh); break;
                case netDxf.Entities.Hatch dxfHatch: res = CreateHatch(dxfHatch); break;
                case netDxf.Entities.Solid dxfSolid: res = CreateSolid(dxfSolid); break;
                case netDxf.Entities.Insert dxfInsert: res = CreateInsert(dxfInsert); break;
                case netDxf.Entities.Polyline2D dxfPolyline2D: res = CreatePolyline2D(dxfPolyline2D); break;
                case netDxf.Entities.MLine dxfMLine: res = CreateMLine(dxfMLine); break;
                case netDxf.Entities.Text dxfText: res = CreateText(dxfText); break;
                case netDxf.Entities.Dimension dxfDimension: res = CreateDimension(dxfDimension); break;
                case netDxf.Entities.MText dxfMText: res = CreateMText(dxfMText); break;
                case netDxf.Entities.Leader dxfLeader: res = CreateLeader(dxfLeader); break;
                case netDxf.Entities.Polyline3D dxfPolyline3D: res = CreatePolyline3D(dxfPolyline3D); break;
                case netDxf.Entities.Point dxfPoint: res = CreatePoint(dxfPoint); break;
                case netDxf.Entities.Mesh dxfMesh: res = CreateMesh(dxfMesh); break;
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
        private static GeoPoint GeoPoint(Vector3 p)
        {
            return new GeoPoint(p.X, p.Y, p.Z);
        }
        private static GeoVector GeoVector(Vector3 p)
        {
            return new GeoVector(p.X, p.Y, p.Z);
        }
        internal static Plane Plane(Vector3 center, Vector3 normal)
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
        private HatchStyleLines FindOrCreateHatchStyleLines(netDxf.Entities.EntityObject entity, double lineAngle, double lineDistance, double[] dashes)
        {
            for (int i = 0; i < project.HatchStyleList.Count; i++)
            {
                if (project.HatchStyleList[i] is HatchStyleLines hsl)
                {
                    if (hsl.ColorDef.Color.ToArgb() == entity.Layer.Color.ToColor().ToArgb() && hsl.LineAngle == lineAngle && hsl.LineDistance == lineDistance) return hsl;
                }
            }
            HatchStyleLines nhsl = new HatchStyleLines();
            string name = NewName(entity.Layer.Name, project.HatchStyleList);
            nhsl.Name = name;
            nhsl.LineAngle = lineAngle;
            nhsl.LineDistance = lineDistance;
            nhsl.ColorDef = project.ColorList.CreateOrFind(entity.Layer.Color.ToColor().ToString(), entity.Layer.Color.ToColor());
            Lineweight lw = entity.Lineweight;
            if (lw == Lineweight.ByLayer) lw = entity.Layer.Lineweight;
            if (lw == Lineweight.ByBlock && entity.Owner != null) lw = entity.Owner.Layer.Lineweight; // not sure, but Block doesn't seem to have a lineweight
            if (lw < 0) lw = 0;
            nhsl.LineWidth = project.LineWidthList.CreateOrFind("DXF_" + lw.ToString(), ((int)lw) / 100.0);
            nhsl.LinePattern = FindOrcreateLinePattern(dashes);
            project.HatchStyleList.Add(nhsl);
            return nhsl;
        }
        private ColorDef FindOrCreateColor(AciColor color, netDxf.Tables.Layer layer)
        {
            if (color.IsByLayer && layer != null)
            {
                ColorDef res = layerColorTable[layer] as ColorDef;
                if (res != null) return res;
            }
            Color rgb = color.ToColor();
            if (color.ToColor().ToArgb() == Color.White.ToArgb())
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
        private void SetAttributes(IGeoObject go, netDxf.Entities.EntityObject entity)
        {
            if (go is IColorDef cd) cd.ColorDef = FindOrCreateColor(entity.Color, entity.Layer);
            go.Layer = layerTable[entity.Layer];
            if (go is ILinePattern lp) lp.LinePattern = project.LinePatternList.Find(entity.Linetype.Name);
            if (go is ILineWidth ld)
            {
                Lineweight lw = entity.Lineweight;
                if (lw == Lineweight.ByLayer) lw = entity.Layer.Lineweight;
                if (lw == Lineweight.ByBlock && entity.Owner != null) lw = entity.Owner.Layer.Lineweight; // not sure, but Block doesn't seem to have a lineweight
                if (lw < 0) lw = 0;
                ld.LineWidth = project.LineWidthList.CreateOrFind("DXF_" + lw.ToString(), ((int)lw) / 100.0);
            }
        }
        private void SetUserData(IGeoObject go, netDxf.Entities.EntityObject entity)
        {
            foreach (KeyValuePair<string, XData> item in entity.XData)
            {
                ExtendedEntityData xdata = new ExtendedEntityData();
                xdata.ApplicationName = item.Value.ApplicationRegistry.Name;

                string name = item.Value.ApplicationRegistry.Name + ":" + item.Key;

                for (int i = 0; i < item.Value.XDataRecord.Count; i++)
                {
                    xdata.Data.Add(new KeyValuePair<XDataCode, object>(item.Value.XDataRecord[i].Code, item.Value.XDataRecord[i].Value));
                }

                go.UserData.Add(name, xdata);
            }
            go.UserData["DxfImport.Handle"] = new UserInterface.StringProperty(entity.Handle, "DxfImport.Handle");
        }
        private GeoObject.Block FindBlock(netDxf.Blocks.Block entity)
        {
            if (!blockTable.TryGetValue(entity.Handle, out GeoObject.Block found))
            {
                found = GeoObject.Block.Construct();
                found.Name = entity.Name;
                found.RefPoint = GeoPoint(entity.Origin);
                for (int i = 0; i < entity.Entities.Count; i++)
                {
                    IGeoObject go = GeoObjectFromEntity(entity.Entities[i]);
                    if (go != null) found.Add(go);
                }
                blockTable[entity.Handle] = found;
            }
            return found;
        }
        private IGeoObject CreateLine(netDxf.Entities.Line line)
        {
            GeoObject.Line l = GeoObject.Line.Construct();
            Vector3 sp = line.StartPoint;
            Vector3 ep = line.EndPoint;
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
        private IGeoObject CreateRay(Ray ray)
        {
            GeoObject.Line l = GeoObject.Line.Construct();
            Vector3 sp = ray.Origin;
            Vector3 dir = ray.Direction;
            l.StartPoint = GeoPoint(sp);
            l.EndPoint = l.StartPoint + GeoVector(dir);
            return l;
        }
        private IGeoObject CreateArc(Arc arc)
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

        private IGeoObject CreateCircle(netDxf.Entities.Circle circle)
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
        private IGeoObject CreateEllipse(netDxf.Entities.Ellipse ellipse)
        {
            GeoObject.Ellipse e = GeoObject.Ellipse.Construct();
            Plane plane = Plane(ellipse.Center, ellipse.Normal);
            ModOp2D rot = ModOp2D.Rotate(Angle.Deg(ellipse.Rotation));
            GeoVector2D majorAxis = 0.5 * ellipse.MajorAxis * (rot * GeoVector2D.XAxis);
            GeoVector2D minorAxis = 0.5 * ellipse.MinorAxis * (rot * GeoVector2D.YAxis);
            e.SetEllipseCenterAxis(GeoPoint(ellipse.Center), plane.ToGlobal(majorAxis), plane.ToGlobal(minorAxis));

            Vector2 startPoint = ellipse.PolarCoordinateRelativeToCenter(ellipse.StartAngle);
            double sp = CalcStartEndParameter(startPoint, ellipse.MajorAxis, ellipse.MinorAxis);

            Vector2 endPoint = ellipse.PolarCoordinateRelativeToCenter(ellipse.EndAngle);
            double ep = CalcStartEndParameter(endPoint, ellipse.MajorAxis, ellipse.MinorAxis);

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

        private IGeoObject CreateSpline(netDxf.Entities.Spline spline)
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

                        netDxf.Entities.Polyline2D p2d = spline.ToPolyline2D(usedCurves);
                        var res = CreatePolyline2D(p2d);
                        
                        return res;
                    }

                    return bsp;
                }
                // strange spline in "bspline-closed-periodic.dxf"
            }
            return null;
        }

        private IGeoObject CreateFace(netDxf.Entities.Face3D face)
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
        private IGeoObject CreatePolyfaceMesh(netDxf.Entities.PolyfaceMesh polyfacemesh)
        {
            polyfacemesh.Explode();

            GeoPoint[] vertices = new GeoPoint[polyfacemesh.Vertexes.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = GeoPoint(polyfacemesh.Vertexes[i]); // there is more information, I would need a good example
            }

            List<Face> faces = new List<Face>();
            for (int i = 0; i < polyfacemesh.Faces.Count; i++)
            {
                short[] indices = polyfacemesh.Faces[i].VertexIndexes;
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
        private IGeoObject CreateHatch(netDxf.Entities.Hatch hatch)
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
                if (hatch.Pattern.Fill == HatchFillType.SolidFill)
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
        private IGeoObject CreateSolid(netDxf.Entities.Solid solid)
        {
            Plane ocs = Plane(new Vector3(solid.Elevation * solid.Normal.X, solid.Elevation * solid.Normal.Y, solid.Elevation * solid.Normal.Z), solid.Normal);
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
        private IGeoObject CreateInsert(netDxf.Entities.Insert insert)
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
        private IGeoObject CreatePolyline2D(netDxf.Entities.Polyline2D polyline2D)
        {
            List<EntityObject> exploded = polyline2D.Explode();
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
        private IGeoObject CreateMLine(netDxf.Entities.MLine mLine)
        {
            List<EntityObject> exploded = mLine.Explode();
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
        private IGeoObject CreateText(netDxf.Entities.Text txt)
        {
            GeoObject.Text text = GeoObject.Text.Construct();
            string txtstring = processAcadString(txt.Value);
            if (txtstring.Trim().Length == 0) return null;
            string filename;
            string name;
            string typeface;
            bool bold;
            bool italic;
            filename = txt.Style.FontFamilyName;
            if (string.IsNullOrEmpty(filename)) filename = txt.Style.FontFile;
            name = txt.Style.Name;
            typeface = "";
            bold = txt.Style.FontStyle.HasFlag(netDxf.Tables.FontStyle.Bold);
            italic = txt.Style.FontStyle.HasFlag(netDxf.Tables.FontStyle.Italic);
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
            text.LineDirection = h * CADability.GeoVector.XAxis; //plane.ToGlobal(new GeoVector2D(a));
            text.GlyphDirection = h * CADability.GeoVector.YAxis; // plane.ToGlobal(new GeoVector2D(a + SweepAngle.ToLeft));
            text.TextSize = h;
            text.Alignment = GeoObject.Text.AlignMode.Bottom;
            text.LineAlignment = GeoObject.Text.LineAlignMode.Left;
            switch (txt.Alignment)
            {
                case TextAlignment.Aligned:
                case TextAlignment.Fit: // fit in width or height: not implemented
                    text.LineAlignment = GeoObject.Text.LineAlignMode.Left;
                    text.Alignment = GeoObject.Text.AlignMode.Baseline;
                    break;
                case TextAlignment.BaselineLeft:
                    text.LineAlignment = GeoObject.Text.LineAlignMode.Left;
                    text.Alignment = GeoObject.Text.AlignMode.Baseline;
                    break;
                case TextAlignment.BaselineCenter:
                    text.LineAlignment = GeoObject.Text.LineAlignMode.Center;
                    text.Alignment = GeoObject.Text.AlignMode.Baseline;
                    break;
                case TextAlignment.BaselineRight:
                    text.LineAlignment = GeoObject.Text.LineAlignMode.Right;
                    text.Alignment = GeoObject.Text.AlignMode.Baseline;
                    break;
                case TextAlignment.BottomLeft:
                    text.LineAlignment = GeoObject.Text.LineAlignMode.Left;
                    text.Alignment = GeoObject.Text.AlignMode.Bottom;
                    break;
                case TextAlignment.BottomCenter:
                    text.LineAlignment = GeoObject.Text.LineAlignMode.Center;
                    text.Alignment = GeoObject.Text.AlignMode.Bottom;
                    break;
                case TextAlignment.BottomRight:
                    text.LineAlignment = GeoObject.Text.LineAlignMode.Right;
                    text.Alignment = GeoObject.Text.AlignMode.Bottom;
                    break;
                case TextAlignment.MiddleLeft:
                    text.LineAlignment = GeoObject.Text.LineAlignMode.Left;
                    text.Alignment = GeoObject.Text.AlignMode.Center;
                    break;
                case TextAlignment.Middle:
                case TextAlignment.MiddleCenter:
                    text.LineAlignment = GeoObject.Text.LineAlignMode.Center;
                    text.Alignment = GeoObject.Text.AlignMode.Center;
                    break;
                case TextAlignment.MiddleRight:
                    text.LineAlignment = GeoObject.Text.LineAlignMode.Right;
                    text.Alignment = GeoObject.Text.AlignMode.Center;
                    break;
                case TextAlignment.TopLeft:
                    text.LineAlignment = GeoObject.Text.LineAlignMode.Left;
                    text.Alignment = GeoObject.Text.AlignMode.Top;
                    break;
                case TextAlignment.TopCenter:
                    text.LineAlignment = GeoObject.Text.LineAlignMode.Center;
                    text.Alignment = GeoObject.Text.AlignMode.Top;
                    break;
                case TextAlignment.TopRight:
                    text.LineAlignment = GeoObject.Text.LineAlignMode.Right;
                    text.Alignment = GeoObject.Text.AlignMode.Top;
                    break;
            }
            // h /= GetFontScaling(text.Font, fnt);
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
        private IGeoObject CreateDimension(netDxf.Entities.Dimension dimension)
        {
            // we could create a CADability Dimension object usind the dimension data and setting the block with the FindBlock values.
            // but then we would need a "CustomBlock" flag in the CADability Dimension object and also save this Block
            if (dimension.Block != null)
            {
                GeoObject.Block block = FindBlock(dimension.Block);
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
        private IGeoObject CreateMText(netDxf.Entities.MText mText)
        {
            // this has to be splitted in chunks (see sourcecode in dwgmtext.cpp) we could implement a MText to List<Text> method in netDxf library
            netDxf.Entities.Text txt = new netDxf.Entities.Text()
            {
                Value = mText.PlainText(),
                Height = mText.Height,
                // Width = mText.Height, // width is not used in CreateText (should be used for align.fit) but may not be 0
                WidthFactor = 1.0,
                Rotation = mText.Rotation,
                ObliqueAngle = mText.Style.ObliqueAngle,
                // IsBackward = false,
                // IsUpsideDown = false,
                Style = mText.Style,
                Position = mText.Position,
                Normal = mText.Normal,
                Alignment = TextAlignment.BaselineLeft
            };
            return CreateText(txt);
        }
        private IGeoObject CreateLeader(netDxf.Entities.Leader leader)
        {
            Plane ocs = Plane(new Vector3(leader.Elevation * leader.Normal.X, leader.Elevation * leader.Normal.Y, leader.Elevation * leader.Normal.Z), leader.Normal);
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
        private IGeoObject CreatePolyline3D(netDxf.Entities.Polyline3D polyline3D)
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
        private IGeoObject CreatePoint(netDxf.Entities.Point point)
        {
            CADability.GeoObject.Point p = CADability.GeoObject.Point.Construct();
            p.Location = GeoPoint(point.Position);
            p.Symbol = PointSymbol.Cross;
            return p;
        }
        private IGeoObject CreateMesh(netDxf.Entities.Mesh mesh)
        {
            GeoPoint[] vertices = new GeoPoint[mesh.Vertexes.Count];
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = GeoPoint(mesh.Vertexes[i]);
            }
            List<Face> faces = new List<Face>();
            for (int i = 0; i < mesh.Faces.Count; i++)
            {
                int[] indices = mesh.Faces[i];
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