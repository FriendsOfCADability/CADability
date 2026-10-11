using System.Reflection;
using CADability.Actions;
using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// Issue #308: ActionFeedBack.Repaint, which is called once per view, painted the feedback once for every view of the
    /// frame into that view, and opened one display list per object painted as selected on every repaint.
    /// </summary>
    [TestClass]
    public class ActionFeedBackTests
    {
        /// <summary>
        /// Implements any interface: counts the calls by method name and returns the default value of the return type, or for
        /// methods like GetColorSetting(name, default) the default passed in. AllViews returns <see cref="Views"/>.
        /// </summary>
        public class Fake : DispatchProxy
        {
            public Dictionary<string, int> Calls = new Dictionary<string, int>();
            public object[] Views;
            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                Calls.TryGetValue(targetMethod.Name, out int n);
                Calls[targetMethod.Name] = n + 1;
                if (targetMethod.Name == "get_AllViews") return Views;
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

        [TestMethod]
        public void selected_objects_are_painted_once_into_a_single_list()
        {
            (IFrame frame, Fake frameFake) = Create<IFrame>();
            IView[] views = new IView[4];
            for (int i = 0; i < views.Length; i++) views[i] = Create<IView>().proxy;
            frameFake.Views = views;
            (IPaintTo3D paintTo3D, Fake paintFake) = Create<IPaintTo3D>();

            ActionFeedBack feedBack = new ActionFeedBack();
            feedBack.Frame = frame;
            for (int i = 0; i < 3; i++) feedBack.AddSelected(Line.TwoPoints(new GeoPoint(i, 0, 0), new GeoPoint(i, 10, 0)));
            feedBack.Repaint(System.Drawing.Rectangle.Empty, views[0], paintTo3D);

            Assert.AreEqual(1, paintFake.Count("OpenList"), "one display list for all selected objects");
            Assert.AreEqual(1, paintFake.Count("CloseList"));
            Assert.AreEqual(3, paintFake.Count("Polyline"), "each of the three lines is painted once, not once per view");
        }

        [TestMethod]
        public void no_list_is_opened_without_selected_objects()
        {
            (IFrame frame, Fake frameFake) = Create<IFrame>();
            frameFake.Views = new IView[] { Create<IView>().proxy, Create<IView>().proxy };
            (IPaintTo3D paintTo3D, Fake paintFake) = Create<IPaintTo3D>();

            ActionFeedBack feedBack = new ActionFeedBack();
            feedBack.Frame = frame;
            feedBack.Add(Line.TwoPoints(GeoPoint.Origin, new GeoPoint(10, 0, 0)));
            feedBack.Repaint(System.Drawing.Rectangle.Empty, null, paintTo3D);

            Assert.AreEqual(0, paintFake.Count("OpenList"));
            Assert.AreEqual(1, paintFake.Count("Polyline"), "a feedback object is painted once, not once per view");
        }
    }
}
