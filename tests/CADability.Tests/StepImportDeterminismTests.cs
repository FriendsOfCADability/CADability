using CADability.GeoObject;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace CADability.Tests
{
    /// <summary>
    /// The parallel STEP import ("StepImport.Parallel", on by default) must give the same objects every time, and the same as
    /// the serial import. It made the faces of a shell on several threads: whichever thread came first made the edges and
    /// vertices shared with the neighbouring faces, so the primary face of an edge, the order of the faces in the shell and
    /// the order of the hash codes of faces, edges and vertices changed from one import to the next. The boolean operations
    /// depend on these orders, so the same file gave different results: a difference of the two parts of
    /// <c>shapeit-DifferenceBug5</c> had 25 or 26 faces, other files gave an empty result or an exception now and then.
    /// </summary>
    [TestClass]
    public class StepImportDeterminismTests
    {
        private const string ParallelSetting = "StepImport.Parallel";

        private static string StepFile(string name, [CallerFilePath] string thisFile = "")
            => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(thisFile), "Files", "Step", name);

        private static GeoObjectList Import(string file, bool parallel)
        {
            bool existed = Settings.GlobalSettings.ContainsSetting(ParallelSetting);
            object old = existed ? Settings.GlobalSettings.GetValue(ParallelSetting) : null;
            Settings.GlobalSettings.SetValue(ParallelSetting, parallel);
            try
            {
                return Project.ReadFromFile(file, "stp").GetActiveModel().AllObjects;
            }
            finally
            {
                if (existed) Settings.GlobalSettings.SetValue(ParallelSetting, old);
                else Settings.GlobalSettings.RemoveSetting(ParallelSetting);
            }
        }

        private static IEnumerable<Shell> Shells(GeoObjectList objects)
        {
            foreach (IGeoObject go in objects)
            {
                if (go is Solid sld) foreach (Shell sh in sld.Shells) yield return sh;
                else if (go is Shell shl) yield return shl;
                else if (go is Block blk) foreach (Shell sh in Shells(new GeoObjectList(Enumerable.Range(0, blk.Count).Select(blk.Child)))) yield return sh;
            }
        }

        /// <summary>
        /// Everything about the imported shells that may depend on the order in which faces, edges and vertices are made: the
        /// order of the objects and faces, which face is the primary face of an edge, the order of the edges at a vertex and the
        /// relative order of the hash codes (the absolute values depend on what was made before).
        /// </summary>
        private static string Signature(GeoObjectList objects)
        {
            StringBuilder sb = new StringBuilder();
            foreach (IGeoObject go in objects) sb.Append(go.GetType().Name).Append(' ').Append(go is Block b ? b.Count : 0).Append(';');
            sb.Append('\n');
            foreach (Shell shell in Shells(objects))
            {
                Face[] faces = shell.Faces;
                Dictionary<Face, int> faceIndex = new Dictionary<Face, int>();
                for (int i = 0; i < faces.Length; i++) faceIndex[faces[i]] = i;
                List<Edge> edges = new List<Edge>();
                Dictionary<Edge, int> edgeIndex = new Dictionary<Edge, int>();
                List<Vertex> vertices = new List<Vertex>();
                Dictionary<Vertex, int> vertexIndex = new Dictionary<Vertex, int>();
                foreach (Face face in faces)
                {
                    sb.Append(face.Surface.GetType().Name).Append(' ');
                    foreach (Edge edge in face.AllEdges)
                    {
                        if (!edgeIndex.ContainsKey(edge)) { edgeIndex[edge] = edges.Count; edges.Add(edge); }
                        sb.Append(edgeIndex[edge]).Append(',');
                        foreach (Vertex v in new[] { edge.Vertex1, edge.Vertex2 })
                        {
                            if (!vertexIndex.ContainsKey(v)) { vertexIndex[v] = vertices.Count; vertices.Add(v); }
                        }
                    }
                    sb.Append(';');
                }
                sb.Append("\nedges ");
                foreach (Edge edge in edges)
                {
                    int primary = edge.PrimaryFace != null && faceIndex.TryGetValue(edge.PrimaryFace, out int p) ? p : -1;
                    int secondary = edge.SecondaryFace != null && faceIndex.TryGetValue(edge.SecondaryFace, out int s) ? s : -1;
                    sb.Append(primary).Append('/').Append(secondary).Append('/').Append(vertexIndex[edge.Vertex1]).Append('/').Append(vertexIndex[edge.Vertex2]).Append(';');
                }
                sb.Append("\nvertex edges ");
                foreach (Vertex v in vertices) sb.Append(string.Join(",", v.Edges.Where(edgeIndex.ContainsKey).Select(e => edgeIndex[e]))).Append(';');
                sb.Append("\nhash order ");
                sb.Append(string.Join(",", Enumerable.Range(0, faces.Length).OrderBy(i => faces[i].GetHashCode()))).Append('|');
                sb.Append(string.Join(",", Enumerable.Range(0, edges.Count).OrderBy(i => edges[i].GetHashCode()))).Append('|');
                sb.Append(string.Join(",", Enumerable.Range(0, vertices.Count).OrderBy(i => vertices[i].GetHashCode()))).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>
        /// A part with many faces sharing edges and an assembly of several parts, written to a STEP file.
        /// </summary>
        private static string WriteTestFile()
        {
            List<ICurve> segments = new List<ICurve>();
            const int n = 160;
            GeoPoint Pt(int i)
            {
                double t = 2 * Math.PI * (i % n) / n;
                double r = 100 + ((i % 2 == 0) ? 0 : 3);
                return new GeoPoint(r * Math.Cos(t), r * Math.Sin(t), 0);
            }
            for (int i = 0; i < n; i += 2)
            {
                Ellipse arc = Ellipse.Construct();
                arc.SetArc3Points(Pt(i), Pt(i + 1), Pt(i + 2), Plane.XYPlane);
                segments.Add(arc);
            }
            GeoObject.Path outline = GeoObject.Path.Construct();
            outline.Set(segments.ToArray());
            Solid plate = Make3D.Extrude(Make3D.MakeFace(outline, null), 20 * GeoVector.ZAxis, null) as Solid;
            Assert.IsNotNull(plate);
            Project project = Project.CreateSimpleProject();
            Model model = project.GetActiveModel();
            model.Add(plate);
            for (int k = 0; k < 6; k++)
            {
                Solid cylinder = Make3D.MakeCylinder(new GeoPoint(300 + 30 * k, 0, 0), 5 * GeoVector.XAxis, 20 * GeoVector.ZAxis);
                model.Add(cylinder);
            }
            string file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "StepImportDeterminism-" + Guid.NewGuid().ToString("N") + ".stp");
            project.Export(file, "stp");
            Assert.IsTrue(File.Exists(file), "the STEP file was not written");
            return file;
        }

        [TestMethod]
        [DoNotParallelize] // changes a global setting
        public void ParallelImportGivesTheSameShellsAsTheSerialImport()
        {
            string file = WriteTestFile();
            try
            {
                string serial = Signature(Import(file, false));
                Assert.AreEqual(serial, Signature(Import(file, false)), "two serial imports differ");
                for (int i = 0; i < 6; i++)
                {
                    string parallel = Signature(Import(file, true));
                    Assert.AreEqual(serial, parallel, $"parallel import {i} differs from the serial import");
                }
            }
            finally
            {
                File.Delete(file);
            }
        }

        [TestMethod]
        [DoNotParallelize] // changes a global setting
        public void BooleanAfterParallelImportIsReproducible()
        {
            string Result(bool parallel)
            {
                Solid a = Import(StepFile("shapeit-DifferenceBug5-a.stp"), parallel).OfType<Solid>().Single();
                Solid b = Import(StepFile("shapeit-DifferenceBug5-b.stp"), parallel).OfType<Solid>().Single();
                Solid[] res = Solid.Subtract(a, b);
                return string.Join(";", res.Select(r => r.Shells.Sum(sh => sh.Faces.Length) + " faces, volume " + r.Volume(1e-3).ToString("F2")));
            }
            string serial = Result(false);
            for (int i = 0; i < 6; i++) Assert.AreEqual(serial, Result(true), $"parallel run {i}");
        }
    }
}
