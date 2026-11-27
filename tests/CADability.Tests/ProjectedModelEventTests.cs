using System.Reflection;
using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// The constructor of <see cref="ProjectedModel"/> and <see cref="ProjectedModel.Connect(IPaintTo3D)"/> both connected the
    /// projected model to the events of the model and of its objects. Every change of an object then raised
    /// <see cref="ProjectedModel.NeedsRepaintEvent"/> twice. Removing an object worked, because the removal from the model was
    /// handled twice as well; the second test makes sure it still does with the events connected once.
    /// </summary>
    [TestClass]
    public class ProjectedModelEventTests
    {
        private static IPaintTo3D FakePaintTo3D()
        {
            return DispatchProxy.Create<IPaintTo3D, RendererRecoveryTests.Fake>();
        }

        /// <summary>
        /// Moves the start point of <paramref name="line"/> and returns how often <paramref name="projectedModel"/> asked for a repaint.
        /// </summary>
        private static int RepaintsForAChangeOf(Line line, ProjectedModel projectedModel)
        {
            int repaints = 0;
            NeedsRepaintDelegate count = (sender, arg) => ++repaints;
            projectedModel.NeedsRepaintEvent += count;
            line.StartPoint = line.StartPoint + GeoVector.XAxis;
            projectedModel.NeedsRepaintEvent -= count;
            return repaints;
        }

        [TestMethod]
        public void ConnectingDoesNotConnectTheEventsASecondTime()
        {
            Model model = new Model();
            Line line = Line.TwoPoints(GeoPoint.Origin, new GeoPoint(10, 0, 0));
            model.Add(line);
            ProjectedModel projectedModel = new ProjectedModel(model, new Projection(Projection.StandardProjection.FromTop));
            int beforeConnect = RepaintsForAChangeOf(line, projectedModel);
            Assert.IsTrue(beforeConnect > 0, "a change of an object asks for a repaint");

            projectedModel.Connect(FakePaintTo3D());

            Assert.AreEqual(beforeConnect, RepaintsForAChangeOf(line, projectedModel), "an object already in the model");
            Line added = Line.TwoPoints(GeoPoint.Origin, new GeoPoint(0, 10, 0));
            model.Add(added);
            Assert.AreEqual(beforeConnect, RepaintsForAChangeOf(added, projectedModel), "an object added after Connect");
        }

        [TestMethod]
        public void AnObjectRemovedFromTheModelNoLongerAsksForARepaint()
        {
            Model model = new Model();
            Line line = Line.TwoPoints(GeoPoint.Origin, new GeoPoint(10, 0, 0));
            model.Add(line);
            ProjectedModel projectedModel = new ProjectedModel(model, new Projection(Projection.StandardProjection.FromTop));
            projectedModel.Connect(FakePaintTo3D());

            model.Remove(line);

            Assert.AreEqual(0, RepaintsForAChangeOf(line, projectedModel));
        }
    }
}
