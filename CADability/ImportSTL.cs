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
        public ImportSTL()
        {
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

            if (!String.IsNullOrEmpty(fileName) && File.Exists(fileName))
            {
                TriangleReader reader = null;
                if (IsASCII(new StreamReader(fileName)))
                    reader = new TriangleReaderASCII(fileName);
                else
                    reader = new TriangleReaderBinary(fileName);
                res = GetShells(reader);
                reader.Close();
            }

            return res.ToArray();
        }


        public Shell[] Read(byte[] byteArray)
        {
            List<Shell> res = new List<Shell>();

            if (byteArray != null && byteArray.Length > 0)
            {
                TriangleReader reader = null;
                if (IsASCII(new StreamReader(new MemoryStream(byteArray))))
                    reader = new TriangleReaderASCII(byteArray);
                else
                    reader = new TriangleReaderBinary(byteArray);
                res = GetShells(reader);
                reader.Close();
            }

            return res.ToArray();
        }

        private bool IsASCII(StreamReader sr)
        {
            bool isASCII = false;

            using (sr)
            {
                char[] head = new char[5];
                int read = sr.ReadBlock(head, 0, 5);
                if (read != 5) throw new ApplicationException("cannot read from file");
                if (new string(head) == "solid") isASCII = true;
                else isASCII = false;
            }

            return isASCII;
        }

        private List<Shell> GetShells(TriangleReader reader)
        {
            List<Shell> res = new List<Shell>();

            OctTree<Vertex> verticesOctTree = null;
            triangle tr;
            int cnt = 0;
            Set<Face> allFaces = new Set<Face>();
            GeoObjectList dbgl = new GeoObjectList();
            do
            {
                tr = reader.GetNextTriangle();
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
//#if DEBUG
//                // TODO: some mechanism to tell whether and how to reverse engineer the stl file
//                double precision;
//                if (reader.numnum == 0) precision = part.GetExtent(0.0).Size * 1e-5;
//                else precision = Math.Pow(10, -reader.numdec / (double)(reader.numnum)); // numdec/numnum is average number of decimal places
//                part.ReconstructSurfaces(precision);
//#endif
                res.Add(part);
            }

            return res;
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

        private abstract class TriangleReader
        {
            private int _numdec = 0, _numnum = 0;


            public int numdec
            {
                get { return _numdec; }
            }

            public int numnum
            {
                get { return _numnum; }
            }

            public abstract triangle GetNextTriangle();

            public abstract void Close();

            protected void accumulatePrecision(params string[] number)
            {
                for (int i = 0; i < number.Length; i++)
                {
                    if (number[i].IndexOf('.') > 0)
                    {
                        ++_numnum;
                        _numdec += number[i].Length - number[i].IndexOf('.') - 1;
                    }
                }
            }
        }

        private class TriangleReaderASCII : TriangleReader
        {
            private StreamReader _sr = null;


            public TriangleReaderASCII(string fileName)
                : this(new StreamReader(fileName))
            {
            }

            public TriangleReaderASCII(byte[] byteArray)
                : this(new StreamReader(new MemoryStream(byteArray)))
            {
            }

            private TriangleReaderASCII(StreamReader sr)
            {
                _sr = sr;
                ReadLine(out string title);
            }

            public override triangle GetNextTriangle()
            {
                triangle res = null;

                if (ReadVector(out GeoVector normal) &&
                    CheckLine("outer loop") &&
                    ReadPoint(out GeoPoint p1) &&
                    ReadPoint(out GeoPoint p2) &&
                    ReadPoint(out GeoPoint p3) &&
                    CheckLine("endloop") &&
                    CheckLine("endfacet"))
                {
                    res = new triangle(p1, p2, p3, normal);
                }

                return res;
            }

            private bool ReadLine(out string line)
            {
                bool ok = false;
                line = null;

                try
                {
                    if (!_sr.EndOfStream)
                    {

                        line = _sr.ReadLine().Trim();
                        ok = true;
                    }
                }
                catch (IOException)
                {
                }

                return ok;
            }

            private bool CheckLine(string expectedValue)
            {
                return ReadLine(out string line) && line == expectedValue;
            }

            private bool ReadPoint(out GeoPoint p)
            {
                bool ok = false;
                p = GeoPoint.Origin;

                if (ReadLine(out string line))
                {
                    string[] vertex = line.Split(' ');
                    if (vertex.Length == 4 && vertex[0] == "vertex")
                    {
                        if (ParseDouble(vertex[1], out double p1x) &&
                            ParseDouble(vertex[2], out double p1y) &&
                            ParseDouble(vertex[3], out double p1z))
                        {
                            accumulatePrecision(vertex[1], vertex[2], vertex[3]);
                            p = new GeoPoint(p1x, p1y, p1z);
                            ok = true;
                        }
                    }
                }
                return ok;
            }

            private bool ReadVector(out GeoVector v)
            {
                bool ok = false;
                v = GeoVector.NullVector;

                if (ReadLine(out string line))
                {
                    string[] facet = line.Split(' ');
                    if (facet.Length == 5 && facet[0] == "facet" && facet[1] == "normal")
                    {
                        if (ParseDouble(facet[2], out double nx) &&
                            ParseDouble(facet[3], out double ny) &&
                            ParseDouble(facet[4], out double nz))
                        {
                            accumulatePrecision(facet[2], facet[3], facet[4]);
                            v = new GeoVector(nx, ny, nz);
                            ok = true;
                        }
                    }
                }

                return ok;
            }

            private bool ParseDouble(string s, out double result)
            {
                return double.TryParse(s, NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out result);
            }

            public override void Close()
            {
                if (_sr != null)
                    _sr.Close();
            }
        }

        private class TriangleReaderBinary : TriangleReader
        {
            private BinaryReader _br = null;


            public TriangleReaderBinary(string fileName)
                : this(new BinaryReader(File.Open(fileName, FileMode.Open)))
            {
            }

            public TriangleReaderBinary(byte[] byteArray)
                : this(new BinaryReader(new MemoryStream(byteArray)))
            {
            }

            private TriangleReaderBinary(BinaryReader br)
            {
                _br = br;
                _br.ReadBytes(80);
                uint nrtr = _br.ReadUInt32();
            }

            public override triangle GetNextTriangle()
            {
                if (_br.BaseStream.Position >= _br.BaseStream.Length) return null;
                try
                {
                    GeoVector normal = ReadVector();
                    GeoPoint p1 = ReadPoint();
                    GeoPoint p2 = ReadPoint();
                    GeoPoint p3 = ReadPoint();
                    int attr = _br.ReadUInt16();
                    triangle res = new triangle(p1, p2, p3, normal);
                    return res;
                }
                catch (EndOfStreamException)
                {
                    return null;
                }
            }

            private GeoPoint ReadPoint()
            {
                return new GeoPoint(_br.ReadSingle(), _br.ReadSingle(), _br.ReadSingle());
            }

            private GeoVector ReadVector()
            {
                return new GeoVector(_br.ReadSingle(), _br.ReadSingle(), _br.ReadSingle());
            }

            public override void Close()
            {
                if (_br != null)
                    _br.Close();
            }
        }
    }
}
