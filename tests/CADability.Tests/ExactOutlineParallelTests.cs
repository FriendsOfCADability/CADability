using CADability.Attribute;
using CADability.Curve2D;
using CADability.GeoObject;
using CADability.Shapes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Runtime.CompilerServices;
using Path = System.IO.Path;

namespace CADability.Tests
{
    /// <summary>
    /// Parallels of outlines with exact splines and ellipses (issue #410). Only lines and arcs have an exact parallel;
    /// <see cref="Border.GetParallel"/> threw for every other curve unless it was told to approximate splines, so
    /// <see cref="SimpleShape.Shrink"/>, <see cref="SimpleShape.Expand"/> and the contour hatch failed on the exact outlines
    /// that faces and hatches keep since #374. They are approximated by lines and arcs now, with the precision of the parallel.
    /// </summary>
    [TestClass]
    public class ExactOutlineParallelTests
    {
        private static string Issue173([CallerFilePath] string thisFile = "")
            => Path.Combine(Path.GetDirectoryName(thisFile), "Files", "Dxf", "issue173.dxf");

        /// <summary>A smooth closed spline, about 52 x 24, with an elliptic hole.</summary>
        private static CompoundShape SplineWithEllipticHole()
        {
            GeoPoint2D[] points = Enumerable.Range(0, 12).Select(i => new GeoPoint2D(
                20 * Math.Cos(i * Math.PI / 6) * (1 + 0.3 * Math.Sin(i * Math.PI / 2)), 12 * Math.Sin(i * Math.PI / 6))).ToArray();
            Border outline = new Border(new BSpline2D(points, 3, true));
            Border hole = new Border(new Ellipse2D(new GeoPoint2D(-6, 0), new GeoVector2D(4, 0), new GeoVector2D(0, 2.5)));
            return new CompoundShape(new SimpleShape(outline, hole));
        }

        /// <summary>The closed spline with corners of issue 173 (coinciding poles), 81.5 x 128.1, as one exact curve.</summary>
        private static CompoundShape Issue173Shape()
        {
            Project project = Project.ReadFromFile(Issue173(), "dxf");
            BSpline spline = project.GetActiveModel().AllObjects.OfType<BSpline>().Single();
            return new CompoundShape(new SimpleShape(new Border((spline as ICurve).GetProjectedCurve(Plane.XYPlane))));
        }

        private static CompoundShape Approximated(CompoundShape shape)
        {
            CompoundShape res = shape.Clone();
            res.Approximate(false, 1e-3);
            return res;
        }

        private static bool HasExactCurves(CompoundShape shape)
            => shape.SimpleShapes.SelectMany(ss => ss.Holes.Prepend(ss.Outline)).SelectMany(b => b.Segments).Any(c => c is BSpline2D || c is Ellipse2D);

        [TestMethod]
        public void ShrinkAndExpandOfExactOutlines()
        {
            foreach ((string name, CompoundShape exact, double d) in new[] { ("spline with elliptic hole", SplineWithEllipticHole(), 1.0), ("issue 173", Issue173Shape(), 4.0) })
            {
                Assert.IsTrue(HasExactCurves(exact), name);
                CompoundShape approximated = Approximated(exact);
                for (int i = 1; i <= 3; i++)
                {
                    CompoundShape shrunk = exact.Shrink(i * d);
                    CompoundShape reference = approximated.Shrink(i * d);
                    Assert.AreEqual(reference.SimpleShapes.Length, shrunk.SimpleShapes.Length, $"{name}: parts after shrinking by {i * d}");
                    Assert.AreEqual(reference.Area, shrunk.Area, reference.Area * 1e-3, $"{name}: area after shrinking by {i * d}");
                }
                CompoundShape expanded = exact.Expand(d);
                Assert.AreEqual(approximated.Expand(d).Area, expanded.Area, expanded.Area * 1e-3, $"{name}: area after expanding by {d}");
            }
        }

        [TestMethod]
        public void ContourHatchOfExactOutlines()
        {
            foreach ((string name, CompoundShape exact, double d) in new[] { ("spline with elliptic hole", SplineWithEllipticHole(), 1.0), ("issue 173", Issue173Shape(), 4.0) })
            {
                CompoundShape approximated = Approximated(exact);
                foreach (HatchStyleContour.ESpiralMode mode in new[] { HatchStyleContour.ESpiralMode.Parallel, HatchStyleContour.ESpiralMode.ContourSpiral })
                {
                    HatchStyleContour style = new HatchStyleContour { LineDistance = d, FirstDistance = d, SpiralMode = mode };
                    GeoObjectList fromExact = style.GenerateContent(exact, Plane.XYPlane);
                    GeoObjectList fromApproximated = style.GenerateContent(approximated, Plane.XYPlane);
                    Assert.IsTrue(fromExact.Count > 0, $"{name}, {mode}: no contours");
                    Assert.AreEqual(fromApproximated.Count, fromExact.Count, $"{name}, {mode}: number of contours");
                    double length = fromExact.OfType<ICurve>().Sum(c => c.Length);
                    double referenceLength = fromApproximated.OfType<ICurve>().Sum(c => c.Length);
                    Assert.AreEqual(referenceLength, length, referenceLength * 1e-2, $"{name}, {mode}: length of the contours");
                }
            }
        }

        /// <summary>
        /// A line hatch with a margin shrinks its shape. It approximated the shape of the hatch itself before, so the
        /// exact outline of the hatch was lost when its content was generated.
        /// </summary>
        [TestMethod]
        public void LineHatchWithMarginKeepsTheExactOutline()
        {
            CompoundShape shape = SplineWithEllipticHole();
            HatchStyleLines style = new HatchStyleLines { LineDistance = 1.0, LineAngle = new Angle(Math.PI / 6), MarginOffset = 0.5 };
            GeoObjectList lines = style.GenerateContent(shape, Plane.XYPlane);
            Assert.IsTrue(lines.Count > 10, "hatch lines");
            Assert.IsTrue(HasExactCurves(shape), "the shape of the hatch keeps its spline and its ellipse");
        }
    }
}
