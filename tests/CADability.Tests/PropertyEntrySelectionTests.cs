using System.Reflection;
using CADability.UserInterface;

namespace CADability.Tests
{
    /// <summary>
    /// Issue #544: entries that used to react to <see cref="IPropertyTreeView.FocusChangedEvent"/>, which is never raised,
    /// apply their deferred changes again: the grid when the edit of a distance ends, the drawing plane when the
    /// selection in the control center leaves a direction.
    /// </summary>
    [TestClass]
    public class PropertyEntrySelectionTests
    {
        /// <summary>
        /// Implements any interface: counts the calls by method name, returns the values in <see cref="Returns"/> for
        /// property getters and the default value of the return type otherwise. It keeps the handlers of
        /// <see cref="IPropertyPage.OnSelectionChanged"/>, so the test can raise it.
        /// </summary>
        public class Fake : DispatchProxy
        {
            public Dictionary<string, int> Calls = new Dictionary<string, int>();
            public Dictionary<string, object> Returns = new Dictionary<string, object>();
            public SelectionChanged SelectionChangedHandlers;
            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                Calls.TryGetValue(targetMethod.Name, out int n);
                Calls[targetMethod.Name] = n + 1;
                if (targetMethod.Name == "add_OnSelectionChanged") SelectionChangedHandlers += (SelectionChanged)args[0];
                if (targetMethod.Name == "remove_OnSelectionChanged") SelectionChangedHandlers -= (SelectionChanged)args[0];
                if (Returns.TryGetValue(targetMethod.Name, out object res)) return res;
                Type ret = targetMethod.ReturnType;
                if (ret == typeof(void)) return null;
                return ret.IsValueType ? Activator.CreateInstance(ret) : null;
            }
            public int Count(string name) => Calls.TryGetValue(name, out int n) ? n : 0;
            public void RaiseSelectionChanged(IPropertyEntry unselected, IPropertyEntry selected)
            {
                SelectionChangedHandlers?.Invoke(unselected, selected);
            }
        }

        private static (T proxy, Fake fake) Create<T>()
        {
            T proxy = DispatchProxy.Create<T, Fake>();
            return (proxy, (Fake)(object)proxy);
        }

        /// <summary>
        /// A page whose frame has a project (needed for the undo context of the edits) and an active view that counts
        /// the calls of InvalidateAll.
        /// </summary>
        private static (IPropertyPage page, Fake pageFake, IFrame frame, Fake viewFake) CreatePage()
        {
            (IView view, Fake viewFake) = Create<IView>();
            (IFrame frame, Fake frameFake) = Create<IFrame>();
            frameFake.Returns["get_Project"] = Project.CreateSimpleProject();
            frameFake.Returns["get_ActiveView"] = view;
            (IPropertyPage page, Fake pageFake) = Create<IPropertyPage>();
            pageFake.Returns["get_Frame"] = frame;
            return (page, pageFake, frame, viewFake);
        }

        [TestMethod]
        public void the_grid_changes_when_the_edit_of_a_distance_ends()
        {
            (IPropertyPage page, _, _, _) = CreatePage();
            Grid grid = new Grid();
            grid.Added(page);
            int changed = 0;
            grid.GridChangedEvent += (Grid sender) => changed++;
            DoubleProperty dx = (DoubleProperty)grid.SubEntries[0];
            dx.Added(page);

            dx.StartEdit(true);
            dx.EditTextChanged("2");
            dx.EditTextChanged("25");
            Assert.AreEqual(25.0, grid.XDistance, "the distance is set while typing");
            Assert.AreEqual(0, changed, "but the grid is not redrawn for every keystroke");

            dx.EndEdit(false, true, "25");
            Assert.AreEqual(1, changed, "the grid changes when the edit ends");

            dx.StartEdit(true);
            dx.EndEdit(false, false, "25");
            Assert.AreEqual(1, changed, "an edit without a change does not redraw the grid");
        }

