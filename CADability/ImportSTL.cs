using CADability.GeoObject;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Wintellect.PowerCollections;

namespace CADability
{
    public class ImportSTL
    {
        private StreamReader sr;
        private bool isASCII;
        private BinaryReader br;
        private int numdec = 0, numnum = 0;

        public ImportSTL()
        {
            // settings for precision etc
        }
        class triangle
        {
            public GeoPoint p1, p2, p3;

            public triangle(GeoPoint p1, GeoPoint p2, GeoPoint p3, GeoVector normal)
            {
                if (((p2 - p1) ^ (p3 - p2)) * normal > 0)
                {
                    this.p1 = p1;
                    this.p2 = p2;
                    this.p3 = p3;
                }
                else
                {
                    this.p1 = p1;
                    this.p2 = p3;
                    this.p3 = p2;
                }
            }
        }

        public Shell[] Read(string fileName)
        {
            List<Shell> res = new List<Shell>();

            using (sr = new StreamReader(fileName)) // may throw exceptions like "file not found" etc.
            {
                char[] head = new char[5];
                int read = sr.ReadBlock(head, 0, 5);
                if (read != 5) throw new ApplicationException("cannot read from file");
                if (new string(head) == "solid") isASCII = true;
                else isASCII = false;
            }
            if (isASCII)
            {
                sr = new StreamReader(fileName);
                string title = sr.ReadLine();
            }
            else
            {
                br = new BinaryReader(File.Open(fileName, FileMode.Open));
                br.ReadBytes(80);
                uint nrtr = br.ReadUInt32();
            }
            OctTree<Vertex> verticesOctTree = null;
            triangle tr;
            int cnt = 0;
            Set<Face> allFaces = new Set<Face>();
            GeoObjectList dbgl = new GeoObjectList();
            do
            {
                tr = GetNextTriangle();
                if (tr == null) break;
                if (verticesOctTree == null) verticesOctTree = new OctTree<Vertex>(new BoundingCube(tr.p1, tr.p2, tr.p3), 1e-6);
                try
                {
                    PlaneSurface ps = new PlaneSurface(tr.p1, tr.p2, tr.p3);
                    Vertex v1 = VertexFromPoint(verticesOctTree, tr.p1);
                    Vertex v2 = VertexFromPoint(verticesOctTree, tr.p2);
                    Vertex v3 = VertexFromPoint(verticesOctTree, tr.p3);
                    Edge e1 = Vertex.SingleConnectingEdge(v1, v2);
                    if (e1 != null && e1.SecondaryFace != null)
                    { }
                    if (e1 == null || e1.SecondaryFace != null) e1 = new Edge(Line.TwoPoints(v1.Position, v2.Position), v1, v2);
                    Edge e2 = Vertex.SingleConnectingEdge(v2, v3);
                    if (e2 != null && e2.SecondaryFace != null)
                    { }
                    if (e2 == null || e2.SecondaryFace != null) e2 = new Edge(Line.TwoPoints(v2.Position, v3.Position), v2, v3);
                    Edge e3 = Vertex.SingleConnectingEdge(v3, v1);
                    if (e3 != null && e3.SecondaryFace != null)
                    { }
                    if (e3 == null || e3.SecondaryFace != null) e3 = new Edge(Line.TwoPoints(v3.Position, v1.Position), v3, v1);
                    dbgl.Add(Line.TwoPoints(v1.Position, v2.Position));
                    dbgl.Add(Line.TwoPoints(v2.Position, v3.Position));
                    dbgl.Add(Line.TwoPoints(v3.Position, v1.Position));
                    Face fc = Face.Construct();
                    fc.Surface = ps;
                    //Line2D l1 = new Line2D(ps.Plane.Project(tr.p1), ps.Plane.Project(tr.p2));
                    //Line2D l2 = new Line2D(ps.Plane.Project(tr.p2), ps.Plane.Project(tr.p3));
                    //Line2D l3 = new Line2D(ps.Plane.Project(tr.p3), ps.Plane.Project(tr.p1));
                    //if (e1.PrimaryFace == null) e1.SetPrimary(fc, l1, true);
                    //else e1.SetSecondary(fc, l1, false);
                    //if (e2.PrimaryFace == null) e2.SetPrimary(fc, l2, true);
                    //else e2.SetSecondary(fc, l2, false);
                    //if (e3.PrimaryFace == null) e3.SetPrimary(fc, l3, true);
                    //else e3.SetSecondary(fc, l3, false);
                    e1.SetFace(fc, e1.Vertex1 == v1);
                    e2.SetFace(fc, e2.Vertex1 == v2);
                    e3.SetFace(fc, e3.Vertex1 == v3);
                    fc.Set(ps, new Edge[][] { new Edge[] { e1, e2, e3 } });
                    allFaces.Add(fc);
                    ++cnt;
                }
                catch (ModOpException)
                {
                    // empty triangle, plane construction failed
                }
            } while (tr != null);
            while (!allFaces.IsEmpty())
            {
                Shell part = Shell.CollectConnected(allFaces);
#if DEBUG
                // TODO: some mechanism to tell whether and how to reverse engineer the stl file
                double precision;
                if (numnum == 0) precision = part.GetExtent(0.0).Size * 1e-5;
                else precision = Math.Pow(10, -numdec / (double)(numnum)); // numdec/numnum is average number of decimal places
                part.ReconstructSurfaces(precision);
#endif
                res.Add(part);
            }
            return res.ToArray();
        }


