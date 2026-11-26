using System.Drawing;
using System.Reflection;
using CADability.GeoObject;
using CADability.Substitutes;
// with UseWindowsForms (the Windows build of the tests), System.Windows.Forms has these types as well
using Keys = CADability.Substitutes.Keys;
using MouseButtons = CADability.Substitutes.MouseButtons;
using MouseEventArgs = CADability.Substitutes.MouseEventArgs;
using PaintEventArgs = CADability.Substitutes.PaintEventArgs;

namespace CADability.Tests
{
    /// <summary>
    /// Issue #568: rotating the view with Ctrl and the middle mouse button got slower the more objects the model has. Every
    /// mouse move rebuilt the projected model (<see cref="ModelView.ForceInvalidateAll"/>), which visits all objects of the
    /// model, and picked the model to find the center of the rotation, at a wrong position in a view that is wider than high.
    /// </summary>
    [TestClass]
    public class ViewRotationTests
    {
        /// <summary>
        /// Implements any interface: returns the value of <see cref="Results"/> for a method name (calls it with the arguments
        /// when it is a <see cref="Func{T, TResult}"/> of object[]), a fake of the same kind for
        /// a method that returns an interface (so that canvas.Frame.UIService works) and the default value otherwise.
        /// </summary>
        public class Fake : DispatchProxy
        {
            public Dictionary<string, object> Results = new Dictionary<string, object>();
            private readonly Dictionary<Type, object> children = new Dictionary<Type, object>();
            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                if (Results.TryGetValue(targetMethod.Name, out object result)) return result is Func<object[], object> f ? f(args) : result;
                Type ret = targetMethod.ReturnType;
                if (ret == typeof(void)) return null;
                if (ret.IsInterface)
                {
                    if (!children.TryGetValue(ret, out object child))
                    {
                        child = typeof(DispatchProxy).GetMethod(nameof(DispatchProxy.Create), 2, Type.EmptyTypes)
                            .MakeGenericMethod(ret, typeof(Fake)).Invoke(null, null);
                        ((Fake)child).Results = Results; // the whole tree answers the same
                        children[ret] = child;
                    }
                    return child;
                }
                return ret.IsValueType ? Activator.CreateInstance(ret) : null;
            }
        }

        private static T Create<T>(Dictionary<string, object> results)
        {
            T proxy = DispatchProxy.Create<T, Fake>();
            ((Fake)(object)proxy).Results = results;
            return proxy;
        }

        private static readonly Rectangle clientRectangle = new Rectangle(0, 0, 1600, 900); // wider than high

        /// <summary>
        /// A view from the top on a box 50 high, the center of its top face in the center of the canvas, connected to a fake canvas
        /// on which the Ctrl key is pressed.
        /// </summary>
        private static ModelView CreateView(Model model)
        {
            Project project = Project.CreateSimpleProject();
            ModelView modelView = new ModelView(project);
            modelView.ProjectedModel = new ProjectedModel(model, new Projection(Projection.StandardProjection.FromTop));
            Dictionary<string, object> results = new Dictionary<string, object>
            {
                ["get_ClientRectangle"] = clientRectangle,
                ["get_ModifierKeys"] = Keys.Control,
                ["get_Precision"] = 0.01,
                // like the renderers: the projection computes the matrix that ProjectF uses
                ["SetProjection"] = new Func<object[], object>(args => ((Projection)args[0]).GetOpenGLProjection(0, clientRectangle.Width, 0, clientRectangle.Height, (BoundingCube)args[1])),
            };
            results["get_PaintTo3D"] = Create<IPaintTo3D>(results);
            ICanvas canvas = Create<ICanvas>(results);
            typeof(ModelView).GetField("canvas", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(modelView, canvas);
            modelView.Frame = canvas.Frame;
            modelView.Projection.SetPlacement(clientRectangle, new BoundingRect(-80, -45, 80, 45));
            return modelView;
        }

        private static Model BoxModel()
        {
            Model model = new Model();
            model.Add(Make3D.MakeBox(new GeoPoint(-10, -10, 0), 20 * GeoVector.XAxis, 20 * GeoVector.YAxis, 50 * GeoVector.ZAxis));
            return model;
        }

        private static MouseEventArgs Mouse(MouseButtons button, int x, int y)
        {
            return new MouseEventArgs { Button = button, X = x, Y = y, Location = new System.Drawing.Point(x, y) };
        }

        /// <summary>
        /// Rotates the view like the user does with Ctrl and the middle mouse button: <paramref name="steps"/> mouse moves of 5 pixels to
        /// the right, each one degree, with a paint after each move. Returns whether a mouse move made the projected model rebuild its
        /// lists with the next paint.
        /// </summary>
        private static bool Rotate(ModelView modelView, int steps)
        {
            IView view = modelView;
            typeof(ModelView).GetField("lastPanPosition", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(modelView, new System.Drawing.Point(800, 450)); // what OnMouseDown does, which needs the action stack of a frame
            bool rebuilt = false;
            for (int i = 1; i <= steps; i++)
            {
                view.OnMouseMove(Mouse(MouseButtons.Middle, 800 + 5 * i, 450));
                rebuilt |= Dirty(modelView);
                view.OnPaint(new PaintEventArgs()); // the canvas paints between the mouse moves
            }
            return rebuilt;
        }

        private static bool Dirty(ModelView modelView)
        {
            return (bool)typeof(ProjectedModel).GetField("dirty", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(modelView.ProjectedModel);
        }

        [TestMethod]
        public void RotatingDoesNotRebuildTheProjectedModelOnEveryMouseMove()
        {
            ModelView modelView = CreateView(BoxModel());
            IView view = modelView;
            view.OnPaint(new PaintEventArgs());
            Assert.IsFalse(Dirty(modelView), "the paint has used the display lists");
            GeoVector before = modelView.Projection.Direction;

            bool rebuilt = Rotate(modelView, 10);

            Assert.IsFalse(Precision.SameDirection(before, modelView.Projection.Direction, false), "the view has been rotated");
            Assert.IsFalse(rebuilt, "a new view direction does not need new display lists");
            Assert.IsTrue(modelView.projectedModelNeedsRecalc, "the projected model is updated when the rotation ends");
        }

        [TestMethod]
        public void TheViewRotatesAroundTheObjectInTheCenterOfTheView()
        {
            ModelView modelView = CreateView(BoxModel());
            IView view = modelView;
            view.OnPaint(new PaintEventArgs());
            GeoPoint topCenter = new GeoPoint(0, 0, 50);
            PointF start = modelView.Projection.ProjectF(topCenter);
            Assert.AreEqual(800, start.X, 1);
            Assert.AreEqual(450, start.Y, 1);

            Rotate(modelView, 10); // 10 degrees around the vertical axis of the screen

            // The pick for the center of the rotation was at x = 445 instead of 800 and missed the box. The view then rotated
            // around the point of the drawing plane (z = 0) and the top of the box moved away from the center by 50 * sin(10°).
            PointF after = modelView.Projection.ProjectF(topCenter);
            Assert.AreEqual(800, after.X, 1);
            Assert.AreEqual(450, after.Y, 1);
        }
    }
}
