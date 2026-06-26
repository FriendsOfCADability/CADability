using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using ACadSharp.XData;
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
using Color = System.Drawing.Color;
#endif

namespace CADability.DXF
{
    /// <summary>
    /// Imports a DXF file, converts it to a project
    /// </summary>
    public class Import
    {
        private CadDocument doc;
        private Project project;
        private Dictionary<string, GeoObject.Block> blockTable;
        private Dictionary<string, ColorDef> layerColorTable;
        private Dictionary<string, Attribute.Layer> layerTable;

        public Import(string fileName)
        {
            using (Stream stream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                doc = new DxfReader(stream).Read();
            }
        }

        internal Import(CadDocument document)
        {
            doc = document;
        }

        public static bool CanImportVersion(string fileName)
        {
            return true; // ACadSharp reads all DXF versions (R12 through 2018)
        }

        private void FillModelSpace(Model model)
        {
            if (!doc.BlockRecords.TryGetValue("*Model_Space", out BlockRecord modelSpace)) return;
            foreach (Entity item in modelSpace.Entities)
            {
                IGeoObject geoObject = GeoObjectFromEntity(item);
                if (geoObject != null) model.Add(geoObject);
            }
            model.Name = "*Model_Space";
        }

        private void FillPaperSpace(Model model)
        {
            if (!doc.BlockRecords.TryGetValue("*Paper_Space", out BlockRecord paperSpace)) return;
            foreach (Entity item in paperSpace.Entities)
            {
                IGeoObject geoObject = GeoObjectFromEntity(item);
                if (geoObject != null) model.Add(geoObject);
            }
            model.Name = "*Paper_Space";
        }

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
                Color rgb = AcadColorToDrawing(item.Color);
                if (rgb.ToArgb() == Color.White.ToArgb()) rgb = Color.Black;
                ColorDef cd = project.ColorList.CreateOrFind(item.Name + ":ByLayer", rgb);
                layerColorTable[item.Name] = cd;
            }
            foreach (var item in doc.LineTypes)
            {
                List<double> pattern = new List<double>();
                foreach (var seg in item.Segments)
                {
                    if (!seg.IsShape && !seg.IsText)
                        pattern.Add(Math.Abs(seg.Length));
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
                {
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

        private IGeoObject GeoObjectFromEntity(Entity item)
        {
            IGeoObject res = null;
            switch (item)
            {
                case ACadSharp.Entities.Line dxfLine: res = CreateLine(dxfLine); break;
                case ACadSharp.Entities.Ray dxfRay: res = CreateRay(dxfRay); break;
                case ACadSharp.Entities.XLine dxfXLine: res = CreateXLine(dxfXLine); break;
                case ACadSharp.Entities.Arc dxfArc: res = CreateArc(dxfArc); break;
                case ACadSharp.Entities.Circle dxfCircle: res = CreateCircle(dxfCircle); break;
                case ACadSharp.Entities.Ellipse dxfEllipse: res = CreateEllipse(dxfEllipse); break;
                case ACadSharp.Entities.Spline dxfSpline: res = CreateSpline(dxfSpline); break;
                case ACadSharp.Entities.Face3D dxfFace: res = CreateFace(dxfFace); break;
                case ACadSharp.Entities.PolyfaceMesh dxfPolyfaceMesh: res = CreatePolyfaceMesh(dxfPolyfaceMesh); break;
                case ACadSharp.Entities.Hatch dxfHatch: res = CreateHatch(dxfHatch); break;
                case ACadSharp.Entities.Solid dxfSolid: res = CreateSolid(dxfSolid); break;
                case ACadSharp.Entities.Insert dxfInsert: res = CreateInsert(dxfInsert); break;
                case ACadSharp.Entities.LwPolyline dxfLwPolyline: res = CreateLwPolyline(dxfLwPolyline); break;
                case ACadSharp.Entities.Polyline2D dxfPolyline2D: res = CreatePolyline2D(dxfPolyline2D); break;
                case ACadSharp.Entities.Polyline3D dxfPolyline3D: res = CreatePolyline3D(dxfPolyline3D); break;
                case ACadSharp.Entities.MLine dxfMLine: res = CreateMLine(dxfMLine); break;
                case ACadSharp.Entities.TextEntity dxfText: res = CreateText(dxfText); break;
                case ACadSharp.Entities.Dimension dxfDimension: res = CreateDimension(dxfDimension); break;
                case ACadSharp.Entities.MText dxfMText: res = CreateMText(dxfMText); break;
                case ACadSharp.Entities.Leader dxfLeader: res = CreateLeader(dxfLeader); break;
                case ACadSharp.Entities.Point dxfPoint: res = CreatePoint(dxfPoint); break;
                case ACadSharp.Entities.Mesh dxfMesh: res = CreateMesh(dxfMesh); break;
                case ACadSharp.Entities.Wipeout dxfWipeout: res = CreateWipeout(dxfWipeout); break;
                case ACadSharp.Entities.Tolerance dxfTolerance: res = CreateTolerance(dxfTolerance); break;
                default:
                    System.Diagnostics.Trace.WriteLine("dxf: not imported: " + item.ToString());
                    break;
            }
            if (res != null)
            {
                SetAttributes(res, item);
                SetUserData(res, item);
                res.IsVisible = !item.IsInvisible;
            }
            return res;
        }

        private static GeoPoint GeoPoint(XYZ p) => new GeoPoint(p.X, p.Y, p.Z);
        private static GeoVector GeoVector(XYZ p) => new GeoVector(p.X, p.Y, p.Z);

        internal static Plane Plane(XYZ center, XYZ normal)
        {
            // AutoCAD Arbitrary Axis Algorithm — must use this for correct plane orientation
            GeoVector n = GeoVector(normal);
            GeoVector ax = (Math.Abs(normal.X) < 1.0 / 64 && Math.Abs(normal.Y) < 1.0 / 64)
                ? CADability.GeoVector.YAxis ^ n
                : CADability.GeoVector.ZAxis ^ n;
            GeoVector ay = n ^ ax;
            return new Plane(GeoPoint(center), ax, ay);
        }

        private Color AcadColorToDrawing(ACadSharp.Color color)
        {
            if (color.IsByLayer || color.IsByBlock)
                return Color.White; // will be resolved from layer
            if (color.IsTrueColor)
                return Color.FromArgb(color.R, color.G, color.B);
            // ACI index color — approximate via the approx RGB
            try { return Color.FromArgb(color.R, color.G, color.B); }
            catch { return Color.White; }
        }

        private ColorDef FindOrCreateColor(ACadSharp.Color color, ACadSharp.Tables.Layer layer)
        {
            if (color.IsByLayer && layer != null && layerColorTable.TryGetValue(layer.Name, out ColorDef layerColor))
                return layerColor;
            Color rgb = AcadColorToDrawing(color);
            if (rgb.ToArgb() == Color.White.ToArgb()) rgb = Color.Black;
            return project.ColorList.CreateOrFind(rgb.ToString(), rgb);
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

        private HatchStyleLines FindOrCreateHatchStyleLines(Entity entity, double lineAngle, double lineDistance, double[] dashes)
        {
            Color layerColor = Color.White;
            if (entity.Layer != null) layerColor = AcadColorToDrawing(entity.Layer.Color);
            if (layerColor.ToArgb() == Color.White.ToArgb()) layerColor = Color.Black;

            for (int i = 0; i < project.HatchStyleList.Count; i++)
            {
                if (project.HatchStyleList[i] is HatchStyleLines hsl)
                {
                    if (hsl.ColorDef.Color.ToArgb() == layerColor.ToArgb() &&
                        hsl.LineAngle == lineAngle && hsl.LineDistance == lineDistance) return hsl;
                }
            }
            HatchStyleLines nhsl = new HatchStyleLines();
            string name = NewName(entity.Layer?.Name ?? "Default", project.HatchStyleList);
            nhsl.Name = name;
            nhsl.LineAngle = lineAngle;
            nhsl.LineDistance = lineDistance;
            nhsl.ColorDef = project.ColorList.CreateOrFind(layerColor.ToString(), layerColor);

            LineWeightType lw = entity.LineWeight;
            if (lw == LineWeightType.ByLayer && entity.Layer != null) lw = entity.Layer.LineWeight;
            if ((int)lw < 0) lw = LineWeightType.W0;
            nhsl.LineWidth = project.LineWidthList.CreateOrFind("DXF_" + lw.ToString(), ((int)lw) / 100.0);
            nhsl.LinePattern = FindOrcreateLinePattern(dashes);
            project.HatchStyleList.Add(nhsl);
            return nhsl;
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
                else name += "_1";
            }
            return name;
        }

        private LinePattern FindOrcreateLinePattern(double[] dashes, string name = null)
        {
            if (dashes.Length == 0)
            {
                for (int i = 0; i < project.LinePatternList.Count; i++)
                {
                    if (project.LinePatternList[i].Pattern == null || project.LinePatternList[i].Pattern.Length == 0)
                        return project.LinePatternList[i];
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
                else pattern.Insert(0, 0.0);
                if ((pattern.Count & 0x01) != 0) pattern.Add(0.0);
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

        private void SetAttributes(IGeoObject go, Entity entity)
        {
            if (go is IColorDef cd) cd.ColorDef = FindOrCreateColor(entity.Color, entity.Layer);
            if (entity.Layer != null && layerTable.TryGetValue(entity.Layer.Name, out Attribute.Layer layer))
                go.Layer = layer;
            if (go is ILinePattern lp && entity.LineType != null)
                lp.LinePattern = project.LinePatternList.Find(entity.LineType.Name);
            if (go is ILineWidth ld)
            {
                LineWeightType lw = entity.LineWeight;
                if (lw == LineWeightType.ByLayer && entity.Layer != null) lw = entity.Layer.LineWeight;
                if ((int)lw < 0) lw = LineWeightType.W0;
                ld.LineWidth = project.LineWidthList.CreateOrFind("DXF_" + lw.ToString(), ((int)lw) / 100.0);
            }
        }

        private void SetUserData(IGeoObject go, Entity entity)
        {
            foreach (var kvp in entity.ExtendedData)
            {
                ExtendedEntityData xdata = new ExtendedEntityData();
                xdata.ApplicationName = kvp.Key.Name;
                string entryName = kvp.Key.Name;
                foreach (var record in kvp.Value.Records)
                {
                    XDataCode code = (XDataCode)(int)record.Code;
                    object value = record.RawValue;
                    xdata.Data.Add(new KeyValuePair<XDataCode, object>(code, value));
                }
                go.UserData.Add(entryName, xdata);
            }
            go.UserData["DxfImport.Handle"] = new UserInterface.StringProperty(entity.Handle.ToString("X"), "DxfImport.Handle");
        }

        private GeoObject.Block FindBlock(BlockRecord blockRec)
        {
            if (blockRec == null) return null;
            string key = blockRec.Handle.ToString("X");
            if (!blockTable.TryGetValue(key, out GeoObject.Block found))
            {
                found = GeoObject.Block.Construct();
                found.Name = blockRec.Name;
                found.RefPoint = GeoPoint(blockRec.BlockEntity?.BasePoint ?? XYZ.Zero);
                blockTable[key] = found; // register before filling (prevents infinite recursion)
                foreach (Entity ent in blockRec.Entities)
                {
                    IGeoObject go = GeoObjectFromEntity(ent);
                    if (go != null) found.Add(go);
                }
            }
            return found;
        }

        private IGeoObject CreateLine(ACadSharp.Entities.Line line)
        {
            GeoObject.Line l = GeoObject.Line.Construct();
            l.StartPoint = GeoPoint(line.StartPoint);
            l.EndPoint = GeoPoint(line.EndPoint);
            double th = line.Thickness;
            GeoVector no = GeoVector(line.Normal);
            if (th != 0.0 && !no.IsNullVector())
            {
                if (l.Length < Precision.eps)
                {
                    l.EndPoint += th * no;
                    return l;
                }
                return Make3D.Extrude(l, th * no, null);
            }
            return l;
        }

        private IGeoObject CreateRay(ACadSharp.Entities.Ray ray)
        {
            GeoObject.Line l = GeoObject.Line.Construct();
            l.StartPoint = GeoPoint(ray.StartPoint);
            l.EndPoint = l.StartPoint + GeoVector(ray.Direction);
            return l;
        }

        private IGeoObject CreateXLine(ACadSharp.Entities.XLine xline)
        {
            GeoObject.Line l = GeoObject.Line.Construct();
            l.StartPoint = GeoPoint(xline.FirstPoint);
            l.EndPoint = l.StartPoint + GeoVector(xline.Direction);
            return l;
        }

        private IGeoObject CreateArc(ACadSharp.Entities.Arc arc)
        {
            GeoObject.Ellipse e = GeoObject.Ellipse.Construct();
            GeoVector nor = GeoVector(arc.Normal);
            Plane plane = Plane(arc.Center, arc.Normal);
            double start = arc.StartAngle;
            double end = arc.EndAngle;
            double sweep = end - start;
            if (sweep < 0.0) sweep += Math.PI * 2.0;
            if (start == end) sweep = 0.0;
            if (start == Math.PI * 2.0 && end == 0.0) sweep = 0.0;
            e.SetArcPlaneCenterRadiusAngles(plane, GeoPoint(arc.Center), arc.Radius, start, sweep);
            if (e.IsCircle && sweep == 0.0 && Precision.IsEqual(e.StartPoint, e.EndPoint))
            {
                GeoObject.Ellipse circle = GeoObject.Ellipse.Construct();
                circle.SetCirclePlaneCenterRadius(plane, GeoPoint(arc.Center), arc.Radius);
                e = circle;
            }
            double th = arc.Thickness;
            if (th != 0.0 && !nor.IsNullVector())
                return Make3D.Extrude(e, th * nor, null);
            return e;
        }

        private IGeoObject CreateCircle(ACadSharp.Entities.Circle circle)
        {
            GeoObject.Ellipse e = GeoObject.Ellipse.Construct();
            Plane plane = Plane(circle.Center, circle.Normal);
            e.SetCirclePlaneCenterRadius(plane, GeoPoint(circle.Center), circle.Radius);
            double th = circle.Thickness;
            GeoVector no = GeoVector(circle.Normal);
            if (th != 0.0 && !no.IsNullVector())
                return Make3D.Extrude(e, th * no, null);
            return e;
        }

        private IGeoObject CreateEllipse(ACadSharp.Entities.Ellipse ellipse)
        {
            GeoObject.Ellipse e = GeoObject.Ellipse.Construct();
            GeoVector majorAxisVec = GeoVector(ellipse.MajorAxisEndPoint);
            GeoVector minorAxisVec = GeoVector(ellipse.MinorAxisEndpoint);
            e.SetEllipseCenterAxis(GeoPoint(ellipse.Center), majorAxisVec, minorAxisVec);
            e.StartParameter = ellipse.StartParameter;
            e.SweepParameter = ellipse.EndParameter - ellipse.StartParameter;
            if (e.SweepParameter == 0.0) e.SweepParameter = Math.PI * 2.0;
            if (e.SweepParameter < 0.0) e.SweepParameter += Math.PI * 2.0;
            return e;
        }

        private IGeoObject CreateSpline(ACadSharp.Entities.Spline spline)
        {
            int degree = spline.Degree;
            if (spline.ControlPoints.Count == 0 && spline.FitPoints.Count > 0)
            {
                BSpline bsp = BSpline.Construct();
                GeoPoint[] fp = new GeoPoint[spline.FitPoints.Count];
                for (int i = 0; i < fp.Length; i++) fp[i] = GeoPoint(spline.FitPoints[i]);
                bsp.ThroughPoints(fp, spline.Degree, spline.IsClosed);
                return bsp;
            }
            else
            {
                bool forcePolyline2D = false;
                GeoPoint[] poles = new GeoPoint[spline.ControlPoints.Count];
                double[] weights = new double[spline.ControlPoints.Count];
                for (int i = 0; i < poles.Length; i++)
                {
                    poles[i] = GeoPoint(spline.ControlPoints[i]);
                    weights[i] = (spline.Weights != null && i < spline.Weights.Count) ? spline.Weights[i] : 1.0;
                    if (i > 0 && (poles[i] | poles[i - 1]) < Precision.eps) forcePolyline2D = true;
                }
                double[] kn = new double[spline.Knots.Count];
                for (int i = 0; i < kn.Length; i++) kn[i] = spline.Knots[i];
                if (poles.Length == 2 && degree > 1)
                {
                    GeoObject.Line l = GeoObject.Line.Construct();
                    l.StartPoint = poles[0];
                    l.EndPoint = poles[1];
                    return l;
                }
                BSpline bsp = BSpline.Construct();
                if (bsp.SetData(degree, poles, weights, kn, null, spline.IsPeriodic))
                {
                    List<int> splitKnots = new List<int>();
                    for (int i = degree + 1; i < kn.Length - degree - 1; i++)
                    {
                        if (kn[i] == kn[i - 1])
                        {
                            bool sameKnot = true;
                            for (int j = 0; j < degree; j++)
                                if (kn[i - 1] != kn[i + j]) sameKnot = false;
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
                    if (forcePolyline2D)
                    {
                        ICurve curve = (ICurve)bsp;
                        double maxError = Settings.GlobalSettings.GetDoubleValue("Approximate.Precision", 0.01);
                        ICurve approxCurve = curve.Approximate(true, maxError);
                        int usedCurves;
                        if (approxCurve is GeoObject.Line || (approxCurve.SubCurves != null && approxCurve.SubCurves.Length == 1 && approxCurve.SubCurves[0] is GeoObject.Line))
                            usedCurves = 2;
                        else
                            usedCurves = approxCurve.SubCurves?.Length ?? 2;
                        return CreateSplineAsPolyline(bsp, usedCurves);
                    }
                    return bsp;
                }
            }
            return null;
        }

        private IGeoObject CreateSplineAsPolyline(BSpline bsp, int segments)
        {
            // Approximate the spline as a polyline (fallback for degenerate splines)
            List<GeoObject.Line> lines = new List<GeoObject.Line>();
            for (int i = 0; i < segments; i++)
            {
                double t0 = (double)i / segments;
                double t1 = (double)(i + 1) / segments;
                GeoPoint p0 = ((ICurve)bsp).PointAt(t0);
                GeoPoint p1 = ((ICurve)bsp).PointAt(t1);
                GeoObject.Line l = GeoObject.Line.Construct();
                l.StartPoint = p0;
                l.EndPoint = p1;
                lines.Add(l);
            }
            GeoObject.Path path = GeoObject.Path.Construct();
            path.Set(new GeoObjectList(lines.Cast<IGeoObject>().ToList()), false, 1e-6);
            return path.CurveCount > 0 ? (IGeoObject)path : null;
        }

        private IGeoObject CreateFace(ACadSharp.Entities.Face3D face)
        {
            List<GeoPoint> points = new List<GeoPoint>();
            GeoPoint p = GeoPoint(face.FirstCorner);
            points.Add(p);
            p = GeoPoint(face.SecondCorner);
            if (points[points.Count - 1] != p) points.Add(p);
            p = GeoPoint(face.ThirdCorner);
            if (points[points.Count - 1] != p) points.Add(p);
            p = GeoPoint(face.FourthCorner);
            if (points[points.Count - 1] != p) points.Add(p);
            if (points.Count == 3)
            {
                Plane pln = new Plane(points[0], points[1], points[2]);
                PlaneSurface surf = new PlaneSurface(pln);
                Border bdr = new Border(new GeoPoint2D[] { new GeoPoint2D(0, 0), pln.Project(points[1]), pln.Project(points[2]) });
                Face fc = Face.MakeFace(surf, new SimpleShape(bdr));
                return fc;
            }
            else if (points.Count == 4)
            {
                Plane pln = CADability.Plane.FromPoints(points.ToArray(), out double maxDist, out bool isLinear);
                if (!isLinear)
                {
                    if (maxDist > Precision.eps)
                    {
                        GeoObject.Block blk = GeoObject.Block.Construct();
                        blk.Set(new GeoObjectList(Face.MakeFace(points[0], points[1], points[2]), Face.MakeFace(points[0], points[2], points[3])));
                        return blk;
                    }
                    else
                    {
                        PlaneSurface surf = new PlaneSurface(pln);
                        Border bdr = new Border(new GeoPoint2D[] { pln.Project(points[0]), pln.Project(points[1]), pln.Project(points[2]), pln.Project(points[3]) });
                        double[] sis = bdr.GetSelfIntersection(Precision.eps);
                        if (sis.Length > 0)
                        {
                            Border[] splitted = bdr.Split(new double[] { sis[0], sis[1] });
                            foreach (var s in splitted) if (s.IsClosed) bdr = s;
                        }
                        return Face.MakeFace(surf, new SimpleShape(bdr));
                    }
                }
            }
            return null;
        }

        private IGeoObject CreatePolyfaceMesh(ACadSharp.Entities.PolyfaceMesh polyfacemesh)
        {
            // Position vertices
            List<GeoPoint> vertices = new List<GeoPoint>();
            foreach (VertexFaceMesh v in polyfacemesh.Vertices)
            {
                vertices.Add(GeoPoint(v.Location));
            }

            List<Face> faces = new List<Face>();
            foreach (VertexFaceRecord faceRec in polyfacemesh.Faces)
            {
                // Indices are 1-based; negative means invisible edge (take abs)
                int i0 = Math.Abs(faceRec.Index1) - 1;
                int i1 = Math.Abs(faceRec.Index2) - 1;
                int i2 = Math.Abs(faceRec.Index3) - 1;
                int i3 = Math.Abs(faceRec.Index4) - 1;

                if (i0 < 0 || i0 >= vertices.Count || i1 < 0 || i1 >= vertices.Count || i2 < 0 || i2 >= vertices.Count) continue;
                bool isQuad = i3 != i2 && i3 >= 0 && i3 < vertices.Count;

                if (!isQuad)
                {
                    if (i0 != i1 && i1 != i2)
                    {
                        try
                        {
                            Plane pln = new Plane(vertices[i0], vertices[i1], vertices[i2]);
                            Border bdr = new Border(new GeoPoint2D[] { new GeoPoint2D(0, 0), pln.Project(vertices[i1]), pln.Project(vertices[i2]) });
                            faces.Add(Face.MakeFace(new PlaneSurface(pln), new SimpleShape(bdr)));
                        }
                        catch { }
                    }
                }
                else
                {
                    if (i0 != i1 && i1 != i2)
                    {
                        try
                        {
                            Plane pln = new Plane(vertices[i0], vertices[i1], vertices[i2]);
                            Border bdr = new Border(new GeoPoint2D[] { new GeoPoint2D(0, 0), pln.Project(vertices[i1]), pln.Project(vertices[i2]) });
                            faces.Add(Face.MakeFace(new PlaneSurface(pln), new SimpleShape(bdr)));
                        }
                        catch { }
                    }
                    if (i2 != i3 && i3 != i0)
                    {
                        try
                        {
                            Plane pln = new Plane(vertices[i2], vertices[i3], vertices[i0]);
                            Border bdr = new Border(new GeoPoint2D[] { new GeoPoint2D(0, 0), pln.Project(vertices[i3]), pln.Project(vertices[i0]) });
                            faces.Add(Face.MakeFace(new PlaneSurface(pln), new SimpleShape(bdr)));
                        }
                        catch { }
                    }
                }
            }
            return AssembleFaces(faces);
        }

        private IGeoObject AssembleFaces(List<Face> faces)
        {
            if (faces.Count > 1)
            {
                GeoObjectList sewed = Make3D.SewFacesAndShells(new GeoObjectList(faces.ToArray() as IGeoObject[]));
                if (sewed.Count == 1) return sewed[0];
                GeoObject.Block blk = GeoObject.Block.Construct();
                blk.Set(new GeoObjectList(faces as ICollection<IGeoObject>));
                return blk;
            }
            if (faces.Count == 1) return faces[0];
            return null;
        }

        private IGeoObject CreateHatch(ACadSharp.Entities.Hatch hatch)
        {
            CompoundShape cs = null;
            bool ok = true;
            List<ICurve2D> allCurves = new List<ICurve2D>();
            Plane pln = CADability.Plane.XYPlane;
            for (int i = 0; i < hatch.Paths.Count; i++)
            {
                var boundaryPath = hatch.Paths[i];
                List<ICurve> boundaryEntities = new List<ICurve>();

                if (boundaryPath.IsPolyline && boundaryPath.Edges.Count > 0 &&
                    boundaryPath.Edges[0] is ACadSharp.Entities.Hatch.BoundaryPath.Polyline pl)
                {
                    // Polyline boundary: convert bulge segments to arcs/lines
                    IGeoObject ent = ConvertPolylineBoundary(pl, hatch.Normal, pln);
                    if (ent is ICurve crv) boundaryEntities.Add(crv);
                }
                else
                {
                    foreach (var edge in boundaryPath.Edges)
                    {
                        Entity edgeEntity = edge.ToEntity();
                        IGeoObject ent = edgeEntity != null ? GeoObjectFromEntity(edgeEntity) : null;
                        if (ent is ICurve crv) boundaryEntities.Add(crv);
                    }
                }

                if (boundaryEntities.Count == 0) continue;

                if (i == 0)
                {
                    if (!Curves.GetCommonPlane(boundaryEntities, out pln)) return null;
                }
                ICurve2D[] bdr2D = new ICurve2D[boundaryEntities.Count];
                for (int j = 0; j < bdr2D.Length; j++) bdr2D[j] = boundaryEntities[j].GetProjectedCurve(pln);
                try
                {
                    Border border = Border.FromUnorientedList(bdr2D, true);
                    allCurves.AddRange(bdr2D);
                    if (border != null)
                    {
                        SimpleShape ss = new SimpleShape(border);
                        if (cs == null) cs = new CompoundShape(ss);
                        else
                        {
                            double a = cs.Area;
                            cs = cs - new CompoundShape(ss);
                            if (cs.Area >= a) ok = false;
                        }
                    }
                }
                catch (BorderException) { }
            }

            if (cs == null) return null;
            if (cs.Area == 0.0 || !ok)
            {
                cs = CompoundShape.CreateFromList(allCurves.ToArray(), Precision.eps);
                if (cs == null || cs.Area == 0.0) return null;
            }

            GeoObject.Hatch res = GeoObject.Hatch.Construct();
            res.CompoundShape = cs;
            res.Plane = pln;

            if (hatch.IsSolid)
            {
                Color layerColor = hatch.Layer != null ? AcadColorToDrawing(hatch.Layer.Color) : Color.Black;
                if (layerColor.ToArgb() == Color.White.ToArgb()) layerColor = Color.Black;
                res.HatchStyle = FindOrCreateSolidHatchStyle(layerColor);
                return res;
            }
            else
            {
                GeoObjectList list = new GeoObjectList();
                if (hatch.Pattern?.Lines != null)
                {
                    foreach (var lineDef in hatch.Pattern.Lines)
                    {
                        if (list.Count > 0) res = res.Clone() as GeoObject.Hatch;
                        double lineAngle = lineDef.Angle;
                        double offX = lineDef.Offset.X;
                        double offY = lineDef.Offset.Y;
                        double[] dashes = lineDef.DashLengths != null ? lineDef.DashLengths.ToArray() : new double[0];
                        HatchStyleLines hsl = FindOrCreateHatchStyleLines(hatch, lineAngle, Math.Sqrt(offX * offX + offY * offY), dashes);
                        res.HatchStyle = hsl;
                        list.Add(res);
                    }
                }
                if (list.Count == 0) { res.HatchStyle = FindOrCreateSolidHatchStyle(Color.Black); return res; }
                if (list.Count > 1)
                {
                    GeoObject.Block block = GeoObject.Block.Construct();
                    block.Set(new GeoObjectList(list));
                    return block;
                }
                return res;
            }
        }

        private IGeoObject ConvertPolylineBoundary(ACadSharp.Entities.Hatch.BoundaryPath.Polyline pl, XYZ normal, Plane plane)
        {
            // Polyline boundary vertices: X,Y are coords, Z is bulge per ACadSharp docs
            // But separate Bulges array is also available
            var verts = pl.Vertices;
            double[] bulgesArr = pl.Bulges?.ToArray();
            int n = verts.Count;
            if (n < 2) return null;

            List<ICurve> curves = new List<ICurve>();
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                if (!pl.IsClosed && next == 0) break;

                XYZ v0 = verts[i];
                XYZ v1 = verts[next];
                GeoPoint p0 = plane.ToGlobal(new GeoPoint2D(v0.X, v0.Y));
                GeoPoint p1 = plane.ToGlobal(new GeoPoint2D(v1.X, v1.Y));

                double bulge = (bulgesArr != null && i < bulgesArr.Length) ? bulgesArr[i] : v0.Z;

                if (Math.Abs(bulge) < 1e-10)
                {
                    GeoObject.Line l = GeoObject.Line.Construct();
                    l.StartPoint = p0;
                    l.EndPoint = p1;
                    curves.Add(l);
                }
                else
                {
                    ICurve arc = BulgeToArc(p0, p1, bulge, plane);
                    if (arc != null) curves.Add(arc);
                }
            }
            if (curves.Count == 0) return null;
            if (curves.Count == 1) return curves[0] as IGeoObject;
            GeoObject.Path path = GeoObject.Path.Construct();
            path.Set(new GeoObjectList(curves), false, 1e-6);
            return path.CurveCount > 0 ? (IGeoObject)path : null;
        }

        private ICurve BulgeToArc(GeoPoint startPt, GeoPoint endPt, double bulge, Plane plane)
        {
            // DXF bulge = tan(includedAngle/4); positive = CCW, negative = CW.
            double angle = 4.0 * Math.Atan(Math.Abs(bulge));
            double chordLen = (endPt - startPt).Length;
            if (chordLen < Precision.eps) return null;

            double radius = chordLen / (2.0 * Math.Sin(angle / 2.0));
            GeoPoint midChord = new GeoPoint(0.5 * (startPt.x + endPt.x), 0.5 * (startPt.y + endPt.y), 0.5 * (startPt.z + endPt.z));
            GeoVector perpDir = (plane.Normal ^ (endPt - startPt).Normalized).Normalized;
            // d = distance from chord midpoint to arc center
            double d = Math.Sqrt(Math.Max(0, radius * radius - (chordLen / 2.0) * (chordLen / 2.0)));
            // CCW (bulge > 0): center is to the left of the chord (Normal × chordDir)
            // CW  (bulge < 0): center is to the right
            GeoPoint center = bulge > 0
                ? midChord + d * perpDir
                : midChord - d * perpDir;

            GeoObject.Ellipse arc = GeoObject.Ellipse.Construct();
            // Use the OCS plane directly for both CCW and CW arcs.
            // Flipping the plane for CW arcs was wrong: it caused SetArcPlaneCenterStartEndPoint
            // to produce a 270° sweep instead of the correct 90° (the arc went the long way round).
            arc.SetArcPlaneCenterStartEndPoint(plane, plane.Project(center), plane.Project(startPt), plane.Project(endPt), plane, bulge > 0);
            return arc;
        }

        private IGeoObject CreateSolid(ACadSharp.Entities.Solid solid)
        {
            // ACadSharp Solid corners are XYZ; elevation is encoded in the Z of the corners
            double elevation = solid.FirstCorner.Z;
            XYZ origin = new XYZ(solid.Normal.X * elevation, solid.Normal.Y * elevation, solid.Normal.Z * elevation);
            Plane ocs = Plane(origin, solid.Normal);
            return BuildSolidHatch(ocs,
                new XY(solid.FirstCorner.X, solid.FirstCorner.Y),
                new XY(solid.SecondCorner.X, solid.SecondCorner.Y),
                new XY(solid.ThirdCorner.X, solid.ThirdCorner.Y),
                new XY(solid.FourthCorner.X, solid.FourthCorner.Y),
                AcadColorToDrawing(solid.Color));
        }


        private IGeoObject BuildSolidHatch(Plane ocs, XY c1, XY c2, XY c3, XY c4, Color color)
        {
            HatchStyleSolid hst = FindOrCreateSolidHatchStyle(color.ToArgb() == Color.White.ToArgb() ? Color.Black : color);
            List<GeoPoint> points = new List<GeoPoint>();
            points.Add(ocs.ToGlobal(new GeoPoint2D(c1.X, c1.Y)));
            points.Add(ocs.ToGlobal(new GeoPoint2D(c2.X, c2.Y)));
            points.Add(ocs.ToGlobal(new GeoPoint2D(c3.X, c3.Y)));
            points.Add(ocs.ToGlobal(new GeoPoint2D(c4.X, c4.Y)));
            for (int i = 3; i > 0; --i)
            {
                for (int j = 0; j < i; ++j)
                {
                    if (Precision.IsEqual(points[j], points[i])) { points.RemoveAt(i); break; }
                }
            }
            if (points.Count < 3) return null;
            Plane pln;
            try { pln = new Plane(points[0], points[1], points[2]); }
            catch (PlaneException) { return null; }
            GeoPoint2D[] vertex = new GeoPoint2D[points.Count + 1];
            for (int i = 0; i < points.Count; ++i) vertex[i] = pln.Project(points[i]);
            vertex[points.Count] = vertex[0];
            Border bdr = new Border(new Curve2D.Polyline2D(vertex));
            GeoObject.Hatch h = GeoObject.Hatch.Construct();
            h.CompoundShape = new CompoundShape(new SimpleShape(bdr));
            h.HatchStyle = hst;
            h.Plane = pln;
            return h;
        }

        private IGeoObject CreateInsert(ACadSharp.Entities.Insert insert)
        {
            GeoObject.Block block = FindBlock(insert.Block);
            if (block != null)
            {
                IGeoObject res = block.Clone();
                ModOp transform = ModOp.Translate(GeoVector(insert.InsertPoint)) *
                    ModOp.Rotate(CADability.GeoVector.ZAxis, new SweepAngle(insert.Rotation)) *
                    ModOp.Scale(insert.XScale, insert.YScale, insert.ZScale) *
                    ModOp.Translate(CADability.GeoPoint.Origin - block.RefPoint);
                res.Modify(transform);
                return res;
            }
            return null;
        }

        private IGeoObject CreateLwPolyline(ACadSharp.Entities.LwPolyline polyline)
        {
            // Hand-implement bulge→arc conversion (ACadSharp has no Explode())
            Plane plane = Plane(new XYZ(0, 0, polyline.Elevation), polyline.Normal);
            int n = polyline.Vertices.Count;
            if (n < 2) return null;

            List<IGeoObject> segments = new List<IGeoObject>();
            for (int i = 0; i < n; i++)
            {
                int next = polyline.IsClosed ? (i + 1) % n : i + 1;
                if (next >= n) break;

                var v0 = polyline.Vertices[i];
                var v1 = polyline.Vertices[next];
                GeoPoint p0 = plane.ToGlobal(new GeoPoint2D(v0.Location.X, v0.Location.Y));
                GeoPoint p1 = plane.ToGlobal(new GeoPoint2D(v1.Location.X, v1.Location.Y));

                if (Math.Abs(v0.Bulge) < 1e-10)
                {
                    GeoObject.Line l = GeoObject.Line.Construct();
                    l.StartPoint = p0;
                    l.EndPoint = p1;
                    segments.Add(l);
                }
                else
                {
                    ICurve arc = BulgeToArc(p0, p1, v0.Bulge, plane);
                    if (arc != null) segments.Add(arc as IGeoObject);
                }
            }
            if (segments.Count == 0) return null;
            if (segments.Count == 1) return segments[0];
            GeoObject.Path path = GeoObject.Path.Construct();
            path.Set(new GeoObjectList(segments), false, 1e-6);
            return path.CurveCount > 0 ? (IGeoObject)path : null;
        }

        private IGeoObject CreatePolyline2D(ACadSharp.Entities.Polyline2D polyline2D)
        {
            // Old-style 2D polyline with vertex entities
            List<IGeoObject> segments = new List<IGeoObject>();
            var verts = new List<Vertex2D>(polyline2D.Vertices);
            int n = verts.Count;
            for (int i = 0; i < n; i++)
            {
                int next = polyline2D.IsClosed ? (i + 1) % n : i + 1;
                if (next >= n) break;
                GeoPoint p0 = GeoPoint(verts[i].Location);
                GeoPoint p1 = GeoPoint(verts[next].Location);
                if (Math.Abs(verts[i].Bulge) < 1e-10)
                {
                    GeoObject.Line l = GeoObject.Line.Construct();
                    l.StartPoint = p0;
                    l.EndPoint = p1;
                    segments.Add(l);
                }
                else
                {
                    // Use the XY plane for old-style 2D polylines
                    Plane plane = Plane(new XYZ(0, 0, 0), new XYZ(0, 0, 1));
                    ICurve arc = BulgeToArc(p0, p1, verts[i].Bulge, plane);
                    if (arc != null) segments.Add(arc as IGeoObject);
                }
            }
            if (segments.Count == 0) return null;
            GeoObject.Path go = GeoObject.Path.Construct();
            go.Set(new GeoObjectList(segments), false, 1e-6);
            return go.CurveCount > 0 ? (IGeoObject)go : null;
        }

        private IGeoObject CreateMLine(ACadSharp.Entities.MLine mLine)
        {
            // Hand-implement (ACadSharp has no Explode())
            // Create lines for each pair of consecutive vertices
            List<IGeoObject> lines = new List<IGeoObject>();
            var verts = mLine.Vertices;
            for (int i = 0; i < verts.Count - 1; i++)
            {
                GeoObject.Line l = GeoObject.Line.Construct();
                l.StartPoint = GeoPoint(verts[i].Position);
                l.EndPoint = GeoPoint(verts[i + 1].Position);
                lines.Add(l);
            }
            if (lines.Count == 0) return null;
            GeoObjectList list = new GeoObjectList(lines);
            GeoObjectList res = new GeoObjectList();
            while (list.Count > 0)
            {
                GeoObject.Path go = GeoObject.Path.Construct();
                if (go.Set(list, true, 1e-6)) res.Add(go);
                else break;
            }
            if (res.Count > 1)
            {
                GeoObject.Block blk = GeoObject.Block.Construct();
                blk.Name = "MLINE " + mLine.Handle.ToString("X");
                blk.Set(res);
                return blk;
            }
            if (res.Count == 1) return res[0];
            return null;
        }

        private string processAcadString(string acstr)
        {
            var sb = new StringBuilder(acstr);
            sb.Replace("%%153", "Ø"); sb.Replace("%%127", "°"); sb.Replace("%%214", "Ö");
            sb.Replace("%%220", "Ü"); sb.Replace("%%228", "ä"); sb.Replace("%%246", "ö");
            sb.Replace("%%223", "ß"); sb.Replace("%%u", ""); sb.Replace("%%U", "");
            sb.Replace("%%D", "°"); sb.Replace("%%d", "°"); sb.Replace("%%P", "±");
            sb.Replace("%%p", "±"); sb.Replace("%%C", "Ø"); sb.Replace("%%c", "Ø");
            sb.Replace("%%%", "%");
            return sb.ToString();
        }

        private IGeoObject CreateText(ACadSharp.Entities.TextEntity txt)
        {
            GeoObject.Text text = GeoObject.Text.Construct();
            string txtstring = processAcadString(txt.Value ?? "");
            if (txtstring.Trim().Length == 0) return null;

            string filename = txt.Style?.Filename ?? "";
            string name = txt.Style?.Name ?? "";
            long trueType = (long)(txt.Style?.TrueType ?? 0);
            bool bold = (trueType & 2L) != 0;
            bool italic = (trueType & 1L) != 0;

            if (filename.EndsWith(".shx", StringComparison.OrdinalIgnoreCase)) filename = filename.Substring(0, filename.Length - 4);
            if (filename.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
            {
                if (name != null && name.Length > 1) filename = name;
                else filename = filename.Substring(0, filename.Length - 4);
            }
            text.Font = string.IsNullOrEmpty(filename) ? "Arial" : filename;
            text.Bold = bold;
            text.Italic = italic;
            text.TextString = txtstring;

            double h = txt.Height;
            Plane plane = Plane(txt.InsertPoint, txt.Normal);
            Angle a = new Angle(txt.Rotation);
            GeoVector2D dir2d = new GeoVector2D(a);
            GeoVector linedir = plane.ToGlobal(dir2d);
            GeoVector glyphdir = plane.ToGlobal(dir2d.ToLeft());
            text.Location = GeoPoint(txt.InsertPoint);
            text.LineDirection = linedir;
            text.GlyphDirection = glyphdir;
            text.TextSize = h;
            linedir.Length = h * txt.WidthFactor;
            if (!linedir.IsNullVector()) text.LineDirection = linedir;

            // Map horizontal alignment
            text.LineAlignment = GeoObject.Text.LineAlignMode.Left;
            text.Alignment = GeoObject.Text.AlignMode.Bottom;
            switch (txt.HorizontalAlignment)
            {
                case TextHorizontalAlignment.Left: text.LineAlignment = GeoObject.Text.LineAlignMode.Left; break;
                case TextHorizontalAlignment.Center: text.LineAlignment = GeoObject.Text.LineAlignMode.Center; break;
                case TextHorizontalAlignment.Right: text.LineAlignment = GeoObject.Text.LineAlignMode.Right; break;
            }
            switch (txt.VerticalAlignment)
            {
                case TextVerticalAlignmentType.Baseline: text.Alignment = GeoObject.Text.AlignMode.Baseline; break;
                case TextVerticalAlignmentType.Bottom: text.Alignment = GeoObject.Text.AlignMode.Bottom; break;
                case TextVerticalAlignmentType.Middle: text.Alignment = GeoObject.Text.AlignMode.Center; break;
                case TextVerticalAlignmentType.Top: text.Alignment = GeoObject.Text.AlignMode.Top; break;
            }
            if (text.TextSize < 1e-5) return null;
            return text;
        }

        private IGeoObject CreateDimension(ACadSharp.Entities.Dimension dimension)
        {
            if (dimension.Block != null)
            {
                GeoObject.Block block = FindBlock(dimension.Block);
                if (block != null) return block.Clone();
            }
            return null;
        }

        private string StripMTextFormatCodes(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            // Remove MTEXT formatting codes: \P (paragraph), \~, {\fFontName|...}, {\H...;}, etc.
            var sb = new StringBuilder();
            int i = 0;
            while (i < value.Length)
            {
                if (value[i] == '\\' && i + 1 < value.Length)
                {
                    char next = value[i + 1];
                    if (next == 'P' || next == 'p') { sb.Append('\n'); i += 2; }
                    else if (next == '~') { sb.Append(' '); i += 2; }
                    else if (next == 'n' || next == 'N') { sb.Append('\n'); i += 2; }
                    else { i += 2; } // skip other escape sequences
                }
                else if (value[i] == '{')
                {
                    // Find matching closing brace - skip format group
                    int depth = 1;
                    int j = i + 1;
                    while (j < value.Length && depth > 0)
                    {
                        if (value[j] == '{') depth++;
                        else if (value[j] == '}') depth--;
                        j++;
                    }
                    // If it starts with a format code, skip the whole group
                    if (i + 1 < value.Length && value[i + 1] == '\\')
                        i = j;
                    else if (i + 1 < value.Length && value[i + 1] == '}')
                        i = j; // empty group
                    else
                    {
                        // Recurse into the group content
                        sb.Append(StripMTextFormatCodes(value.Substring(i + 1, j - i - 2)));
                        i = j;
                    }
                }
                else
                    sb.Append(value[i++]);
            }
            return sb.ToString();
        }

        private IGeoObject CreateMText(ACadSharp.Entities.MText mText)
        {
            string plainText = StripMTextFormatCodes(mText.Value ?? "");
            var txt = new ACadSharp.Entities.TextEntity
            {
                Value = plainText,
                Height = mText.Height,
                WidthFactor = 1.0,
                Rotation = mText.Rotation,
                Style = mText.Style,
                InsertPoint = mText.InsertPoint,
                Normal = mText.Normal,
            };
            // Map MTEXT attachment point (group 71) to horizontal/vertical alignment.
            // The InsertPoint is the anchor for the specified attachment mode.
            switch (mText.AttachmentPoint)
            {
                case AttachmentPointType.TopCenter:
                case AttachmentPointType.MiddleCenter:
                case AttachmentPointType.BottomCenter:
                    txt.HorizontalAlignment = TextHorizontalAlignment.Center; break;
                case AttachmentPointType.TopRight:
                case AttachmentPointType.MiddleRight:
                case AttachmentPointType.BottomRight:
                    txt.HorizontalAlignment = TextHorizontalAlignment.Right; break;
                default:
                    txt.HorizontalAlignment = TextHorizontalAlignment.Left; break;
            }
            switch (mText.AttachmentPoint)
            {
                case AttachmentPointType.TopLeft:
                case AttachmentPointType.TopCenter:
                case AttachmentPointType.TopRight:
                    txt.VerticalAlignment = TextVerticalAlignmentType.Top; break;
                case AttachmentPointType.MiddleLeft:
                case AttachmentPointType.MiddleCenter:
                case AttachmentPointType.MiddleRight:
                    txt.VerticalAlignment = TextVerticalAlignmentType.Middle; break;
                default:
                    txt.VerticalAlignment = TextVerticalAlignmentType.Bottom; break;
            }
            return CreateText(txt);
        }

        private IGeoObject CreateTolerance(ACadSharp.Entities.Tolerance tolerance)
        {
            // DXF TOLERANCE (AcDbFcf) is a Feature Control Frame annotation.
            // The text uses GDT-font characters and %%v cell separators. Convert to a
            // readable plain-text string and import as a Text object at the insertion point.
            string raw = tolerance.Text ?? "";
            string plain = ExtractFcfText(raw);
            plain = plain.Trim('|', ' ');
            if (string.IsNullOrWhiteSpace(plain)) return null;

            double height = (tolerance.Style?.TextHeight ?? 0) > 0 ? tolerance.Style.TextHeight : 2.5;
            GeoVector dir = GeoVector(tolerance.Direction);
            double rotation = dir.IsNullVector() ? 0.0 : Math.Atan2(dir.y, dir.x);

            var txt = new ACadSharp.Entities.TextEntity
            {
                Value = plain,
                Height = height,
                InsertPoint = tolerance.InsertionPoint,
                Normal = tolerance.Normal,
                Rotation = rotation,
                Style = tolerance.Style?.Style,   // DimensionStyle.Style is the TextStyle
            };
            return CreateText(txt);
        }

        private static string ExtractFcfText(string text)
        {
            // Convert FCF (Feature Control Frame) encoding to plain text:
            //   {\Fgdt;X}  → X  (GDT font character — keep the letter as-is)
            //   %%v        → |  (cell separator)
            //   %%d / %%D  → °
            //   %%p / %%P  → ±
            //   %%c / %%C  → ⌀
            //   ^J         → \n (DXF FCF line break)
            //   \P / \p    → \n
            if (string.IsNullOrEmpty(text)) return text;
            var sb = new StringBuilder(text.Length);
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (c == '{')
                {
                    int depth = 1, j = i + 1;
                    while (j < text.Length && depth > 0)
                    {
                        if (text[j] == '{') depth++;
                        else if (text[j] == '}') depth--;
                        j++;
                    }
                    string inner = text.Substring(i + 1, j - i - 2);
                    // Strip leading format codes of the form \X...;
                    int k = 0;
                    while (k < inner.Length && inner[k] == '\\')
                    {
                        int semi = inner.IndexOf(';', k + 1);
                        if (semi < 0) { k = inner.Length; break; }
                        k = semi + 1;
                    }
                    sb.Append(ExtractFcfText(inner.Substring(k)));
                    i = j;
                }
                else if (c == '%' && i + 2 < text.Length && text[i + 1] == '%')
                {
                    char code = char.ToLower(text[i + 2]);
                    if (code == 'v') { sb.Append('|'); i += 3; }
                    else if (code == 'd') { sb.Append('°'); i += 3; }
                    else if (code == 'p') { sb.Append('±'); i += 3; }
                    else if (code == 'c') { sb.Append('⌀'); i += 3; }
                    else { sb.Append(c); i++; }
                }
                else if (c == '^' && i + 1 < text.Length && text[i + 1] == 'J')
                {
                    sb.Append('\n'); i += 2;
                }
                else if (c == '\\' && i + 1 < text.Length)
                {
                    char next = char.ToLower(text[i + 1]);
                    if (next == 'p') { sb.Append('\n'); i += 2; }
                    else { i += 2; }
                }
                else
                {
                    sb.Append(c); i++;
                }
            }
            return sb.ToString();
        }

        private IGeoObject CreateLeader(ACadSharp.Entities.Leader leader)
        {
            Plane ocs = Plane(XYZ.Zero, leader.Normal);
            GeoObject.Block blk = GeoObject.Block.Construct();
            blk.Name = "Leader:" + leader.Handle.ToString("X");
            if (leader.AssociatedAnnotation != null)
            {
                IGeoObject annotation = GeoObjectFromEntity(leader.AssociatedAnnotation);
                if (annotation != null) blk.Add(annotation);
            }
            GeoPoint[] vtx = new GeoPoint[leader.Vertices.Count];
            for (int i = 0; i < vtx.Length; i++)
                vtx[i] = GeoPoint(leader.Vertices[i]);
            GeoObject.Polyline pln = GeoObject.Polyline.Construct();
            pln.SetPoints(vtx, false);
            blk.Add(pln);
            return blk;
        }

        private IGeoObject CreatePolyline3D(ACadSharp.Entities.Polyline3D polyline3D)
        {
            GeoObject.Polyline res = GeoObject.Polyline.Construct();
            foreach (Vertex3D v in polyline3D.Vertices)
            {
                res.AddPoint(GeoPoint(v.Location));
            }
            res.IsClosed = polyline3D.IsClosed;
            if (res.GetExtent(0.0).Size < 1e-6) return null;
            return res;
        }

        private IGeoObject CreatePoint(ACadSharp.Entities.Point point)
        {
            CADability.GeoObject.Point p = CADability.GeoObject.Point.Construct();
            p.Location = GeoPoint(point.Location);
            p.Symbol = PointSymbol.Cross;
            return p;
        }

        private IGeoObject CreateMesh(ACadSharp.Entities.Mesh mesh)
        {
            GeoPoint[] vertices = new GeoPoint[mesh.Vertices.Count];
            for (int i = 0; i < vertices.Length; i++) vertices[i] = GeoPoint(mesh.Vertices[i]);
            List<Face> faces = new List<Face>();
            foreach (var faceIndices in mesh.Faces)
            {
                int[] idx = faceIndices;
                if (idx.Length < 3) continue;
                bool isQuad = idx.Length >= 4 && idx[3] != idx[2];
                if (idx[0] != idx[1] && idx[1] != idx[2])
                {
                    try
                    {
                        Plane pln = new Plane(vertices[idx[0]], vertices[idx[1]], vertices[idx[2]]);
                        Border bdr = new Border(new GeoPoint2D[] { new GeoPoint2D(0, 0), pln.Project(vertices[idx[1]]), pln.Project(vertices[idx[2]]) });
                        faces.Add(Face.MakeFace(new PlaneSurface(pln), new SimpleShape(bdr)));
                    }
                    catch { }
                }
                if (isQuad && idx[2] != idx[3] && idx[3] != idx[0])
                {
                    try
                    {
                        Plane pln = new Plane(vertices[idx[2]], vertices[idx[3]], vertices[idx[0]]);
                        Border bdr = new Border(new GeoPoint2D[] { new GeoPoint2D(0, 0), pln.Project(vertices[idx[3]]), pln.Project(vertices[idx[0]]) });
                        faces.Add(Face.MakeFace(new PlaneSurface(pln), new SimpleShape(bdr)));
                    }
                    catch { }
                }
            }
            return AssembleFaces(faces);
        }

        private IGeoObject CreateWipeout(ACadSharp.Entities.Wipeout wipeout)
        {
            // Represent as a face in the XY plane
            try
            {
                var boundary = wipeout.ClipBoundaryVertices;
                if (boundary == null || boundary.Count < 3) return null;
                List<GeoPoint> pts = new List<GeoPoint>();
                foreach (var v in boundary) pts.Add(new GeoPoint(v.X, v.Y, 0));
                Plane pln = CADability.Plane.FromPoints(pts.ToArray(), out _, out bool isLinear);
                if (isLinear) return null;
                GeoPoint2D[] pts2D = new GeoPoint2D[pts.Count + 1];
                for (int i = 0; i < pts.Count; i++) pts2D[i] = pln.Project(pts[i]);
                pts2D[pts.Count] = pts2D[0];
                Border bdr = new Border(new Curve2D.Polyline2D(pts2D));
                GeoObject.Hatch h = GeoObject.Hatch.Construct();
                h.CompoundShape = new CompoundShape(new SimpleShape(bdr));
                h.HatchStyle = FindOrCreateSolidHatchStyle(Color.White);
                h.Plane = pln;
                h.UserData["Wipeout"] = new UserInterface.StringProperty("true", "Wipeout");
                return h;
            }
            catch { return null; }
        }
    }
}