        public Shell[] Read(byte[] byteArray)
        {
            List<Shell> res = new List<Shell>();

            using (sr = new StreamReader(new MemoryStream(byteArray))) // may throw exceptions like "file not found" etc.
            {
                char[] head = new char[5];
                int read = sr.ReadBlock(head, 0, 5);
                if (read != 5) throw new ApplicationException("cannot read from file");
                if (new string(head) == "solid") isASCII = true;
                else isASCII = false;
            }
            if (isASCII)
            {
                sr = new StreamReader(new MemoryStream(byteArray));
                string title = sr.ReadLine();
            }
            else
            {
                br = new BinaryReader(new MemoryStream(byteArray));
                br.ReadBytes(80);
                uint nrtr = br.ReadUInt32();
            }
            OctTree<Vertex> verticesOctTree = null;
            triangle tr;
            int cnt = 0;
            Set<Face> allFaces = new Set<Face>();
            GeoObjectList dbgl = new GeoObjectList();
            do
            {
                tr = GetNextTriangle();
                if (tr == null) break;
                if (verticesOctTree == null) verticesOctTree = new OctTree<Vertex>(new BoundingCube(tr.p1, tr.p2, tr.p3), 1e-6);
                try
                {
                    PlaneSurface ps = new PlaneSurface(tr.p1, tr.p2, tr.p3);
                    Vertex v1 = VertexFromPoint(verticesOctTree, tr.p1);
                    Vertex v2 = VertexFromPoint(verticesOctTree, tr.p2);
                    Vertex v3 = VertexFromPoint(verticesOctTree, tr.p3);
                    Edge e1 = Vertex.SingleConnectingEdge(v1, v2);
                    if (e1 != null && e1.SecondaryFace != null)
                    { }
                    if (e1 == null || e1.SecondaryFace != null) e1 = new Edge(Line.TwoPoints(v1.Position, v2.Position), v1, v2);
                    Edge e2 = Vertex.SingleConnectingEdge(v2, v3);
                    if (e2 != null && e2.SecondaryFace != null)
                    { }
                    if (e2 == null || e2.SecondaryFace != null) e2 = new Edge(Line.TwoPoints(v2.Position, v3.Position), v2, v3);
                    Edge e3 = Vertex.SingleConnectingEdge(v3, v1);
                    if (e3 != null && e3.SecondaryFace != null)
                    { }
                    if (e3 == null || e3.SecondaryFace != null) e3 = new Edge(Line.TwoPoints(v3.Position, v1.Position), v3, v1);
                    dbgl.Add(Line.TwoPoints(v1.Position, v2.Position));
                    dbgl.Add(Line.TwoPoints(v2.Position, v3.Position));
                    dbgl.Add(Line.TwoPoints(v3.Position, v1.Position));
                    Face fc = Face.Construct();
                    fc.Surface = ps;
                    //Line2D l1 = new Line2D(ps.Plane.Project(tr.p1), ps.Plane.Project(tr.p2));
                    //Line2D l2 = new Line2D(ps.Plane.Project(tr.p2), ps.Plane.Project(tr.p3));
                    //Line2D l3 = new Line2D(ps.Plane.Project(tr.p3), ps.Plane.Project(tr.p1));
                    //if (e1.PrimaryFace == null) e1.SetPrimary(fc, l1, true);
                    //else e1.SetSecondary(fc, l1, false);
                    //if (e2.PrimaryFace == null) e2.SetPrimary(fc, l2, true);
                    //else e2.SetSecondary(fc, l2, false);
                    //if (e3.PrimaryFace == null) e3.SetPrimary(fc, l3, true);
                    //else e3.SetSecondary(fc, l3, false);
                    e1.SetFace(fc, e1.Vertex1 == v1);
                    e2.SetFace(fc, e2.Vertex1 == v2);
                    e3.SetFace(fc, e3.Vertex1 == v3);
                    fc.Set(ps, new Edge[][] { new Edge[] { e1, e2, e3 } });
                    allFaces.Add(fc);
                    ++cnt;
                }
                catch (ModOpException)
                {
                    // empty triangle, plane construction failed
                }
            } while (tr != null);
            while (!allFaces.IsEmpty())
            {
                Shell part = Shell.CollectConnected(allFaces);
#if DEBUG
                // TODO: some mechanism to tell whether and how to reverse engineer the stl file
                double precision;
                if (numnum == 0) precision = part.GetExtent(0.0).Size * 1e-5;
                else precision = Math.Pow(10, -numdec / (double)(numnum)); // numdec/numnum is average number of decimal places
                part.ReconstructSurfaces(precision);
#endif
                res.Add(part);
            }
            return res.ToArray();
        }

