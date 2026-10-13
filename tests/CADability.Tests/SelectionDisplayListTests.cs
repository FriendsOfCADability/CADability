using System.Reflection;
using CADability.Actions;
using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// The selection of a spline is painted as a yellow polyline from the cached approximation of the spline. The
    /// selection kept the display list it made when the objects were selected, while the model recalculated its own
    /// display list more precisely after zooming in, so the yellow polyline lay beside the black one in some places and
    /// on it in others (the spline of issue #173).
    /// </summary>
    [TestClass]
    public class SelectionDisplayListTests
    {
        /// <summary>
        /// Like <see cref="ActionFeedBackTests.Fake"/>, and also returns <see cref="Model"/> for the model of a view and
        /// <see cref="Capabilities"/> and <see cref="Precision"/> for a paint target. It records the precision a polyline
        /// is painted with as the number of its points.
        /// </summary>
        public class Fake : DispatchProxy
        {
            public Dictionary<string, int> Calls = new Dictionary<string, int>();
            public Model Model;
            public PaintCapabilities Capabilities;
            public double Precision;
            public List<int> PolylinePointCounts = new List<int>();
            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                Calls.TryGetValue(targetMethod.Name, out int n);
                Calls[targetMethod.Name] = n + 1;
                switch (targetMethod.Name)
                {
                    case "get_Model": return Model;
                    case "get_Capabilities": return Capabilities;
                    case "get_Precision": return Precision;
                    case "Polyline": PolylinePointCounts.Add(((GeoPoint[])args[0]).Length); break;
                    case "CloseList": return DispatchProxy.Create<IPaintTo3DList, Fake>();
                }
                Type ret = targetMethod.ReturnType;
                if (ret == typeof(void)) return null;
                if (args != null && args.Length == 2 && args[1] != null && ret.IsInstanceOfType(args[1])) return args[1];
                return ret.IsValueType ? Activator.CreateInstance(ret) : null;
            }
            public int Count(string name) => Calls.TryGetValue(name, out int n) ? n : 0;
        }

        private static (T proxy, Fake fake) Create<T>()
        {
            T proxy = DispatchProxy.Create<T, Fake>();
            return (proxy, (Fake)(object)proxy);
        }

        private static readonly MethodInfo onRepaintSelect = typeof(SelectObjectsAction).GetMethod("OnRepaintSelect", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// Selects a spline of a model in a <see cref="SelectObjectsAction"/> and repaints the selection twice, with the
        /// display list precision of the model set to <paramref name="first"/> and then to <paramref name="second"/>.
        /// Returns the paint target fake.
        /// </summary>
        private static Fake RepaintSelectionTwice(double first, double second, out BSpline spline)
        {
            Project project = Project.CreateSimpleProject();
            Model model = project.GetActiveModel();
            spline = BSpline.Construct();
            spline.ThroughPoints(new GeoPoint[] { new GeoPoint(0, 0, 0), new GeoPoint(30, 40, 0), new GeoPoint(60, -10, 0), new GeoPoint(100, 20, 0) }, 3, false);
            model.Add(spline);

            (IFrame frame, Fake _) = Create<IFrame>();
            SelectObjectsAction action = new SelectObjectsAction(frame);
            typeof(SelectObjectsAction).GetField("selectedObjects", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(action, new GeoObjectList(spline));
            (IView view, Fake viewFake) = Create<IView>();
            viewFake.Model = model;
            (IPaintTo3D paintTo3D, Fake paintFake) = Create<IPaintTo3D>();
            paintFake.Capabilities = PaintCapabilities.ZoomIndependentDisplayList;

            model.displayListPrecision = first;
            paintFake.Precision = first;
            onRepaintSelect.Invoke(action, new object[] { System.Drawing.Rectangle.Empty, view, paintTo3D });
            // the model recalculates its display lists, which refines the cached approximation of the spline
            spline.PrepareDisplayList(second);
            model.displayListPrecision = second;
            onRepaintSelect.Invoke(action, new object[] { System.Drawing.Rectangle.Empty, view, paintTo3D });
            return paintFake;
        }

        [TestMethod]
        public void the_selection_is_made_again_when_the_model_display_list_gets_more_precise()
        {
            Fake paint = RepaintSelectionTwice(1.0, 0.01, out BSpline spline);
            Assert.AreEqual(2, paint.Count("OpenList"), "the selection must be made again for the more precise model display list");
            Assert.AreEqual(2, paint.PolylinePointCounts.Count);
            Assert.IsTrue(paint.PolylinePointCounts[1] > paint.PolylinePointCounts[0],
                $"the second selection must follow the finer approximation ({paint.PolylinePointCounts[0]} and {paint.PolylinePointCounts[1]} points)");
        }

        [TestMethod]
        public void the_selection_is_kept_while_the_model_display_list_stays()
        {
            Fake paint = RepaintSelectionTwice(0.5, 0.5, out BSpline _);
            Assert.AreEqual(1, paint.Count("OpenList"), "an unchanged selection is painted from its display list");
            Assert.AreEqual(2, paint.Count("SelectedList"));
        }

        /// <summary>
        /// #416, finding 17: with two views of different models the selection compared the display list precision of the
        /// model of the view that was painted, so it was made again on every other repaint. The precision of the model of
        /// the selected objects decides now; the other view does not show the selection anyway.
        /// </summary>
        [TestMethod]
        public void the_selection_is_kept_while_views_of_another_model_are_painted()
        {
            Project project = Project.CreateSimpleProject();
            Model model = project.GetActiveModel();
            Model other = new Model();
            Line line = Line.TwoPoints(new GeoPoint(0, 0, 0), new GeoPoint(10, 5, 0));
            model.Add(line);
            model.displayListPrecision = 0.5;
            other.displayListPrecision = 1.0;

            (IFrame frame, Fake _) = Create<IFrame>();
            SelectObjectsAction action = new SelectObjectsAction(frame);
            typeof(SelectObjectsAction).GetField("selectedObjects", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(action, new GeoObjectList(line));
            (IView view, Fake viewFake) = Create<IView>();
            viewFake.Model = model;
            (IView otherView, Fake otherViewFake) = Create<IView>();
            otherViewFake.Model = other;
            (IPaintTo3D paintTo3D, Fake paintFake) = Create<IPaintTo3D>();
            paintFake.Capabilities = PaintCapabilities.ZoomIndependentDisplayList;

            foreach (IView v in new[] { view, otherView, view, otherView, view })
                onRepaintSelect.Invoke(action, new object[] { System.Drawing.Rectangle.Empty, v, paintTo3D });
            Assert.AreEqual(1, paintFake.Count("OpenList"), "the selection is made once");
            Assert.AreEqual(3, paintFake.Count("SelectedList"), "and shown in the three repaints of the view of its model");
        }
    }
}
