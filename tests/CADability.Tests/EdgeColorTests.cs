using System.Drawing;
using System.Reflection;
using CADability.Attribute;
using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// Issue #65: the color of an edge picked in the view could be changed in the property grid, but the solid was not
    /// repainted, because nobody listens to the change events of an edge's curve. Now the face, shell or solid the edge belongs
    /// to reports changes of the attributes of its edge curves.
    /// </summary>
    [TestClass]
    public class EdgeColorTests
    {
        /// <summary>
        /// Records the color of every polyline painted. Paints only edges, no faces.
        /// </summary>
        public class Recorder : DispatchProxy
        {
            public List<(Color color, GeoPoint[] points)> Polylines = new List<(Color, GeoPoint[])>();
            private Color current;
            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                switch (targetMethod.Name)
                {
                    case "SetColor": current = (Color)args[0]; return null;
                    case "Polyline": Polylines.Add((current, (GeoPoint[])args[0])); return null;
                    case "get_PaintEdges":
                    case "get_PaintSurfaceEdges": return true;
                }
                Type ret = targetMethod.ReturnType;
                return ret == typeof(void) || !ret.IsValueType ? null : Activator.CreateInstance(ret);
            }
        }

        private static List<(Color color, GeoPoint[] points)> Paint(IGeoObject go)
        {
            IPaintTo3D paintTo3D = DispatchProxy.Create<IPaintTo3D, Recorder>();
            go.PaintTo3D(paintTo3D);
            return ((Recorder)(object)paintTo3D).Polylines;
        }

        private static Solid Box() => Make3D.MakeBox(GeoPoint.Origin, 10 * GeoVector.XAxis, 20 * GeoVector.YAxis, 30 * GeoVector.ZAxis);

        private static Color ColorOf(List<(Color color, GeoPoint[] points)> painted, Edge edge)
        {
            GeoPoint s = edge.Curve3D.StartPoint, e = edge.Curve3D.EndPoint;
            return painted.Single(p => (Precision.IsEqual(p.points[0], s) && Precision.IsEqual(p.points[p.points.Length - 1], e))
                || (Precision.IsEqual(p.points[0], e) && Precision.IsEqual(p.points[p.points.Length - 1], s))).color;
        }

        [TestMethod]
        public void changing_the_color_of_an_edge_is_reported_by_the_solid()
        {
            Project project = Project.CreateSimpleProject();
            Model model = project.GetActiveModel();
            Solid box = Box();
            model.Add(box);
            List<IGeoObject> willChange = new List<IGeoObject>(), didChange = new List<IGeoObject>();
            model.GeoObjectWillChangeEvent += (sender, change) => willChange.Add(sender);
            model.GeoObjectDidChangeEvent += (sender, change) =>
            {
                didChange.Add(sender);
                Assert.IsTrue(change.OnlyAttributeChanged);
            };
            Edge edge = box.Shells[0].Edges[0];
            ColorDef red = project.ColorList.CreateOrFind("red", Color.Red);

            (edge.Curve3D as IColorDef).ColorDef = red;

            CollectionAssert.AreEqual(new IGeoObject[] { box }, willChange, "the model hears of the change through the solid");
            CollectionAssert.AreEqual(new IGeoObject[] { box }, didChange);
            List<(Color color, GeoPoint[] points)> painted = Paint(box);
            Assert.AreEqual(Color.Red.ToArgb(), ColorOf(painted, edge).ToArgb(), "the edge is painted in its color");
            Assert.AreEqual(Color.Black.ToArgb(), ColorOf(painted, box.Shells[0].Edges[1]).ToArgb(), "the other edges stay black");

            Assert.IsTrue(project.Undo.UndoLastStep());
            Assert.AreEqual(Color.Black.ToArgb(), (edge.Curve3D as IColorDef).ColorDef.Color.ToArgb(), "the change can be undone");
            Assert.AreEqual(Color.Black.ToArgb(), ColorOf(Paint(box), edge).ToArgb());
        }

        [TestMethod]
        public void the_line_width_of_an_edge_of_a_shell_or_a_face_is_reported_too()
        {
            Model model = new Model();
            Shell shell = Box().Shells[0];
            shell = (Shell)shell.Clone(); // a shell without a solid
            Face face = (Face)Box().Shells[0].Faces[0].Clone(); // a face without a shell
            model.Add(shell);
            model.Add(face);
            List<IGeoObject> didChange = new List<IGeoObject>();
            model.GeoObjectDidChangeEvent += (sender, change) => didChange.Add(sender);
            LineWidth thick = new LineWidth("thick", 0.7);

            (shell.Edges[0].Curve3D as ILineWidth).LineWidth = thick;
            (face.Edges.First().Curve3D as ILineWidth).LineWidth = thick;

            CollectionAssert.AreEqual(new IGeoObject[] { shell, face }, didChange);
        }

        [TestMethod]
        public void geometric_changes_of_the_solid_are_reported_once()
        {
            Model model = new Model();
            Solid box = Box();
            model.Add(box);
            int didChange = 0;
            model.GeoObjectDidChangeEvent += (sender, change) => ++didChange;
            box.Modify(ModOp.Translate(1, 2, 3));
            Assert.AreEqual(1, didChange);
        }

        [TestMethod]
        public void the_color_of_an_edge_survives_cloning_and_saving()
        {
            Project project = Project.CreateSimpleProject();
            Solid box = Box();
            project.GetActiveModel().Add(box);
            ColorDef red = project.ColorList.CreateOrFind("red", Color.Red);
            (box.Shells[0].Edges[0].Curve3D as IColorDef).ColorDef = red;
            GeoPoint s = box.Shells[0].Edges[0].Curve3D.StartPoint, e = box.Shells[0].Edges[0].Curve3D.EndPoint;
            bool IsRed(Solid solid) => solid.Shells[0].Edges.Count(edge => Precision.IsEqual(edge.Curve3D.StartPoint, s) && Precision.IsEqual(edge.Curve3D.EndPoint, e)
                && (edge.Curve3D as IColorDef).ColorDef?.Color.ToArgb() == Color.Red.ToArgb()) == 1;

            Assert.IsTrue(IsRed((Solid)box.Clone()), "clone");

            MemoryStream written = new MemoryStream();
            project.WriteToJson(written); // closes the stream
            Project read = null;
            try
            {
                read = Project.ReadFromJson(new MemoryStream(written.ToArray()));
            }
            catch (TypeInitializationException ex) when (ex.GetBaseException() is PlatformNotSupportedException)
            {
                Assert.Inconclusive("reading a project needs System.Drawing printing, which is only available on Windows");
            }
            Assert.IsTrue(IsRed(read.GetActiveModel().AllObjects.OfType<Solid>().Single()), "saved and read");
        }
    }
}