        [TestMethod]
        public void the_drawing_plane_takes_a_direction_when_the_selection_leaves_it()
        {
            (IPropertyPage page, Fake pageFake, IFrame frame, Fake viewFake) = CreatePage();
            Projection projection = new Projection(Projection.StandardProjection.FromTop);
            GeoVector initialX = projection.DrawingPlane.DirectionX;
            DrawingPlaneProperty dpp = new DrawingPlaneProperty(projection, frame);
            dpp.Added(page);
            // the page makes an entry the parent of the sub entries it shows under it
            IPropertyEntry directionX = (IPropertyEntry)dpp.SubEntries[1];
            directionX.Parent = dpp;
            IPropertyEntry componentOfX = directionX.SubItems[0];
            componentOfX.Parent = directionX;

            GeoVector typed = new GeoVector(1, 1, 0);
            dpp.PlaneDirectionX = typed;
            Assert.IsTrue(Precision.SameDirection(initialX, projection.DrawingPlane.DirectionX, false), "typing only stores the direction");
            Assert.IsTrue(Precision.SameDirection(typed, dpp.PlaneDirectionX, false), "the entry shows the typed direction");

            pageFake.RaiseSelectionChanged(directionX, componentOfX);
            Assert.IsTrue(Precision.SameDirection(initialX, projection.DrawingPlane.DirectionX, false), "moving within the direction keeps it pending");
            Assert.AreEqual(0, viewFake.Count("InvalidateAll"));

            pageFake.RaiseSelectionChanged(componentOfX, dpp);
            Assert.IsTrue(Precision.SameDirection(typed, projection.DrawingPlane.DirectionX, false), "leaving the direction applies it");
            Assert.AreEqual(1, viewFake.Count("InvalidateAll"));

            pageFake.RaiseSelectionChanged(dpp, directionX);
            pageFake.RaiseSelectionChanged(directionX, dpp);
            Assert.AreEqual(1, viewFake.Count("InvalidateAll"), "leaving an unchanged direction does nothing");
        }

        [TestMethod]
        public void the_drawing_plane_entry_shows_a_plane_set_elsewhere()
        {
            (IPropertyPage page, _, IFrame frame, _) = CreatePage();
            Projection projection = new Projection(Projection.StandardProjection.FromTop);
            DrawingPlaneProperty dpp = new DrawingPlaneProperty(projection, frame);
            dpp.Added(page);

            projection.DrawingPlane = new Plane(GeoPoint.Origin, new GeoVector(1, 1, 0), new GeoVector(-1, 1, 0));
            Assert.IsTrue(Precision.SameDirection(new GeoVector(1, 1, 0), dpp.PlaneDirectionX, false), "no stale copy of the x direction");
            Assert.IsTrue(Precision.SameDirection(new GeoVector(-1, 1, 0), dpp.PlaneDirectionY, false), "no stale copy of the y direction");
        }

        [TestMethod]
        public void a_pending_direction_is_applied_when_the_entry_is_removed()
        {
            (IPropertyPage page, Fake pageFake, IFrame frame, _) = CreatePage();
            Projection projection = new Projection(Projection.StandardProjection.FromTop);
            DrawingPlaneProperty dpp = new DrawingPlaneProperty(projection, frame);
            dpp.Added(page);
            Assert.IsNotNull(pageFake.SelectionChangedHandlers, "the entry listens to the selection while it is shown");

            GeoVector typed = new GeoVector(0, 1, 1); // perpendicular to the x direction, so the plane keeps it as it is
            dpp.PlaneDirectionY = typed;
            dpp.Removed(page);
            Assert.IsTrue(Precision.SameDirection(typed, projection.DrawingPlane.DirectionY, false), "closing the control center does not lose the direction");
            Assert.IsNull(pageFake.SelectionChangedHandlers, "the entry stops listening when it is removed");
        }
    }
}