        private Vertex VertexFromPoint(OctTree<Vertex> verticesOctTree, GeoPoint closeTo)
        {
            Vertex[] close = verticesOctTree.GetObjectsFromPoint(closeTo);
            for (int i = 0; i < close.Length; i++)
            {
                if ((close[i].Position | closeTo) == 0.0)
                {
                    return close[i];
                }
            }
            Vertex res = new Vertex(closeTo);
            verticesOctTree.AddObject(res);
            return res;
        }

        private void accumulatePrecision(params string[] number)
        {
            for (int i = 0; i < number.Length; i++)
            {
                if (number[i].IndexOf('.') > 0)
                {
                    ++numnum;
                    numdec += number[i].Length - number[i].IndexOf('.') - 1;
                }
            }
        }
        private triangle GetNextTriangle()
        {
            if (isASCII)
            {
                if (sr.EndOfStream) return null;
                try
                {
                    string[] facet = sr.ReadLine().Trim().Split(' ');
                    if (facet.Length != 5 || facet[0] != "facet" || facet[1] != "normal") return null;
                    if (!double.TryParse(facet[2], NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out double nx)) return null;
                    if (!double.TryParse(facet[3], NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out double ny)) return null;
                    if (!double.TryParse(facet[4], NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out double nz)) return null;
                    accumulatePrecision(facet[2], facet[3], facet[4]);
                    if (sr.ReadLine().Trim() != "outer loop") return null;
                    string[] vertex = sr.ReadLine().Trim().Split(' ');
                    if (vertex.Length != 4 || vertex[0] != "vertex") return null;
                    if (!double.TryParse(vertex[1], NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out double p1x)) return null;
                    if (!double.TryParse(vertex[2], NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out double p1y)) return null;
                    if (!double.TryParse(vertex[3], NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out double p1z)) return null;
                    accumulatePrecision(vertex[1], vertex[2], vertex[3]);
                    vertex = sr.ReadLine().Trim().Split(' ');
                    if (vertex.Length != 4 || vertex[0] != "vertex") return null;
                    if (!double.TryParse(vertex[1], NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out double p2x)) return null;
                    if (!double.TryParse(vertex[2], NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out double p2y)) return null;
                    if (!double.TryParse(vertex[3], NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out double p2z)) return null;
                    accumulatePrecision(vertex[1], vertex[2], vertex[3]);
                    vertex = sr.ReadLine().Trim().Split(' ');
                    if (vertex.Length != 4 || vertex[0] != "vertex") return null;
                    if (!double.TryParse(vertex[1], NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out double p3x)) return null;
                    if (!double.TryParse(vertex[2], NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out double p3y)) return null;
                    if (!double.TryParse(vertex[3], NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out double p3z)) return null;
                    accumulatePrecision(vertex[1], vertex[2], vertex[3]);
                    if (sr.ReadLine().Trim() != "endloop") return null;
                    if (sr.ReadLine().Trim() != "endfacet") return null;
                    triangle res = new triangle(new GeoPoint(p1x, p1y, p1z), new GeoPoint(p2x, p2y, p2z), new GeoPoint(p3x, p3y, p3z), new GeoVector(nx, ny, nz));
                    return res;
                }
                catch (IOException)
                {
                    return null;
                }
            }
            else
            {
                if (br.BaseStream.Position >= br.BaseStream.Length) return null;
                try
                {
                    GeoVector normal = new GeoVector(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    GeoPoint p1 = new GeoPoint(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    GeoPoint p2 = new GeoPoint(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    GeoPoint p3 = new GeoPoint(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    int attr = br.ReadUInt16();
                    triangle res = new triangle(p1, p2, p3, normal);
                    return res;
                }
                catch (EndOfStreamException)
                {
                    return null;
                }
            }
        }
    }
}
