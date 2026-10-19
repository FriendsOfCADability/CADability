using System.Collections;
using System.Reflection;
using CADability.GeoObject;
using CADability.Substitutes;
// with UseWindowsForms (the Windows build of the tests), System.Windows.Forms has a PaintEventArgs as well
using PaintEventArgs = CADability.Substitutes.PaintEventArgs;

namespace CADability.Tests
{
    /// <summary>
    /// What a host needs to recover its display when the renderer fails, is switched or loses its graphics device:
    /// a view that keeps painting after an exception (issue #493), a way to release the glyph display lists of the font
    /// cache (issue #494), an out of memory exception a renderer outside CADability can throw (issue #495) and a
    /// lightweight way to record the display lists again (issue #496).
    /// </summary>
    [TestClass]
    public class RendererRecoveryTests
    {
        /// <summary>
        /// Implements any interface: counts the calls by method name, throws for the method named in <see cref="Throw"/>,
        /// returns <see cref="PaintTo3D"/> for a canvas and the default value of the return type otherwise.
        /// </summary>
        public class Fake : DispatchProxy
        {
            public Dictionary<string, int> Calls = new Dictionary<string, int>();
            public string Throw;
            public IPaintTo3D PaintTo3D;
            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                Calls.TryGetValue(targetMethod.Name, out int n);
                Calls[targetMethod.Name] = n + 1;
                if (targetMethod.Name == Throw) throw new InvalidOperationException("renderer failed");
                if (targetMethod.Name == "get_PaintTo3D") return PaintTo3D;
                Type ret = targetMethod.ReturnType;
                if (ret == typeof(void)) return null;
                return ret.IsValueType ? Activator.CreateInstance(ret) : null;
            }
            public int Count(string name) => Calls.TryGetValue(name, out int n) ? n : 0;
        }

        private static (T proxy, Fake fake) Create<T>()
        {
            T proxy = DispatchProxy.Create<T, Fake>();
            return (proxy, (Fake)(object)proxy);
        }

        /// <summary>
        /// The renderer throws while painting. The exception goes to the host as before, but the next paint must paint
        /// again: before, the reentrancy flag stayed set and the view never painted again.
        /// </summary>
        [TestMethod]
        public void ViewPaintsAgainAfterAnException()
        {
            Project project = Project.CreateSimpleProject();
            ModelView modelView = new ModelView(project);
            modelView.ProjectedModel = new ProjectedModel(project.GetActiveModel(), new Projection(Projection.StandardProjection.FromTop));
            (IPaintTo3D paintTo3D, Fake paintFake) = Create<IPaintTo3D>();
            paintFake.Throw = "Clear";
            (ICanvas canvas, Fake canvasFake) = Create<ICanvas>();
            canvasFake.PaintTo3D = paintTo3D;
            typeof(ModelView).GetField("canvas", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(modelView, canvas);

            IView view = modelView;
            Assert.ThrowsException<InvalidOperationException>(() => view.OnPaint(new PaintEventArgs()));
            Assert.ThrowsException<InvalidOperationException>(() => view.OnPaint(new PaintEventArgs()));
            Assert.AreEqual(2, paintFake.Count("Clear"), "the second paint must reach the renderer");
        }

        /// <summary>
        /// <see cref="FontCache.Clear"/> disposes the cached glyph display lists and empties all its caches. The entries are
        /// put into the cache directly, no glyph is painted.
        /// </summary>
        [TestMethod]
        public void ClearingTheFontCacheDisposesTheGlyphLists()
        {
            // the font cache gets its fonts from FontProvider.Current and no longer needs GDI (issue #412)
            FontCache fontCache = new FontCache();
            string[] dictionaries = { "cache", "centerLineCache", "fontCharacteristicsCache", "faces" };
            IDictionary cache = Dictionary(fontCache, "cache");
            Type dictKey = typeof(FontCache).GetNestedType("DictKey", BindingFlags.NonPublic);
            Type dictVal = typeof(FontCache).GetNestedType("DictVal", BindingFlags.NonPublic);
            (IPaintTo3DList list, Fake listFake) = Create<IPaintTo3DList>();
            object withList = Activator.CreateInstance(dictVal);
            dictVal.GetField("list").SetValue(withList, list);
            dictVal.GetField("listCreated").SetValue(withList, true);
            cache[Activator.CreateInstance(dictKey, "Arial", 0, (int)'A')] = withList;
            object withoutList = Activator.CreateInstance(dictVal); // e.g. a space: the list is null
            dictVal.GetField("listCreated").SetValue(withoutList, true);
            cache[Activator.CreateInstance(dictKey, "Arial", 0, (int)' ')] = withoutList;
            Type fontKey = typeof(FontCache).GetNestedType("FontKey", BindingFlags.NonPublic);
            Dictionary(fontCache, "faces")[Activator.CreateInstance(fontKey, "Arial", 0)] = null;

            fontCache.Clear();

            Assert.AreEqual(1, listFake.Count("Dispose"));
            foreach (string name in dictionaries) Assert.AreEqual(0, Dictionary(fontCache, name).Count, name);
        }

        private static IDictionary Dictionary(FontCache fontCache, string name)
        {
            return (IDictionary)typeof(FontCache).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fontCache);
        }

        [TestMethod]
        public void OutOfMemoryCanBeThrownWithItsCause()
        {
            Exception cause = new OutOfMemoryException("E_OUTOFMEMORY");
            PaintTo3DOutOfMemory ex = new PaintTo3DOutOfMemory("vertex buffer", cause);
            Assert.AreEqual("vertex buffer", ex.Message);
            Assert.AreSame(cause, ex.InnerException);
            Assert.IsNotNull(new PaintTo3DOutOfMemory());
        }

        /// <summary>
        /// <see cref="ProjectedModel.InvalidateDisplayLists"/> makes the model and all its projected models record the display
        /// lists again and requests a repaint, without the work of <see cref="ProjectedModel.ForceRecalc"/>. The OctTree of the
        /// model, which <see cref="Model.ClearDisplayLists"/> drops, is kept.
        /// </summary>
        [TestMethod]
        public void InvalidateDisplayListsMarksAllProjectedModelsOfTheModel()
        {
            Model model = new Model();
            model.Add(Line.TwoPoints(new GeoPoint(0, 0, 0), new GeoPoint(10, 10, 0)));
            ProjectedModel first = new ProjectedModel(model, new Projection(Projection.StandardProjection.FromTop));
            ProjectedModel second = new ProjectedModel(model, new Projection(Projection.StandardProjection.FromTop));
            FieldInfo dirty = typeof(ProjectedModel).GetField("dirty", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo recalcVisibility = typeof(ProjectedModel).GetField("recalcVisibility", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo displayListsDirty = typeof(Model).GetField("displayListsDirty", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (ProjectedModel pm in new[] { first, second })
            {
                dirty.SetValue(pm, false);
                recalcVisibility.SetValue(pm, false);
            }
            displayListsDirty.SetValue(model, false);
            object octTree = model.OctTree;
            int repaints = 0;
            second.NeedsRepaintEvent += (sender, arg) => ++repaints;

            first.InvalidateDisplayLists();

            Assert.IsTrue((bool)displayListsDirty.GetValue(model));
            foreach (ProjectedModel pm in new[] { first, second })
            {
                Assert.IsTrue((bool)dirty.GetValue(pm));
                Assert.IsTrue((bool)recalcVisibility.GetValue(pm));
            }
            Assert.AreEqual(1, repaints);
            Assert.AreSame(octTree, model.OctTree);
        }
    }
}
