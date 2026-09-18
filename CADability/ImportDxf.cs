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
        /// <summary>
        /// The entity a block's contents inherit from while that block is being expanded. DXF
        /// resolves ByBlock color, linetype and lineweight against the entity that placed the
        /// block, and contents drawn on layer "0" take that entity's layer. Only dimension
        /// blocks set this so far; <see cref="FindBlock"/> bypasses its cache while it is set,
        /// because the same block record placed by two entities then yields two results.
        /// A block nested inside keeps the outermost owner, which is what DXF means as long as
        /// the nested reference is itself ByBlock - the normal case for arrow head blocks.
        /// </summary>
        private Entity byBlockOwner;
        /// <summary>
        /// Block records currently being expanded on behalf of <see cref="byBlockOwner"/>, to
        /// stop a circular block reference from recursing forever on the uncached path.
        /// </summary>
        private readonly HashSet<string> expandingBlocks = new HashSet<string>();
        /// <summary>
        /// Read from the setting "DxfImport.DimensionsAsDimension". Off by default: a DIMENSION
        /// then keeps the picture AutoCAD drew. On, it is rebuilt as CADability's own
        /// <see cref="GeoObject.Dimension"/>, which can be measured and edited but is redrawn by
        /// CADability's style engine and will not match the original line for line.
        /// </summary>
        private bool dimensionsAsDimension;

        public Import(string fileName)
        {
            byte[] raw;
            using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                raw = new byte[fs.Length];
                fs.Read(raw, 0, raw.Length);
            }
            using (var stream = new MemoryStream(raw))
            {
                try
                {
                    doc = new DxfReader(stream).Read();
                }
                catch (Exception)
                {
                    // The OBJECTS section is the last DXF section and contains non-geometric
                    // data (dictionaries, layouts, xrecords). Geometry lives in HEADERS,
                    // TABLES, BLOCKS, and ENTITIES — all read before OBJECTS.
                    // When OBJECTS is malformed (DxfException, NullReferenceException, etc.)
                    // we fall back to a read that stops before it, so geometry still imports.
                    doc = ReadWithoutObjectsSection(raw);
                    if (doc == null) throw; // OBJECTS was not the problem — re-throw
                }
            }
        }

        // Retries reading by presenting the file content only up to (but not including)
        // the OBJECTS section.  Returns null if the OBJECTS section boundary cannot be
        // located (meaning something earlier in the file caused the original error).
        private static CadDocument ReadWithoutObjectsSection(byte[] raw)
        {
            // The section header is a group-0 entity followed by SECTION / group-2 / OBJECTS.
            // Match both CRLF and LF line endings.
            byte[][] markers = new byte[][]
            {
                Encoding.ASCII.GetBytes("  0\r\nSECTION\r\n  2\r\nOBJECTS\r\n"),
                Encoding.ASCII.GetBytes("  0\nSECTION\n  2\nOBJECTS\n"),
            };

            int cutAt = -1;
            foreach (byte[] marker in markers)
            {
                int pos = IndexOf(raw, marker);
                if (pos >= 0) { cutAt = pos; break; }
            }

            if (cutAt < 0) return null; // OBJECTS boundary not found — original error is elsewhere

            // Build a valid minimal DXF ending: EOF group.
            byte[] eof = Encoding.ASCII.GetBytes("  0\r\nEOF\r\n");
            byte[] truncated = new byte[cutAt + eof.Length];
            Array.Copy(raw, truncated, cutAt);
            Array.Copy(eof, 0, truncated, cutAt, eof.Length);

            using (MemoryStream ms = new MemoryStream(truncated))
                return new DxfReader(ms).Read();
        }

        private static int IndexOf(byte[] haystack, byte[] needle)
        {
            int limit = haystack.Length - needle.Length;
            for (int i = 0; i <= limit; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j]) { match = false; break; }
                }
                if (match) return i;
            }
            return -1;
        }

        internal Import(CadDocument document)
        {
            doc = document;
        }

        // The drawing versions ACadSharp can read. DXF reaches back to R11/R12, DWG to R13;
        // anything older has to be converted by the writing application before CADability gets
        // to see it - DwgReader throws CadNotSupportedException for R12 and earlier, there is no
        // code for those at all.
        // The compatibility table in https://github.com/DomCR/ACadSharp#compatible-dwgdxf-versions
        // marks R13 (AC1012) as unreadable, but that is the writer's limit showing through:
        // DwgWriter does throw for AC1012, DwgReader does not. Its R13 path is implemented, not
        // aliased to R14 - the header and object readers carry ~40 R13/R14 branches and two spots
        // that separate R13 from R14 (the R14-only byte in dictionaries, the R14+ leader offset).
        // It is untested though: ACadSharp ships no R13 sample, so nothing exercises it. Letting
        // it through costs nothing when it does not work, because the caller reports the reader's
        // exception either way, and gains the import when it does.
        private static readonly ACadVersion[] readableDxfVersions =
        {
            ACadVersion.AC1009, // R11, R12
            ACadVersion.AC1012, // R13
            ACadVersion.AC1014, // R14
            ACadVersion.AC1015, // 2000, 2000i, 2002
            ACadVersion.AC1018, // 2004, 2005, 2006
            ACadVersion.AC1021, // 2007, 2008, 2009
            ACadVersion.AC1024, // 2010, 2011, 2012
            ACadVersion.AC1027, // 2013 - 2017
            ACadVersion.AC1032, // 2018 - 2020
        };

        private static readonly ACadVersion[] readableDwgVersions =
        {
            ACadVersion.AC1012, // R13 - implemented but not covered by any ACadSharp test
            ACadVersion.AC1014, // R14
            ACadVersion.AC1015, // 2000, 2000i, 2002
            ACadVersion.AC1018, // 2004, 2005, 2006
            ACadVersion.AC1021, // 2007, 2008, 2009
            ACadVersion.AC1024, // 2010, 2011, 2012
            ACadVersion.AC1027, // 2013 - 2017
            ACadVersion.AC1032, // 2018 - 2020
        };

        private static readonly byte[] acadVerMarker = Encoding.ASCII.GetBytes("$ACADVER");

        /// <summary>
        /// Tells whether <see cref="Project.ReadFromFile(string, string)"/> can read this DXF or
        /// DWG file, judged by the drawing version stored in the file itself. Which of the two
        /// formats it is comes from the content, not from the extension. Only the head of the
        /// file is looked at, so this is cheap enough to call before every import.
        /// </summary>
        /// <param name="fileName">Path of the DXF or DWG file to test</param>
        /// <returns>true if the version is one that can be read</returns>
        /// <exception cref="IOException">The file cannot be opened at all, which is not a
        /// version problem and therefore not reported as an unsupported version.</exception>
        public static bool CanImportVersion(string fileName)
        {
            return CanImportVersion(fileName, out _);
        }

        /// <summary>
        /// Same as <see cref="CanImportVersion(string)"/>, but also reports the version found in
        /// the file so that a caller can name it in an error message.
        /// </summary>
        /// <param name="fileName">Path of the DXF or DWG file to test</param>
        /// <param name="versionName">Receives the drawing version found in the file, e.g.
        /// "AC1015" for AutoCAD 2000, or an empty string when the file carries no version</param>
        /// <returns>true if the version is one that can be read</returns>
        /// <exception cref="IOException">The file cannot be opened at all, which is not a
        /// version problem and therefore not reported as an unsupported version.</exception>
        public static bool CanImportVersion(string fileName, out string versionName)
        {
            ACadVersion version = GetDrawingVersion(fileName, out bool isDwg);
            versionName = version == ACadVersion.Unknown ? "" : version.ToString().Replace('_', '.');

            if (isDwg) return Array.IndexOf(readableDwgVersions, version) >= 0;

            // A DXF without a $ACADVER header still gets its chance: ACadSharp falls back to a
            // generic reader for those instead of refusing them.
            if (version == ACadVersion.Unknown) return true;
            return Array.IndexOf(readableDxfVersions, version) >= 0;
        }

        /// <summary>
        /// Reads the drawing version out of the head of a DXF or DWG file. A DWG starts with its
        /// six character version tag, a DXF carries the tag as the value of the $ACADVER header
        /// variable - as plain ASCII in the text and in the binary encoding alike.
        /// </summary>
        private static ACadVersion GetDrawingVersion(string fileName, out bool isDwg)
        {
            isDwg = false;
            byte[] head = ReadFileHead(fileName, 0x20000);
            if (head.Length < 6) return ACadVersion.Unknown;

            // "AC" followed by a digit: a DWG version tag. No DXF starts like that, a text DXF
            // begins with a group code and a binary one with "AutoCAD Binary DXF".
            if (head[0] == (byte)'A' && head[1] == (byte)'C' && head[2] >= (byte)'0' && head[2] <= (byte)'9')
            {
                isDwg = true;
                return VersionFromName(Encoding.ASCII.GetString(head, 0, 6));
            }

            int pos = IndexOf(head, acadVerMarker);
            if (pos < 0) return ACadVersion.Unknown;
            return VersionAfterMarker(head, pos + acadVerMarker.Length);
        }

        // Scans forward from the $ACADVER marker for the version tag. What sits in between is the
        // group code - "  1" in a text DXF, two non printable bytes in a binary one - and is
        // skipped over because a version tag is at least four characters long.
        private static ACadVersion VersionAfterMarker(byte[] raw, int start)
        {
            int limit = Math.Min(raw.Length, start + 64);
            int i = start;
            while (i < limit)
            {
                if (!IsVersionChar(raw[i])) { i++; continue; }

                int tokenStart = i;
                while (i < limit && IsVersionChar(raw[i])) i++;
                if (i - tokenStart >= 4)
                {
                    ACadVersion version = VersionFromName(Encoding.ASCII.GetString(raw, tokenStart, i - tokenStart));
                    if (version != ACadVersion.Unknown) return version;
                }
            }
            return ACadVersion.Unknown;
        }

        private static bool IsVersionChar(byte b)
        {
            return (b >= (byte)'0' && b <= (byte)'9')
                || (b >= (byte)'A' && b <= (byte)'Z')
                || (b >= (byte)'a' && b <= (byte)'z')
                || b == (byte)'.' || b == (byte)'_';
        }

        // The names in the files ("AC1015", "AC1.50") differ from the enum members only in the
        // separator, which cannot be a dot in C#.
        private static ACadVersion VersionFromName(string name)
        {
            if (Enum.TryParse(name.Replace('.', '_').ToUpperInvariant(), out ACadVersion version)
                && Enum.IsDefined(typeof(ACadVersion), version))
                return version;
            return ACadVersion.Unknown;
        }

        private static byte[] ReadFileHead(string fileName, int maxBytes)
        {
            using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                int count = (int)Math.Min(maxBytes, fs.Length);
                byte[] buffer = new byte[count];
                int read = 0;
                while (read < count)
                {
                    int n = fs.Read(buffer, read, count - read);
                    if (n <= 0) break;
                    read += n;
                }
                if (read < count) Array.Resize(ref buffer, read);
                return buffer;
            }
        }

        // Converts one entity and adds the result to the model. A failing entity is traced
        // and skipped so it cannot abort the rest of the import.
        private void ConvertAndAdd(Entity item, Model model, ref int converted, ref int empty, ref int failed)
        {
            try
            {
                IGeoObject geoObject = GeoObjectFromEntity(item);
                if (geoObject != null)
                {
                    model.Add(geoObject);
                    converted++;
                }
                else
                {
                    System.Diagnostics.Trace.WriteLine("dxf: no geometry created for " + item.GetType().Name + " (handle " + item.Handle.ToString("X") + ")");
                    empty++;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("dxf: failed to import " + item.GetType().Name + " (handle " + item.Handle.ToString("X") + "): " + ex.Message);
                failed++;
            }
        }

        private void FillModelSpace(Model model)
        {
            int converted = 0, empty = 0, failed = 0;
            if (doc.BlockRecords.TryGetValue("*Model_Space", out BlockRecord modelSpace)
                && modelSpace.Entities.Count > 0)
            {
                foreach (Entity item in modelSpace.Entities)
                {
                    ConvertAndAdd(item, model, ref converted, ref empty, ref failed);
                }
            }
            else
            {
                // Fallback for non-standard R12 files: *Model_Space absent or empty.
                // doc.Entities is ACadSharp's flat view of all drawing entities; take only
                // those whose owner is a space block (or unowned, which is model-space in R12).
                foreach (var obj in doc.Entities)
                {
                    if (!(obj is Entity item)) continue;
                    string ownerName = (item.Owner as BlockRecord)?.Name;
                    if (ownerName != null && !ownerName.StartsWith("*"))
                        continue;
                    ConvertAndAdd(item, model, ref converted, ref empty, ref failed);
                }
            }
            System.Diagnostics.Trace.WriteLine("dxf: *Model_Space: " + converted + " entities imported, " + empty + " without geometry, " + failed + " failed");
            model.Name = "*Model_Space";
        }

        private void FillPaperSpace(Model model)
        {
            if (!doc.BlockRecords.TryGetValue("*Paper_Space", out BlockRecord paperSpace)) return;
            int converted = 0, empty = 0, failed = 0;
            foreach (Entity item in paperSpace.Entities)
            {
                ConvertAndAdd(item, model, ref converted, ref empty, ref failed);
            }
            if (converted + empty + failed > 0)
                System.Diagnostics.Trace.WriteLine("dxf: *Paper_Space: " + converted + " entities imported, " + empty + " without geometry, " + failed + " failed");
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
            dimensionsAsDimension = Settings.GlobalSettings.GetBoolValue("DxfImport.DimensionsAsDimension", false);
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
                case ACadSharp.Entities.TableEntity dxfTable: res = CreateTable(dxfTable); break;
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

        // Convert a 2D entity's position from OCS (Object Coordinate System) to WCS.
        // DXF stores ARC/CIRCLE centers, TEXT insert points, etc. in OCS when Normal ≠ (0,0,1).
        // The OCS axes are derived via the AutoCAD Arbitrary Axis Algorithm.
        private static GeoPoint OcsToWcs(XYZ ocsPoint, XYZ normal)
        {
            GeoVector n = GeoVector(normal);
            GeoVector ax = (Math.Abs(normal.X) < 1.0 / 64 && Math.Abs(normal.Y) < 1.0 / 64)
                ? CADability.GeoVector.YAxis ^ n
                : CADability.GeoVector.ZAxis ^ n;
            GeoVector ay = n ^ ax;
            return new GeoPoint(
                ocsPoint.X * ax.x + ocsPoint.Y * ay.x + ocsPoint.Z * n.x,
                ocsPoint.X * ax.y + ocsPoint.Y * ay.y + ocsPoint.Z * n.y,
                ocsPoint.X * ax.z + ocsPoint.Y * ay.z + ocsPoint.Z * n.z);
        }

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
            ACadSharp.Tables.Layer effectiveLayer = entity.Layer;
            ACadSharp.Color effectiveColor = entity.Color;
            LineWeightType effectiveWeight = entity.LineWeight;
            ACadSharp.Tables.LineType effectiveLineType = entity.LineType;
            if (byBlockOwner != null)
            {
                // Inside a block, AutoCAD draws contents on layer "0" with the layer of the
                // entity that placed the block, and resolves ByBlock against that entity.
                if (effectiveLayer == null || effectiveLayer.Name == "0")
                    effectiveLayer = byBlockOwner.Layer ?? effectiveLayer;
                if (effectiveColor.IsByBlock) effectiveColor = byBlockOwner.Color;
                if (effectiveWeight == LineWeightType.ByBlock) effectiveWeight = byBlockOwner.LineWeight;
                if (effectiveLineType == null || effectiveLineType.Name == ACadSharp.Tables.LineType.ByBlockName)
                    effectiveLineType = byBlockOwner.LineType;
            }
            if (go is IColorDef cd) cd.ColorDef = FindOrCreateColor(effectiveColor, effectiveLayer);
            if (effectiveLayer != null && layerTable.TryGetValue(effectiveLayer.Name, out Attribute.Layer layer))
                go.Layer = layer;
            if (go is ILinePattern lp && effectiveLineType != null)
                lp.LinePattern = project.LinePatternList.Find(effectiveLineType.Name);
            if (go is ILineWidth ld)
            {
                LineWeightType lw = effectiveWeight;
                if (lw == LineWeightType.ByLayer && effectiveLayer != null) lw = effectiveLayer.LineWeight;
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


        }

        private GeoObject.Block FindBlock(BlockRecord blockRec)
        {
            if (blockRec == null) return null;
            string key = blockRec.Handle.ToString("X");
            if (byBlockOwner != null)
            {
                // The contents depend on the placing entity, so the shared cache cannot serve
                // them. Nested blocks (custom arrow heads, for instance) land here.
                if (!expandingBlocks.Add(key)) return null; // circular reference
                try { return BuildBlock(blockRec); }
                finally { expandingBlocks.Remove(key); }
            }
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

        /// <summary>
        /// Builds a block's contents without touching the cache, for the case where they depend
        /// on the entity that placed the block (see <see cref="byBlockOwner"/>). Entities on the
        /// non-plotting DEFPOINTS layer are left out: they are construction data, and ACadSharp
        /// writes them into the blocks it regenerates for dimensions.
        /// </summary>
        private GeoObject.Block BuildBlock(BlockRecord blockRec)
        {
            GeoObject.Block block = GeoObject.Block.Construct();
            block.Name = blockRec.Name;
            block.RefPoint = GeoPoint(blockRec.BlockEntity?.BasePoint ?? XYZ.Zero);
            foreach (Entity ent in blockRec.Entities)
            {
                if (IsDefpointsLayer(ent.Layer)) continue;
                try
                {
                    IGeoObject go = GeoObjectFromEntity(ent);
                    if (go != null) block.Add(go);
                }
                catch (Exception ex)
                {
                    // One unreadable part must not cost the whole dimension - the same choice
                    // ConvertAndAdd makes for the entities of a model space.
                    System.Diagnostics.Trace.WriteLine("dxf: skipped " + ent.GetType().Name
                        + " in block '" + blockRec.Name + "': " + ex.Message);
                }
            }
            return block;
        }

        /// <summary>
        /// The font a DXF text style names. A TrueType style carries the file it was loaded
        /// from, which is not what a font is called, so the style name wins where there is one.
        /// </summary>
        private static string FontNameFromStyle(ACadSharp.Tables.TextStyle style)
        {
            string filename = style?.Filename ?? "";
            string name = style?.Name ?? "";
            if (filename.EndsWith(".shx", StringComparison.OrdinalIgnoreCase))
                filename = filename.Substring(0, filename.Length - 4);
            if (filename.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
            {
                if (name.Length > 1) filename = name;
                else filename = filename.Substring(0, filename.Length - 4);
            }
            return string.IsNullOrEmpty(filename) ? "Arial" : filename;
        }

        private static bool IsDefpointsLayer(ACadSharp.Tables.Layer layer)
        {
            return layer != null && string.Equals(layer.Name,
                ACadSharp.Tables.Layer.DefpointsName, StringComparison.OrdinalIgnoreCase);
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
            GeoVector dir = GeoVector(ray.Direction);
            if (dir.IsNullVector()) return null;
            // ConstructionLine reports only its base point as extent, so the huge displayed
            // segment cannot ruin zoom-to-extents (like AutoCAD ignoring RAY/XLINE in ZOOM E)
            return ConstructionLine.MakeRay(GeoPoint(ray.StartPoint), dir);
        }

        private IGeoObject CreateXLine(ACadSharp.Entities.XLine xline)
        {
            GeoVector dir = GeoVector(xline.Direction);
            if (dir.IsNullVector()) return null;
            return ConstructionLine.MakeXLine(GeoPoint(xline.FirstPoint), dir);
        }

        private IGeoObject CreateArc(ACadSharp.Entities.Arc arc)
        {
            GeoObject.Ellipse e = GeoObject.Ellipse.Construct();
            GeoVector nor = GeoVector(arc.Normal);
            Plane plane = Plane(arc.Center, arc.Normal);
            GeoPoint wcsCenter = OcsToWcs(arc.Center, arc.Normal);
            double start = arc.StartAngle;
            double end = arc.EndAngle;
            double sweep = end - start;
            if (sweep < 0.0) sweep += Math.PI * 2.0;
            if (start == end) sweep = 0.0;
            if (start == Math.PI * 2.0 && end == 0.0) sweep = 0.0;
            e.SetArcPlaneCenterRadiusAngles(plane, wcsCenter, arc.Radius, start, sweep);
            if (e.IsCircle && sweep == 0.0 && Precision.IsEqual(e.StartPoint, e.EndPoint))
            {
                GeoObject.Ellipse circle = GeoObject.Ellipse.Construct();
                circle.SetCirclePlaneCenterRadius(plane, wcsCenter, arc.Radius);
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
            GeoPoint wcsCenter = OcsToWcs(circle.Center, circle.Normal);
            e.SetCirclePlaneCenterRadius(plane, wcsCenter, circle.Radius);
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
            // MinorAxisEndpoint was removed from ACadSharp; compute it from Normal × MajorAxis × RadiusRatio.
            GeoVector normalVec = GeoVector(ellipse.Normal);
            GeoVector minorDir = normalVec ^ majorAxisVec;
            if (!minorDir.IsNullVector()) minorDir.Norm();
            GeoVector minorAxisVec = (majorAxisVec.Length * ellipse.RadiusRatio) * minorDir;
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
            // |bulge| > 1 means the arc spans more than a half circle.
            double angle = 4.0 * Math.Atan(Math.Abs(bulge));
            double chordLen = (endPt - startPt).Length;
            if (chordLen < Precision.eps) return null;

            double radius = chordLen / (2.0 * Math.Sin(angle / 2.0));
            GeoPoint midChord = new GeoPoint(0.5 * (startPt.x + endPt.x), 0.5 * (startPt.y + endPt.y), 0.5 * (startPt.z + endPt.z));
            GeoVector perpDir = (plane.Normal ^ (endPt - startPt).Normalized).Normalized;
            // d = distance from chord midpoint to arc center
            double d = Math.Sqrt(Math.Max(0, radius * radius - (chordLen / 2.0) * (chordLen / 2.0)));
            // An arc of more than 180° wraps around its center: the center then lies on the
            // opposite side of the chord (seen from the arc's bulge side).
            if (angle > Math.PI) d = -d;
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
            // DXF SOLID polygon order is c1→c2→c4→c3 (third and fourth corners are swapped
            // relative to their group-code order). Using c1→c2→c3→c4 produces a self-intersecting
            // bowtie that fills as an X cross rather than a rectangle.
            return BuildSolidHatch(ocs,
                new XY(solid.FirstCorner.X, solid.FirstCorner.Y),
                new XY(solid.SecondCorner.X, solid.SecondCorner.Y),
                new XY(solid.FourthCorner.X, solid.FourthCorner.Y),
                new XY(solid.ThirdCorner.X, solid.ThirdCorner.Y),
                AcadColorToDrawing(solid.Color));
        }


        private IGeoObject BuildSolidHatch(Plane ocs, XY c1, XY c2, XY c3, XY c4, Color color)
        {
            HatchStyleSolid hst = FindOrCreateSolidHatchStyle(color.ToArgb() == Color.White.ToArgb() ? Color.Black : color);
            // Convert OCS corners to WCS, then remove duplicates.
            List<GeoPoint> points = new List<GeoPoint>();
            points.Add(ocs.ToGlobal(new GeoPoint2D(c1.X, c1.Y)));
            points.Add(ocs.ToGlobal(new GeoPoint2D(c2.X, c2.Y)));
            points.Add(ocs.ToGlobal(new GeoPoint2D(c3.X, c3.Y)));
            points.Add(ocs.ToGlobal(new GeoPoint2D(c4.X, c4.Y)));
            for (int i = 3; i > 0; --i)
                for (int j = 0; j < i; ++j)
                    if (Precision.IsEqual(points[j], points[i])) { points.RemoveAt(i); break; }
            if (points.Count < 3) return null;
            // Check non-collinear (throws PlaneException if degenerate)
            try { var _ = new Plane(points[0], points[1], points[2]); }
            catch (PlaneException) { return null; }
            // Project back to ocs 2D so the hatch plane keeps ocs.Normal (the SOLID's own OCS
            // normal). Using a plane derived from the cross-product of WCS points would give the
            // wrong normal direction (e.g. (0,0,-1) for CW points), causing DXF viewers to apply
            // the Arbitrary Axis Algorithm with a flipped X-axis and mirror every vertex.
            GeoPoint2D[] vertex = new GeoPoint2D[points.Count + 1];
            for (int i = 0; i < points.Count; ++i) vertex[i] = ocs.Project(points[i]);
            vertex[points.Count] = vertex[0];
            Border bdr = new Border(new Curve2D.Polyline2D(vertex));
            GeoObject.Hatch h = GeoObject.Hatch.Construct();
            h.CompoundShape = new CompoundShape(new SimpleShape(bdr));
            h.HatchStyle = hst;
            h.Plane = ocs;
            return h;
        }

        private IGeoObject CreateInsert(ACadSharp.Entities.Insert insert)
        {
            GeoObject.Block block = FindBlock(insert.Block);
            if (block != null && block.Count > 0)
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

        private static string DescribeColor(ACadSharp.Color color)
        {
            if (color.IsByLayer) return "ByLayer";
            if (color.IsByBlock) return "ByBlock";
            if (color.IsTrueColor) return "RGB(" + color.R + "," + color.G + "," + color.B + ")";
            return "ACI " + color.Index + " (" + color.R + "," + color.G + "," + color.B + ")";
        }

        private IGeoObject CreateTable(ACadSharp.Entities.TableEntity table)
        {
            int numRows = table.Rows.Count;
            int numCols = table.Columns.Count;
            if (numRows == 0 || numCols == 0) return null;
            System.Diagnostics.Trace.WriteLine("dxf: table " + table.Handle.ToString("X")
                + ": entity color " + DescribeColor(table.Color)
                + ", layer '" + (table.Layer?.Name ?? "<none>") + "' color "
                + DescribeColor(table.Layer?.Color ?? default));

            // Determine the table's horizontal (X) axis in WCS.
            GeoVector xDir = GeoVector(table.HorizontalDirection);
            if (xDir.IsNullVector())
            {
                Plane ocsPlane = Plane(table.InsertPoint, table.Normal);
                xDir = ocsPlane.ToGlobal(new GeoVector2D(new Angle(table.Rotation)));
            }
            if (xDir.IsNullVector()) xDir = CADability.GeoVector.XAxis;
            xDir = xDir.Normalized;

            GeoVector normal = GeoVector(table.Normal);
            if (normal.IsNullVector()) normal = CADability.GeoVector.ZAxis;
            normal = normal.Normalized;

            // Table grows downward: yDown = xDir × normal.
            // For standard Z-up with xDir=(1,0,0): (1,0,0)×(0,0,1)=(0,-1,0) = downward ✓
            GeoVector yDown = xDir ^ normal;
            if (yDown.IsNullVector()) yDown = -CADability.GeoVector.YAxis;
            else yDown = yDown.Normalized;

            GeoPoint origin = GeoPoint(table.InsertPoint);

            // Cumulative column X offsets (rightward)
            double[] colX = new double[numCols + 1];
            colX[0] = 0.0;
            for (int c = 0; c < numCols; c++)
                colX[c + 1] = colX[c] + table.Columns[c].Width;

            // Cumulative row Y offsets (downward, stored as positive magnitude)
            double[] rowY = new double[numRows + 1];
            rowY[0] = 0.0;
            for (int r = 0; r < numRows; r++)
                rowY[r + 1] = rowY[r] + table.Rows[r].Height;

            // Build merged-cell lookup. Modern tables (TABLECONTENT object) provide
            // MergedCellRanges; the classic ACAD_TABLE entity instead stores the merge on the
            // cells themselves: the top-left cell of a merged region carries the number of
            // spanned columns/rows in BorderWidth/BorderHeight (DXF 175/176) and the covered
            // cells are marked with MergedValue = 1 (DXF 173).
            var mergedRanges = new List<ACadSharp.Entities.TableEntity.CellRange>();
            var mergedNonPrimary = new HashSet<(int r, int c)>();
            var mergedSpan = new Dictionary<(int r, int c), ACadSharp.Entities.TableEntity.CellRange>();
            void AddMergedRange(int top, int left, int bottom, int right)
            {
                top = Math.Max(0, top);
                left = Math.Max(0, left);
                bottom = Math.Min(bottom, numRows - 1);
                right = Math.Min(right, numCols - 1);
                if (bottom < top || right < left) return;
                if (bottom == top && right == left) return; // a single cell is not a merge
                var range = new ACadSharp.Entities.TableEntity.CellRange
                {
                    TopRowIndex = top,
                    LeftColumnIndex = left,
                    BottomRowIndex = bottom,
                    RightColumnIndex = right
                };
                mergedRanges.Add(range);
                for (int r = top; r <= bottom; r++)
                {
                    for (int c = left; c <= right; c++)
                    {
                        if (r == top && c == left) mergedSpan[(r, c)] = range;
                        else mergedNonPrimary.Add((r, c));
                    }
                }
            }
            foreach (var range in table.MergedCellRanges)
                AddMergedRange(range.TopRowIndex, range.LeftColumnIndex, range.BottomRowIndex, range.RightColumnIndex);
            for (int r = 0; r < numRows; r++)
            {
                var row = table.Rows[r];
                for (int c = 0; c < numCols && c < row.Cells.Count; c++)
                {
                    if (mergedSpan.ContainsKey((r, c)) || mergedNonPrimary.Contains((r, c))) continue;
                    var cell = row.Cells[c];
                    if (cell.BorderWidth > 1 || cell.BorderHeight > 1)
                        AddMergedRange(r, c, r + Math.Max(1, cell.BorderHeight) - 1, c + Math.Max(1, cell.BorderWidth) - 1);
                }
            }
            // Cells flagged as merged-away never display their own content, even when no
            // covering range was found (defensive against inconsistent span data)
            for (int r = 0; r < numRows; r++)
            {
                var row = table.Rows[r];
                for (int c = 0; c < numCols && c < row.Cells.Count; c++)
                {
                    if (row.Cells[c].MergedValue != 0 && !mergedSpan.ContainsKey((r, c)))
                        mergedNonPrimary.Add((r, c));
                }
            }

            // Convert local table 2D coordinates (right=x, down=y) to WCS point
            GeoPoint Pt(double x, double y) => origin + x * xDir + y * yDown;

            var geoList = new GeoObjectList();

            GeoObject.Line MakeBorderLine(GeoPoint p0, GeoPoint p1)
            {
                var l = GeoObject.Line.Construct();
                l.StartPoint = p0;
                l.EndPoint = p1;
                // grid lines inherit the table entity's attributes (ByLayer color resolves
                // to the layer color, plus layer, linetype and line weight)
                SetAttributes(l, table);
                return l;
            }

            // Horizontal grid lines: skip interior segments of merged ranges
            for (int r = 0; r <= numRows; r++)
            {
                double y = rowY[r];
                // A merged range suppresses the horizontal line at row position r
                // where topRow < r <= bottomRow (line is interior to the range)
                var gaps = new List<(double from, double to)>();
                foreach (var range in mergedRanges)
                {
                    if (r > range.TopRowIndex && r <= range.BottomRowIndex)
                        gaps.Add((colX[range.LeftColumnIndex], colX[Math.Min(range.RightColumnIndex + 1, numCols)]));
                }
                if (gaps.Count == 0)
                {
                    geoList.Add(MakeBorderLine(Pt(0, y), Pt(colX[numCols], y)));
                }
                else
                {
                    gaps.Sort((a, b) => a.from.CompareTo(b.from));
                    double cur = 0;
                    foreach (var gap in gaps)
                    {
                        if (cur < gap.from - 1e-10)
                            geoList.Add(MakeBorderLine(Pt(cur, y), Pt(gap.from, y)));
                        if (gap.to > cur) cur = gap.to;
                    }
                    if (cur < colX[numCols] - 1e-10)
                        geoList.Add(MakeBorderLine(Pt(cur, y), Pt(colX[numCols], y)));
                }
            }

            // Vertical grid lines: skip interior segments of merged ranges
            for (int c = 0; c <= numCols; c++)
            {
                double x = colX[c];
                var gaps = new List<(double from, double to)>();
                foreach (var range in mergedRanges)
                {
                    if (c > range.LeftColumnIndex && c <= range.RightColumnIndex)
                        gaps.Add((rowY[range.TopRowIndex], rowY[Math.Min(range.BottomRowIndex + 1, numRows)]));
                }
                if (gaps.Count == 0)
                {
                    geoList.Add(MakeBorderLine(Pt(x, 0), Pt(x, rowY[numRows])));
                }
                else
                {
                    gaps.Sort((a, b) => a.from.CompareTo(b.from));
                    double cur = 0;
                    foreach (var gap in gaps)
                    {
                        if (cur < gap.from - 1e-10)
                            geoList.Add(MakeBorderLine(Pt(x, cur), Pt(x, gap.from)));
                        if (gap.to > cur) cur = gap.to;
                    }
                    if (cur < rowY[numRows] - 1e-10)
                        geoList.Add(MakeBorderLine(Pt(x, cur), Pt(x, rowY[numRows])));
                }
            }

            // Cell text
            for (int r = 0; r < numRows; r++)
            {
                var row = table.Rows[r];
                for (int c = 0; c < numCols; c++)
                {
                    if (mergedNonPrimary.Contains((r, c))) continue;
                    if (c >= row.Cells.Count) continue;
                    var cell = row.Cells[c];

                    var content = cell.Content ?? cell.Contents?.FirstOrDefault();
                    if (content == null) continue;

                    string cellText = content.CadValue?.FormattedValue;
                    if (string.IsNullOrEmpty(cellText))
                        cellText = content.CadValue?.Value?.ToString();
                    if (string.IsNullOrWhiteSpace(cellText)) continue;

                    // Determine cell span (merged or single)
                    int colEnd = c + 1;
                    int rowEnd = r + 1;
                    if (mergedSpan.TryGetValue((r, c), out var span))
                    {
                        colEnd = span.RightColumnIndex + 1;
                        rowEnd = span.BottomRowIndex + 1;
                    }

                    double textHeight = content.Format?.TextHeight ?? 0;
                    if (textHeight <= 0)
                    {
                        // Fall back to half the height of the primary row — not of the whole
                        // merged span, which would double the text size in a two-row merge
                        textHeight = table.Rows[r].Height * 0.5;
                    }
                    if (textHeight < 1e-5) continue;

                    // Center of cell in table-local coordinates
                    double cx = (colX[c] + colX[colEnd]) * 0.5;
                    double cy = (rowY[r] + rowY[rowEnd]) * 0.5;
                    GeoPoint textPos = Pt(cx, cy);

                    var txt = new ACadSharp.Entities.TextEntity
                    {
                        Value = cellText,
                        Height = textHeight,
                        InsertPoint = new XYZ(textPos.x, textPos.y, textPos.z),
                        Normal = table.Normal,
                        Rotation = table.Rotation,
                        HorizontalAlignment = TextHorizontalAlignment.Center,
                        VerticalAlignment = TextVerticalAlignmentType.Middle,
                    };
                    if (content.Format?.TextStyle != null)
                        txt.Style = content.Format.TextStyle;
                    var geo = CreateText(txt);
                    if (geo != null)
                    {
                        // cell texts inherit the table entity's attributes; without an explicit
                        // ColorDef a Text renders in an undefined color
                        SetAttributes(geo, table);
                        // per-cell content color (TABLECONTENT format color or the classic
                        // cell override, DXF code 64) beats the table color when it is an
                        // explicit color and not ByBlock/ByLayer
                        ACadSharp.Color formatColor = content.Format?.Color ?? default;
                        ACadSharp.Color overrideColor = cell.StyleOverride?.ContentColor ?? default;
                        ACadSharp.Color contentColor = formatColor;
                        if (contentColor.IsByBlock || contentColor.IsByLayer)
                            contentColor = overrideColor;
                        string colorSource = "table";
                        if (!contentColor.IsByBlock && !contentColor.IsByLayer && geo is IColorDef cdef)
                        {
                            cdef.ColorDef = FindOrCreateColor(contentColor, table.Layer);
                            colorSource = "cell";
                        }
                        System.Diagnostics.Trace.WriteLine("dxf: table cell [" + r + "," + c + "]: format=" + DescribeColor(formatColor)
                            + " override=" + DescribeColor(overrideColor) + " -> " + colorSource + " color");
                        geoList.Add(geo);
                    }
                }
            }

            if (geoList.Count == 0) return null;
            if (geoList.Count == 1) return geoList[0];
            var block = GeoObject.Block.Construct();
            block.Set(geoList);
            return block;
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

            long trueType = (long)(txt.Style?.TrueType ?? 0);
            text.Font = FontNameFromStyle(txt.Style);
            text.Bold = (trueType & 2L) != 0;
            text.Italic = (trueType & 1L) != 0;
            text.TextString = txtstring;

            double h = txt.Height;
            // Groups 72/73 decide which point positions the text: with anything other than
            // left/baseline alignment it is group 11 (AlignmentPoint), and group 10 is then
            // commonly left at (0,0,0). Using group 10 unconditionally piles those texts up
            // on the origin.
            XYZ anchor = txt.InsertPoint;
            if (txt.HorizontalAlignment != TextHorizontalAlignment.Left
                || txt.VerticalAlignment != TextVerticalAlignmentType.Baseline)
            {
                XYZ alignmentPoint = txt.AlignmentPoint;
                // Tolerate writers that announce a non-default alignment but never fill in
                // group 11 — fall back to the insertion point rather than the origin.
                if (alignmentPoint != XYZ.Zero || txt.InsertPoint == XYZ.Zero)
                    anchor = alignmentPoint;
            }
            Plane plane = Plane(anchor, txt.Normal);
            Angle a = new Angle(txt.Rotation);
            GeoVector2D dir2d = new GeoVector2D(a);
            GeoVector linedir = plane.ToGlobal(dir2d);
            GeoVector glyphdir = plane.ToGlobal(dir2d.ToLeft());
            text.Location = GeoPoint(anchor);
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

        /// <summary>
        /// Imports a DIMENSION as the anonymous block AutoCAD keeps with it, which holds the
        /// picture it drew: dimension line, extension lines, arrows and the measurement text.
        /// That is the faithful reading - CADability's own <see cref="GeoObject.Dimension"/>
        /// would redraw the dimension from its own style engine and look different. The data
        /// needed to build one is kept in UserData, so it can be upgraded later without a
        /// second import.
        /// </summary>
        private IGeoObject CreateDimension(ACadSharp.Entities.Dimension dimension)
        {
            if (dimensionsAsDimension)
            {
                GeoObject.Dimension native = TryCreateNativeDimension(dimension);
                if (native != null)
                {
                    SetDimensionUserData(native, dimension);
                    return native;
                }
                // the type has no counterpart or the rebuild failed: keep the picture
            }
            BlockRecord blockRec = dimension.Block;
            if (blockRec == null || blockRec.Entities.Count == 0)
            {
                // DXF R12 and several third party writers leave the anonymous block out.
                // ACadSharp draws the dimension from its definition points and style, which
                // beats dropping it without a word.
                try
                {
                    dimension.UpdateBlock();
                    blockRec = dimension.Block;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine("dxf: dimension " + dimension.Handle.ToString("X")
                        + " has no block and could not be regenerated: " + ex.Message);
                    blockRec = null;
                }
            }
            if (blockRec == null || blockRec.Entities.Count == 0) return null;

            Entity previousOwner = byBlockOwner;
            byBlockOwner = dimension; // the block's contents are mostly ByBlock on layer 0
            GeoObject.Block block;
            try { block = BuildBlock(blockRec); }
            finally { byBlockOwner = previousOwner; }
            if (block == null || block.Count == 0) return null;
            SetDimensionUserData(block, dimension);
            return block;
        }

        /// <summary>
        /// Keeps what makes the block a dimension: its type, the measured value, the text
        /// override and the dimension style it was drawn with. The marker
        /// "CADability.DxfDimension" lets an application tell imported dimensions from ordinary
        /// blocks - CADability's own <see cref="GeoObject.Dimension"/> is not used on import.
        /// </summary>
        private void SetDimensionUserData(IGeoObject go, ACadSharp.Entities.Dimension dimension)
        {
            go.UserData.Add("CADability.DxfDimension", dimension.GetType().Name);
            try { go.UserData.Add("CADability.DxfDimension.Measurement", dimension.Measurement); }
            catch (Exception) { /* a degenerate dimension has no measurement */ }
            if (!string.IsNullOrEmpty(dimension.Text))
                go.UserData.Add("CADability.DxfDimension.Text", dimension.Text);
            if (dimension.Style != null)
                go.UserData.Add("CADability.DxfDimension.Style", dimension.Style.Name);
        }

        /// <summary>
        /// Builds CADability's own <see cref="GeoObject.Dimension"/> from a DXF DIMENSION:
        /// a real dimension that can be measured, edited and moved, instead of the frozen
        /// picture the anonymous block holds. What it costs is that CADability redraws the
        /// dimension with its own style engine, so it will not look pixel for pixel like the
        /// original - which is why the caller only asks for this when told to.
        /// Returns null when the type has no counterpart or the reconstruction draws nothing;
        /// the caller then falls back to the block.
        /// </summary>
        private GeoObject.Dimension TryCreateNativeDimension(ACadSharp.Entities.Dimension dimension)
        {
            try
            {
                GeoObject.Dimension dim = MapDimensionGeometry(dimension);
                if (dim == null) return null;
                dim.DimensionStyle = FindOrCreateDimensionStyle(dimension);
                ApplyDimensionText(dim, dimension);
                return dim;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("dxf: dimension " + dimension.Handle.ToString("X")
                    + " (" + dimension.GetType().Name + ") not rebuilt as a Dimension, keeping the block: "
                    + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// The definition points of a DXF dimension, in the shape CADability's dimension wants
        /// them. DXF has eight dimension types, CADability six: the arc length dimension has no
        /// counterpart at all, and returns null here.
        /// </summary>
        private GeoObject.Dimension MapDimensionGeometry(ACadSharp.Entities.Dimension dimension)
        {
            GeoVector normal = GeoVector(dimension.Normal);
            if (normal.IsNullVector()) normal = CADability.GeoVector.ZAxis;
            normal.Norm();
            // The rotation angles below are measured in the OCS, the same frame the rest of the
            // import derives from a normal - built from the checked one, a zero normal in the
            // file would leave the arbitrary axis algorithm without an axis.
            Plane ocs = Plane(XYZ.Zero, new XYZ(normal.x, normal.y, normal.z));
            GeoVector ocsX = ocs.DirectionX;
            GeoVector ocsY = ocs.DirectionY;

            GeoObject.Dimension dim = GeoObject.Dimension.Construct();
            dim.Normal = normal;
            switch (dimension)
            {
                // DimensionLinear derives from DimensionAligned, so it has to be asked first
                case ACadSharp.Entities.DimensionLinear linear:
                    {
                        GeoVector dir = Math.Cos(linear.Rotation) * ocsX + Math.Sin(linear.Rotation) * ocsY;
                        if (dir.IsNullVector()) return null;
                        dim.DimType = GeoObject.Dimension.EDimType.DimPoints;
                        dim.DimLineDirection = dir;
                        dim.DimLineRef = GeoPoint(linear.DefinitionPoint);
                        dim.AddPoint(GeoPoint(linear.FirstPoint));
                        dim.AddPoint(GeoPoint(linear.SecondPoint));
                    }
                    break;
                case ACadSharp.Entities.DimensionAligned aligned:
                    {
                        GeoPoint p1 = GeoPoint(aligned.FirstPoint);
                        GeoPoint p2 = GeoPoint(aligned.SecondPoint);
                        GeoVector dir = p2 - p1; // an aligned dimension runs parallel to its points
                        if (dir.IsNullVector()) return null;
                        dim.DimType = GeoObject.Dimension.EDimType.DimPoints;
                        dim.DimLineDirection = dir;
                        dim.DimLineRef = GeoPoint(aligned.DefinitionPoint);
                        dim.AddPoint(p1);
                        dim.AddPoint(p2);
                    }
                    break;
                case ACadSharp.Entities.DimensionRadius radial:
                    {
                        // For a radial dimension DXF puts the center in group 10 and the point
                        // on the circle in group 15, the other way round than for a diameter.
                        GeoPoint center = GeoPoint(radial.DefinitionPoint);
                        GeoVector dir = GeoPoint(radial.AngleVertex) - center;
                        double r = dir.Length;
                        if (r < Precision.eps) return null;
                        dir.Norm();
                        dim.DimType = GeoObject.Dimension.EDimType.DimRadius;
                        dim.AddPoint(center);
                        dim.Radius = r;
                        // DimLineRef is where the dimension line runs and where it bends; a
                        // leader length of zero means the text sits on the radius itself.
                        dim.DimLineRef = center + (r + Math.Max(0.0, radial.LeaderLength)) * dir;
                    }
                    break;
                case ACadSharp.Entities.DimensionDiameter diametric:
                    {
                        GeoPoint onCircle = GeoPoint(diametric.DefinitionPoint);
                        GeoPoint opposite = GeoPoint(diametric.AngleVertex);
                        GeoPoint center = new GeoPoint(onCircle, opposite); // the two ends of the diameter
                        GeoVector dir = onCircle - center;
                        double r = dir.Length;
                        if (r < Precision.eps) return null;
                        dir.Norm();
                        dim.DimType = GeoObject.Dimension.EDimType.DimDiameter;
                        dim.AddPoint(center);
                        dim.Radius = r;
                        // Without a leader AutoCAD draws the dimension line across the circle,
                        // which is what CADability does for a reference point inside it.
                        dim.DimLineRef = diametric.LeaderLength > 0.0
                            ? center + (r + diametric.LeaderLength) * dir
                            : center + 0.5 * r * dir;
                    }
                    break;
                case ACadSharp.Entities.DimensionAngular3Pt angular3:
                    {
                        GeoPoint center = GeoPoint(angular3.AngleVertex);
                        GeoPoint arcPoint = GeoPoint(angular3.DefinitionPoint);
                        if (!SetAngleDimension(dim, center, GeoPoint(angular3.FirstPoint) - center,
                                GeoPoint(angular3.SecondPoint) - center, arcPoint, normal))
                            return null;
                    }
                    break;
                case ACadSharp.Entities.DimensionAngular2Line angular2:
                    {
                        GeoPoint center = GeoPoint(angular2.Center); // where the two lines cross
                        if (double.IsNaN(center.x) || double.IsNaN(center.y) || double.IsNaN(center.z))
                            return null; // parallel lines have no vertex
                        GeoPoint arcPoint = GeoPoint(angular2.DimensionArc);
                        GeoVector leg1 = GeoPoint(angular2.SecondPoint) - GeoPoint(angular2.FirstPoint);
                        GeoVector leg2 = GeoPoint(angular2.DefinitionPoint) - GeoPoint(angular2.AngleVertex);
                        if (!SetAngleDimension(dim, center, leg1, leg2, arcPoint, normal)) return null;
                    }
                    break;
                case ACadSharp.Entities.DimensionOrdinate ordinate:
                    {
                        // An ordinate dimension measures one feature against the origin, along
                        // the X or the Y axis of the drawing.
                        dim.DimType = GeoObject.Dimension.EDimType.DimCoord;
                        dim.DimLineDirection = ordinate.IsOrdinateTypeX ? ocsX : ocsY;
                        dim.DimLineRef = GeoPoint(ordinate.LeaderEndpoint);
                        dim.AddPoint(GeoPoint(ordinate.DefinitionPoint)); // the origin it counts from
                        dim.AddPoint(GeoPoint(ordinate.FeatureLocation));
                    }
                    break;
                default:
                    return null; // DimensionArc: CADability has no arc length dimension
            }
            // Every type but the angular one spans its plane from the dimension line and the
            // normal. Where those are parallel the dimension cannot be drawn at all, and the
            // picture in the block is the better answer.
            if (dim.DimType != GeoObject.Dimension.EDimType.DimAngle)
            {
                GeoVector dimLine = dim.DimType == GeoObject.Dimension.EDimType.DimRadius
                    || dim.DimType == GeoObject.Dimension.EDimType.DimDiameter
                    ? dim.DimLineRef - dim.GetPoint(0)
                    : dim.DimLineDirection;
                if (dimLine.IsNullVector() || (normal ^ dimLine).IsNullVector()) return null;
            }
            return dim;
        }

        /// <summary>
        /// Fills an angular dimension. DXF gives the two legs as lines that may point either
        /// way and says which of the four sectors is meant by the point the dimension arc runs
        /// through. CADability sweeps counterclockwise from the first leg to the second, so the
        /// legs are turned into the pair that encloses that point.
        /// </summary>
        private static bool SetAngleDimension(GeoObject.Dimension dim, GeoPoint center,
            GeoVector leg1, GeoVector leg2, GeoPoint arcPoint, GeoVector normal)
        {
            if (leg1.IsNullVector() || leg2.IsNullVector()) return false;
            GeoVector toArc = arcPoint - center;
            double radius = toArc.Length;
            if (radius < Precision.eps) return false;
            Plane plane;
            try { plane = new Plane(center, normal); }
            catch (PlaneException) { return false; }

            double a1 = plane.Project(leg1).Angle.Radian;
            double a2 = plane.Project(leg2).Angle.Radian;
            double toArcAngle = plane.Project(toArc).Angle.Radian;
            GeoVector best1 = leg1, best2 = leg2;
            double smallestSweep = double.MaxValue;
            for (int flip1 = 0; flip1 < 2; flip1++)
            {
                for (int flip2 = 0; flip2 < 2; flip2++)
                {
                    double start = a1 + flip1 * Math.PI;
                    double sweep = Normalized(a2 + flip2 * Math.PI - start);
                    if (Normalized(toArcAngle - start) > sweep) continue; // the arc is elsewhere
                    if (sweep >= smallestSweep) continue;
                    smallestSweep = sweep;
                    best1 = flip1 == 0 ? leg1 : -leg1;
                    best2 = flip2 == 0 ? leg2 : -leg2;
                }
            }
            best1.Norm();
            best2.Norm();
            if ((best1 ^ best2).IsNullVector()) return false; // collinear legs span no plane
            dim.DimType = GeoObject.Dimension.EDimType.DimAngle;
            dim.AddPoint(center);
            dim.AddPoint(center + radius * best1);
            dim.AddPoint(center + radius * best2);
            dim.DimLineRef = arcPoint;
            return true;
        }

        /// <summary>An angle brought into [0, 2π).</summary>
        private static double Normalized(double angle)
        {
            double res = angle % (2.0 * Math.PI);
            return res < 0.0 ? res + 2.0 * Math.PI : res;
        }

        /// <summary>
        /// The CADability dimension style for a DXF dimension. A dimension that overrides the
        /// style it names - which most dimensions in a real drawing do - gets a style of its
        /// own, so the override is not lost and does not leak into its neighbours.
        /// </summary>
        private Attribute.DimensionStyle FindOrCreateDimensionStyle(ACadSharp.Entities.Dimension dimension)
        {
            ACadSharp.Tables.DimensionStyle acad;
            try { acad = dimension.GetActiveDimensionStyle(); }
            catch (Exception) { acad = dimension.Style; }
            if (acad == null) acad = ACadSharp.Tables.DimensionStyle.Default;

            string name = dimension.Style?.Name ?? acad.Name ?? "Standard";
            if (!dimension.HasStyleOverride)
            {
                Attribute.DimensionStyle plain = project.DimensionStyleList.Find(name);
                if (plain != null) return plain;
            }
            Attribute.DimensionStyle style = Attribute.DimensionStyle.GetDefault();
            MapDimensionStyle(acad, style, dimension);
            // Overrides are the rule, not the exception, and a style per dimension would bury
            // the list. Dimensions that override the same way share one.
            for (int i = 0; i < project.DimensionStyleList.Count; i++)
            {
                if (project.DimensionStyleList[i].SameData(style)) return project.DimensionStyleList[i];
            }
            style.Name = UnusedDimensionStyleName(name);
            project.DimensionStyleList.Add(style);
            return style;
        }

        private string UnusedDimensionStyleName(string name)
        {
            if (project.DimensionStyleList.Find(name) == null) return name;
            for (int i = 1; ; i++)
            {
                string candidate = name + "_" + i.ToString();
                if (project.DimensionStyleList.Find(candidate) == null) return candidate;
            }
        }

        /// <summary>
        /// Translates the DIMSTYLE variables that CADability has a counterpart for. DIMSCALE
        /// is applied here rather than kept, because CADability draws its sizes as they are.
        /// Left out are the parts CADability expresses differently or not at all: alternate
        /// units, tolerance texts, the fit rules and the text placement variables.
        /// </summary>
        private void MapDimensionStyle(ACadSharp.Tables.DimensionStyle acad,
            Attribute.DimensionStyle style, ACadSharp.Entities.Dimension owner)
        {
            double scale = acad.ScaleFactor > 0.0 ? acad.ScaleFactor : 1.0;
            style.Types = Attribute.DimensionStyle.ETypeFlag.DimAllTypes;
            style.TextSize = acad.TextHeight * scale;
            style.TextSizeTol = acad.TextHeight * acad.ToleranceScaleFactor * scale;
            style.SymbolSize = acad.ArrowSize * scale;
            style.ExtLineOffset = acad.ExtensionLineOffset * scale;
            style.ExtLineExtension = acad.ExtensionLineExtension * scale;
            style.DimLineExtension = acad.DimensionLineExtension * scale;
            style.DimensionLineGap = acad.DimensionLineGap * scale;
            style.LineIncrement = acad.DimensionLineIncrement * scale;
            style.CenterMarkSize = Math.Abs(acad.CenterMarkSize) * scale;
            style.Scale = acad.LinearScaleFactor != 0.0 ? acad.LinearScaleFactor : 1.0;
            style.ScaleAlt = acad.AlternateUnitScaleFactor != 0.0 ? acad.AlternateUnitScaleFactor : 1.0;
            style.Round = acad.Rounding > 0.0 ? acad.Rounding : DecimalsToRounding(acad.DecimalPlaces);
            style.RoundAlt = acad.AlternateUnitRounding > 0.0
                ? acad.AlternateUnitRounding : DecimalsToRounding(acad.AlternateUnitDecimalPlaces);
            style.TextPrefix = acad.Prefix ?? "";
            style.TextPostfix = acad.Suffix ?? "";
            style.TextFont = FontNameFromStyle(acad.Style);
            // DIMTAD says whether the text sits on the dimension line or above it, DIMGAP how
            // far; CADability measures that distance in text heights.
            style.TextDist = acad.TextVerticalAlignment == DimensionTextVerticalAlignment.Centered
                || acad.TextHeight <= 0.0
                ? 0.0
                : Math.Abs(acad.DimensionLineGap) / acad.TextHeight;
            style.Symbol = MapArrowSymbol(acad);
            style.DimNoExtLine1 = acad.SuppressFirstExtensionLine;
            style.DimNoExtLine2 = acad.SuppressSecondExtensionLine;
            style.DimNoDimLine = acad.SuppressFirstDimensionLine && acad.SuppressSecondDimensionLine;
            style.DimTxtInsideHor = acad.TextInsideHorizontal;
            style.DimTxtOutsideHor = acad.TextOutsideHorizontal;
            style.DimTxtOutside = acad.TextOutsideExtensions;
            style.DimLineColor = DimensionStyleColor(acad.DimensionLineColor, owner);
            style.ExtLineColor = DimensionStyleColor(acad.ExtensionLineColor, owner);
            style.FontColor = DimensionStyleColor(acad.TextColor, owner);
            style.FillColor = style.DimLineColor;
            style.DimLineWidth = DimensionStyleWidth(acad.DimensionLineWeight, owner);
            style.ExtLineWidth = DimensionStyleWidth(acad.ExtensionLineWeight, owner);
        }

        /// <summary>DIMDEC as the rounding increment CADability formats with.</summary>
        private static double DecimalsToRounding(short decimalPlaces)
        {
            int places = Math.Min((short)8, Math.Max((short)0, decimalPlaces));
            return Math.Pow(10.0, -places);
        }

        /// <summary>
        /// A DIMSTYLE color is ByBlock by default, which for a dimension means the color of the
        /// DIMENSION itself.
        /// </summary>
        private ColorDef DimensionStyleColor(ACadSharp.Color color, Entity owner)
        {
            if (color.IsByBlock && owner != null) return FindOrCreateColor(owner.Color, owner.Layer);
            return FindOrCreateColor(color, owner?.Layer);
        }

        private Attribute.LineWidth DimensionStyleWidth(LineWeightType weight, Entity owner)
        {
            LineWeightType lw = weight;
            if (lw == LineWeightType.ByBlock && owner != null) lw = owner.LineWeight;
            if (lw == LineWeightType.ByLayer && owner?.Layer != null) lw = owner.Layer.LineWeight;
            if ((int)lw < 0) lw = LineWeightType.W0;
            return project.LineWidthList.CreateOrFind("DXF_" + lw.ToString(), ((int)lw) / 100.0);
        }

        /// <summary>
        /// AutoCAD names its arrow heads by block, CADability picks from seven shapes. An
        /// unknown or user defined arrow block becomes the filled arrow AutoCAD also uses when
        /// no block is named.
        /// </summary>
        private static Attribute.DimensionStyle.ESymbol MapArrowSymbol(ACadSharp.Tables.DimensionStyle acad)
        {
            ACadSharp.Tables.BlockRecord arrow = acad.SeparateArrowBlocks ? acad.DimArrow1 : acad.ArrowBlock;
            string name = (arrow?.Name ?? "").TrimStart('_').ToUpperInvariant();
            switch (name)
            {
                case "OPEN":
                case "OPEN30":
                case "OPEN90":
                    return Attribute.DimensionStyle.ESymbol.DimOpenArrow;
                case "CLOSED":
                case "CLOSEDBLANK":
                    return Attribute.DimensionStyle.ESymbol.DimClosedArrow;
                case "DOT":
                case "DOTSMALL":
                    return Attribute.DimensionStyle.ESymbol.DimFilledCircle;
                case "DOTBLANK":
                case "ORIGIN":
                case "ORIGIN2":
                    return Attribute.DimensionStyle.ESymbol.DimCircle;
                case "OBLIQUE":
                case "ARCHTICK":
                case "INTEGRAL":
                    return Attribute.DimensionStyle.ESymbol.DimSlash;
                default:
                    return Attribute.DimensionStyle.ESymbol.DimFilledArrow;
            }
        }

        /// <summary>
        /// DXF group 1 overrides the measured text: empty means "use the measurement", a single
        /// space means "no text at all", and anything else is the text, where "&lt;&gt;" stands
        /// for the measurement. CADability has no such placeholder, so a text around it becomes
        /// prefix and postfix and the measurement keeps being computed.
        /// </summary>
        private void ApplyDimensionText(GeoObject.Dimension dim, ACadSharp.Entities.Dimension dimension)
        {
            string text = dimension.Text;
            if (string.IsNullOrEmpty(text)) return; // measured, CADability formats it itself
            text = ReplaceControlCodes(StripMTextFormatCodes(text));
            if (text.Trim().Length == 0)
            {
                dim.SetDimText(0, " "); // a single space suppresses the text in AutoCAD
                return;
            }
            int placeholder = text.IndexOf("<>", StringComparison.Ordinal);
            if (placeholder < 0)
            {
                dim.SetDimText(0, text);
                return;
            }
            dim.SetPrefix(0, text.Substring(0, placeholder));
            dim.SetPostfix(0, text.Substring(placeholder + 2));
        }

        /// <summary>The %% codes AutoCAD uses for the symbols that are not on a keyboard.</summary>
        private static string ReplaceControlCodes(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text
                .Replace("%%c", "⌀").Replace("%%C", "⌀")  // diameter
                .Replace("%%d", "°").Replace("%%D", "°")  // degree
                .Replace("%%p", "±").Replace("%%P", "±")  // plus/minus
                .Replace("%%%", "%");
        }

        private string StripMTextFormatCodes(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
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
                    else if (next == '\\') { sb.Append('\\'); i += 2; }
                    else if (next == '{') { sb.Append('{'); i += 2; }
                    else if (next == '}') { sb.Append('}'); i += 2; }
                    else
                    {
                        // Parameterized codes (\H, \W, \A, \C, \T, \Q, \S, \f, \p, etc.)
                        // end with a semicolon. Toggle codes (\L, \l, \O, \o, \K, \k) have no
                        // parameters. Skip up to and including the terminating semicolon when
                        // present; otherwise skip just the two-char sequence.
                        int semi = value.IndexOf(';', i + 2);
                        i = semi >= 0 && semi - (i + 2) <= 64 ? semi + 1 : i + 2;
                    }
                }
                else if (value[i] == '{')
                {
                    // Find matching closing brace
                    int depth = 1;
                    int j = i + 1;
                    while (j < value.Length && depth > 0)
                    {
                        if (value[j] == '{') depth++;
                        else if (value[j] == '}') depth--;
                        j++;
                    }
                    if (i + 1 < value.Length && value[i + 1] == '}')
                    {
                        i = j; // empty group
                    }
                    else
                    {
                        // Always recurse — the format codes inside (e.g. \fArial|b0;, \H2.0;)
                        // will be stripped, preserving any actual text content.
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
            // \P paragraph breaks arrive as '\n'; trim trailing empty lines (common when the
            // content ends with \P)
            string[] lines = plainText.Split('\n');
            int numLines = lines.Length;
            while (numLines > 0 && string.IsNullOrEmpty(lines[numLines - 1]))
                numLines--;
            if (numLines == 0) return null;

            // Map attachment point to horizontal/vertical alignment. The vertical alignment
            // refers to the whole text block (Text handles multi-line block alignment).
            TextHorizontalAlignment hAlign;
            switch (mText.AttachmentPoint)
            {
                case AttachmentPointType.TopCenter:
                case AttachmentPointType.MiddleCenter:
                case AttachmentPointType.BottomCenter:
                    hAlign = TextHorizontalAlignment.Center; break;
                case AttachmentPointType.TopRight:
                case AttachmentPointType.MiddleRight:
                case AttachmentPointType.BottomRight:
                    hAlign = TextHorizontalAlignment.Right; break;
                default:
                    hAlign = TextHorizontalAlignment.Left; break;
            }
            TextVerticalAlignmentType vAlign;
            switch (mText.AttachmentPoint)
            {
                case AttachmentPointType.TopLeft:
                case AttachmentPointType.TopCenter:
                case AttachmentPointType.TopRight:
                    vAlign = TextVerticalAlignmentType.Top; break;
                case AttachmentPointType.MiddleLeft:
                case AttachmentPointType.MiddleCenter:
                case AttachmentPointType.MiddleRight:
                    vAlign = TextVerticalAlignmentType.Middle; break;
                default: // Bottom
                    vAlign = TextVerticalAlignmentType.Bottom; break;
            }

            // A single Text object carries the whole (multi-line) content; it breaks the lines
            // itself at the column width, so editing the text or the width re-wraps correctly.
            var txt = new ACadSharp.Entities.TextEntity
            {
                Value = string.Join("\n", lines, 0, numLines),
                Height = mText.Height,
                WidthFactor = 1.0,
                Rotation = mText.Rotation,
                Style = mText.Style,
                InsertPoint = mText.InsertPoint,
                // MTEXT anchors at its insertion point whatever the attachment point is;
                // mirror it into group 11 so CreateText's alignment rule picks it up.
                AlignmentPoint = mText.InsertPoint,
                Normal = mText.Normal,
                HorizontalAlignment = hAlign,
                VerticalAlignment = vAlign,
            };
            IGeoObject geo = CreateText(txt);
            if (geo is GeoObject.Text text)
            {
                // Automatic word wrap at the MTEXT column width (DXF group code 41)
                if (mText.RectangleWidth > 0) text.ColumnWidth = mText.RectangleWidth;
                // AutoCAD spaces MTEXT baselines 5/3 of the text height apart, scaled by the
                // file's line spacing factor (DXF group code 44, default 1.0)
                double factor = mText.LineSpacing > 0 ? mText.LineSpacing : 1.0;
                text.LineSpacing = 5.0 / 3.0 * factor;
            }
            return geo;
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
