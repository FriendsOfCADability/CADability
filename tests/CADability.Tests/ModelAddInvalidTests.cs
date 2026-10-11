using System.Reflection;
using ACadSharp;
using ACadSharp.IO;
using CADability.Actions;
using CADability.GeoObject;
using AcadCircle = ACadSharp.Entities.Circle;
using AcadLine = ACadSharp.Entities.Line;
using Line = CADability.GeoObject.Line;

namespace CADability.Tests
{
    /// <summary>
    /// Issue #303: Model.Add(IGeoObject) silently dropped objects whose HasValidData returned false, while the other
    /// overloads added them. Now every overload adds the object, and the check is made where invalid objects must not be
    /// created: by the interactive construct actions and by the DXF import, which used to rely on Model.Add. The exports
    /// skip invalid objects, which could already get into a model before (a circle of radius 0 aborted the DXF export).
    /// </summary>
    [TestClass]
    public class ModelAddInvalidTests
    {
        public TestContext TestContext { get; set; }

        private static Line ZeroLength() => Line.TwoPoints(new GeoPoint(1, 1, 0), new GeoPoint(1, 1, 0));

        private static Ellipse ZeroRadius()
        {
            Ellipse circle = Ellipse.Construct();
            circle.SetCirclePlaneCenterRadius(Plane.XYPlane, new GeoPoint(5, 5, 0), 0.0);
            return circle;
        }

        [TestMethod]
        public void every_overload_adds_an_invalid_object()
        {
            Model model = new Model();
            Line line = ZeroLength();
            Assert.IsFalse(line.HasValidData());
            model.Add(line);
            model.Add(new GeoObjectList(ZeroLength()));
            model.Add(new IGeoObject[] { ZeroLength() });
            Assert.AreEqual(3, model.Count);
            Assert.AreSame(model, line.Owner);
        }

        [TestMethod]
        public void an_object_can_still_be_rejected_by_the_adding_event()
        {
            Model model = new Model();
            model.AddingGeoObjectEvent += (IGeoObject go, ref bool cancel) => cancel = !go.HasValidData();
            model.Add(ZeroLength());
            model.Add(Line.TwoPoints(GeoPoint.Origin, new GeoPoint(10, 0, 0)));
            Assert.AreEqual(1, model.Count);
        }

        /// <summary>Implements IFrame: Project returns the project, AllViews no views, everything else the default value.</summary>
        public class FakeFrame : DispatchProxy
        {
            public Project Project;
            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                if (targetMethod.Name == "get_Project") return Project;
                if (targetMethod.Name == "get_AllViews") return new IView[0];
                Type ret = targetMethod.ReturnType;
                if (ret == typeof(void)) return null;
                if (args != null && args.Length == 2 && args[1] != null && ret.IsInstanceOfType(args[1])) return args[1];
                return ret.IsValueType ? Activator.CreateInstance(ret) : null;
            }
        }

        private class TestAction : ConstructAction
        {
            public override string GetID() => "ModelAddInvalidTests";
        }

        private static Model Done(IGeoObject activeObject)
        {
            Project project = Project.CreateSimpleProject();
            IFrame frame = DispatchProxy.Create<IFrame, FakeFrame>();
            ((FakeFrame)(object)frame).Project = project;
            project.SetDefaults(activeObject); // the attributes OnSetAction would give it
            TestAction action = new TestAction();
            typeof(CADability.Actions.Action).GetField("actionStack", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(action, new ActionStack(frame));
            action.SetInput();
            action.ActiveObject = activeObject;
            action.OnDone();
            return project.GetActiveModel();
        }

        [TestMethod]
        public void a_construct_action_adds_only_a_valid_object()
        {
            Assert.AreEqual(0, Done(ZeroLength()).Count, "a line with identical start and end point");
            Assert.AreEqual(0, Done(ZeroRadius()).Count, "a circle with radius 0");
            Assert.AreEqual(1, Done(Line.TwoPoints(GeoPoint.Origin, new GeoPoint(10, 0, 0))).Count);
        }

        [TestMethod]
        public void the_dxf_import_skips_degenerate_entities()
        {
            CadDocument doc = new CadDocument();
            doc.Entities.Add(new AcadLine(new CSMath.XYZ(0, 0, 0), new CSMath.XYZ(10, 0, 0)));
            doc.Entities.Add(new AcadLine(new CSMath.XYZ(3, 3, 0), new CSMath.XYZ(3, 3, 0)));
            doc.Entities.Add(new AcadCircle { Center = new CSMath.XYZ(5, 5, 0), Radius = 2 });
            string file = TestContext.TestName + ".dxf";
            DxfWriter.Write(file, doc);
            Project project = Project.ReadFromFile(file, "dxf");
            Assert.AreEqual(2, project.GetActiveModel().Count, "the line of length 0 is not imported");
        }

        [TestMethod]
        public void the_exports_skip_invalid_objects()
        {
            Project project = Project.CreateSimpleProject();
            Model model = project.GetActiveModel();
            model.Add(Line.TwoPoints(GeoPoint.Origin, new GeoPoint(10, 0, 0)));
            model.Add(new GeoObjectList(ZeroRadius(), Polyline.Construct())); // a polyline without vertices; a list was always added
            model.Add(Make3D.MakeBox(GeoPoint.Origin, 10 * GeoVector.XAxis, 10 * GeoVector.YAxis, 10 * GeoVector.ZAxis));

            string dxf = TestContext.TestName + ".dxf";
            Assert.IsTrue(project.Export(dxf, "dxf"));
            Assert.IsTrue(Project.ReadFromFile(dxf, "dxf").GetActiveModel().AllObjects.OfType<Line>().Any(), "the valid line is exported");

            string step = TestContext.TestName + ".stp";
            Assert.IsTrue(project.Export(step, "step"));
            Assert.AreEqual(1, Project.ReadFromFile(step, "step").GetActiveModel().AllObjects.OfType<Solid>().Count(), "the box is exported");
        }
    }
}
