using CADability.Actions;
using CADability.Curve2D;
using CADability.GeoObject;
using CADability.Shapes;
using System.Collections;
using System.Globalization;
using System.Reflection;

namespace CADability.Tests
{
    /// <summary>
    /// <see cref="CompoundShape.CreateFromList(ICurve2D[], double, bool, out GeoObjectList)"/> nests the closed contours of
    /// a whole drawing with a containment forest (<see cref="CurveGraph.NestByContainment"/>) and removes open curves
    /// without traversing the whole cluster tree for every one of them. Both must give exactly the results of the original
    /// code: <see cref="CurveGraph.NestPairwise"/> is that code, and the removal of dead ends is compared with a copy of the
    /// original implementation.
    /// </summary>
    [TestClass]
    public class CurveGraphNestingTests
    {
        private const double MaxGap = 0.1;

        #region generator
        /// <summary>
        /// Random drawings of nested contours, perforated parts and open curves.
        /// </summary>
        private class SheetGenerator
        {
            private readonly Random rnd;
            public readonly List<ICurve2D> Curves = new List<ICurve2D>();
            public SheetGenerator(int seed) { rnd = new Random(seed); }

            public void Rect(double x0, double y0, double x1, double y1)
            {
                GeoPoint2D[] p = { new GeoPoint2D(x0, y0), new GeoPoint2D(x1, y0), new GeoPoint2D(x1, y1), new GeoPoint2D(x0, y1) };
                for (int k = 0; k < 4; k++)
                {
                    GeoPoint2D a = p[k], b = p[(k + 1) % 4];
                    if (rnd.Next(4) == 0)
                    {   // an edge split into two lines
                        GeoPoint2D m = new GeoPoint2D((a.x + b.x) / 2, (a.y + b.y) / 2);
                        Curves.Add(new Line2D(a, m));
                        Curves.Add(new Line2D(m, b));
                    }
                    else Curves.Add(new Line2D(a, b));
                }
            }
            public void Circle(double cx, double cy, double r)
            {
                GeoPoint2D c = new GeoPoint2D(cx, cy);
                switch (rnd.Next(3))
                {
                    case 0:
                        Curves.Add(new Circle2D(c, r)); // a single closed curve, kept in Border.UnsplittedOutline
                        break;
                    case 1:
                        double a0 = rnd.NextDouble() * Math.PI;
                        Curves.Add(new Arc2D(c, r, new Angle(a0), SweepAngle.Opposite));
                        Curves.Add(new Arc2D(c, r, new Angle(a0 + Math.PI), SweepAngle.Opposite));
                        break;
                    default:
                        for (int k = 0; k < 4; k++) Curves.Add(new Arc2D(c, r, new Angle(k * Math.PI / 2), new SweepAngle(Math.PI / 2)));
                        break;
                }
            }
            /// <summary>
            /// A rectangle or circle centered at (cx, cy) that fits in a square of the given size, with nested contours
            /// inside down to maxDepth (depth 0 is the outermost contour).
            /// </summary>
            public void Nested(double cx, double cy, double size, int depth, int maxDepth)
            {
                double inner; // half the size of a square inside the contour
                if (rnd.Next(2) == 0)
                {
                    double w = size * (0.8 + 0.2 * rnd.NextDouble()) / 2, h = size * (0.6 + 0.4 * rnd.NextDouble()) / 2;
                    Rect(cx - w, cy - h, cx + w, cy + h);
                    inner = Math.Min(w, h);
                }
                else
                {
                    double r = size / 2 * (0.7 + 0.3 * rnd.NextDouble());
                    Circle(cx, cy, r);
                    inner = r / Math.Sqrt(2);
                }
                if (depth >= maxDepth) return;
                int m = rnd.Next(3) + 1;
                double cell = 2 * inner * 0.9 / m;
                double ox = cx - inner * 0.9, oy = cy - inner * 0.9;
                for (int i = 0; i < m; i++)
                {
                    for (int j = 0; j < m; j++)
                    {
                        // contours much smaller than that would be in the range of maxGap
                        if (rnd.Next(3) != 0 && cell * 0.8 >= 4) Nested(ox + (i + 0.5) * cell, oy + (j + 0.5) * cell, cell * 0.8, depth + 1, maxDepth);
                    }
                }
            }
            public void Perforated(double x, double y, double w, double h, int m)
            {
                Rect(x, y, x + w, y + h);
                double dx = w / (m + 1), dy = h / (m + 1), r = Math.Min(dx, dy) * 0.3;
                for (int i = 1; i <= m; i++)
                {
                    for (int j = 1; j <= m; j++) Circle(x + i * dx, y + j * dy, r);
                }
            }
            public void OpenChain(double x, double y, int segments)
            {
                GeoPoint2D p = new GeoPoint2D(x, y);
                for (int i = 0; i < segments; i++)
                {
                    GeoPoint2D q = new GeoPoint2D(p.x + 3 + rnd.NextDouble() * 5, p.y + (rnd.NextDouble() - 0.5) * 6);
                    Curves.Add(new Line2D(p, q));
                    p = q;
                }
            }
            /// <summary>
            /// A square with a stub at a corner, an open chain hanging from a vertex on an edge and an isolated line inside.
            /// </summary>
            public void PartWithBranches(double x, double y, double size)
            {
                GeoPoint2D a = new GeoPoint2D(x, y), b = new GeoPoint2D(x + size, y), c = new GeoPoint2D(x + size, y + size), d = new GeoPoint2D(x, y + size);
                GeoPoint2D m = new GeoPoint2D(x + size / 2, y);
                Curves.Add(new Line2D(a, m));
                Curves.Add(new Line2D(m, b));
                Curves.Add(new Line2D(b, c));
                Curves.Add(new Line2D(c, d));
                Curves.Add(new Line2D(d, a));
                Curves.Add(new Line2D(c, new GeoPoint2D(c.x + 5, c.y + 5)));
                Curves.Add(new Line2D(m, new GeoPoint2D(m.x, m.y - 4)));
                Curves.Add(new Line2D(new GeoPoint2D(m.x, m.y - 4), new GeoPoint2D(m.x + 3, m.y - 8)));
                Curves.Add(new Line2D(new GeoPoint2D(x + size / 2, y + size / 2), new GeoPoint2D(x + size / 2 + 3, y + size / 2 + 2)));
            }
            /// <summary>
            /// A square with a gap smaller than maxGap at a corner, and two open lines whose ends are closer than maxGap.
            /// </summary>
            public void Gaps(double x, double y, double size, double gap)
            {
                GeoPoint2D a = new GeoPoint2D(x, y), b = new GeoPoint2D(x + size, y), c = new GeoPoint2D(x + size, y + size), d = new GeoPoint2D(x, y + size);
                Curves.Add(new Line2D(new GeoPoint2D(x + gap, y), b));
                Curves.Add(new Line2D(b, c));
                Curves.Add(new Line2D(c, d));
                Curves.Add(new Line2D(d, a));
                Curves.Add(new Line2D(new GeoPoint2D(x, y - 20), new GeoPoint2D(x + 10, y - 20)));
                Curves.Add(new Line2D(new GeoPoint2D(x + 10 + gap, y - 20), new GeoPoint2D(x + 20, y - 25)));
            }
            /// <summary>
            /// Reverses some curves and shuffles all of them, like in a real drawing.
            /// </summary>
            public ICurve2D[] Finish()
            {
                for (int i = 0; i < Curves.Count; i++)
                {
                    if (rnd.Next(3) == 0) Curves[i].Reverse();
                }
                for (int i = Curves.Count - 1; i > 0; i--)
                {
                    int j = rnd.Next(i + 1);
                    (Curves[i], Curves[j]) = (Curves[j], Curves[i]);
                }
                return Curves.ToArray();
            }
            public static ICurve2D[] Random(int seed, bool withOpenCurves)
            {
                SheetGenerator g = new SheetGenerator(seed);
                Random r = g.rnd;
                int parts = r.Next(1, 7);
                for (int i = 0; i < parts; i++)
                {
                    double x = (i % 3) * 300, y = (i / 3) * 300;
                    if (r.Next(4) == 0) g.Perforated(x, y, 200, 150, r.Next(1, 8));
                    else g.Nested(x + 120, y + 120, 220, 0, r.Next(1, 5));
                }
                if (withOpenCurves)
                {
                    if (r.Next(2) == 0) g.PartWithBranches(-200, 0, 60);
                    if (r.Next(2) == 0) g.Gaps(-200, 200, 50, MaxGap / 2);
                    int open = r.Next(0, 6);
                    for (int i = 0; i < open; i++) g.OpenChain(-400 + r.NextDouble() * 100, -300 + r.NextDouble() * 200, r.Next(1, 4));
                }
                return g.Finish();
            }
        }
        #endregion

        #region helpers
        private static ICurve2D[] CloneCurves(ICurve2D[] curves) => curves.Select(c => c.Clone()).ToArray();

        /// <summary>
        /// Runs CurveGraph.CreateCompoundShape like CompoundShape.CreateFromList does, with or without the containment forest.
        /// </summary>
        private static CompoundShape Create(ICurve2D[] curves, bool partInPart, bool useForest, out List<IGeoObject> dead, out bool usedForest)
        {
            CurveGraph graph = new CurveGraph(CloneCurves(curves), MaxGap);
            graph.UseContainmentForest = useForest;
            CompoundShape res = graph.CreateCompoundShape(false, new GeoPoint2D(0.0, 0.0), ConstrHatchInside.HatchMode.hull, partInPart);
            dead = graph.DeadObjects;
            usedForest = graph.UsedContainmentForest;
            return res;
        }

        private static string Describe(Border b)
        {
            BoundingRect e = b.Extent;
            return string.Format(CultureInfo.InvariantCulture, "area {0:R} length {1:R} extent [{2:R}, {3:R}, {4:R}, {5:R}] segments {6} unsplitted {7} ids {8} unsplitted id {9}",
                b.Area, b.Length, e.Left, e.Bottom, e.Right, e.Top, b.Count, b.UnsplittedOutline != null,
                string.Join(",", b.Segments.Select(CurveId)), b.UnsplittedOutline == null ? "-" : CurveId(b.UnsplittedOutline));
        }

        private const string IdKey = "CurveGraphNestingTests.Id";

        /// <summary>
        /// Marks every curve with its index in the UserData. Callers of CompoundShape.CreateFromList carry their own data
        /// on the curves this way and read it from the segments of the resulting borders, so it must arrive there.
        /// </summary>
        private static void TagCurves(ICurve2D[] curves)
        {
            for (int i = 0; i < curves.Length; i++) curves[i].UserData.Add(IdKey, i);
        }

        private static string CurveId(ICurve2D curve)
        {
            return curve.UserData.ContainsData(IdKey) ? curve.UserData.GetData(IdKey).ToString() : "?";
        }

        /// <summary>
        /// One line per outline and hole, in the order of the shape.
        /// </summary>
        private static List<string> Describe(CompoundShape cs)
        {
            List<string> res = new List<string>();
            if (cs == null)
            {
                res.Add("null");
                return res;
            }
            for (int i = 0; i < cs.SimpleShapes.Length; i++)
            {
                SimpleShape ss = cs.SimpleShapes[i];
                res.Add("shape " + i + ": " + Describe(ss.Outline));
                for (int j = 0; j < ss.NumHoles; j++) res.Add("  hole " + j + ": " + Describe(ss.Holes[j]));
            }
            return res;
        }

        private static List<string> Describe(IEnumerable<IGeoObject> dead)
        {
            return dead.Select(go =>
            {
                ICurve c = (ICurve)go;
                return string.Format(CultureInfo.InvariantCulture, "{0} ({1:R}, {2:R}) ({3:R}, {4:R})", go.GetType().Name, c.StartPoint.x, c.StartPoint.y, c.EndPoint.x, c.EndPoint.y);
            }).ToList();
        }

        private static void AssertSameLines(List<string> expected, List<string> actual, string what)
        {
            for (int i = 0; i < Math.Min(expected.Count, actual.Count); i++)
            {
                Assert.AreEqual(expected[i], actual[i], what + ", line " + i);
            }
            Assert.AreEqual(expected.Count, actual.Count, what + ": number of lines");
        }

        /// <summary>
        /// Compares the containment forest with the original pairwise nesting on the same input and returns whether the
        /// containment forest was used (and not its fallback).
        /// </summary>
        private static bool AssertSameAsPairwise(ICurve2D[] curves, bool partInPart, string what)
        {
            TagCurves(curves);
            CompoundShape expected = Create(curves, partInPart, false, out List<IGeoObject> expectedDead, out bool usedForest);
            Assert.IsFalse(usedForest);
            CompoundShape actual = Create(curves, partInPart, true, out List<IGeoObject> actualDead, out usedForest);
            AssertSameLines(Describe(expected), Describe(actual), what);
            Assert.IsFalse(Describe(actual).Any(line => line.Contains("?")), what + ": a segment lost its UserData");
            AssertSameLines(Describe(expectedDead), Describe(actualDead), what + ", dead objects");
            return usedForest;
        }

        private static readonly FieldInfo clusterSetField = typeof(CurveGraph).GetField("clusterSet", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo clusterTreeField = typeof(CurveGraph).GetField("clusterTree", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// The removal of dead ends as it was before the cluster tree traversal was fixed, as the reference.
        /// </summary>
        private static void OriginalRemoveAllDeadEnds(CurveGraph graph)
        {
            HashSet<Cluster> clusterSet = (HashSet<Cluster>)clusterSetField.GetValue(graph);
            QuadTree clusterTree = (QuadTree)clusterTreeField.GetValue(graph);
            ArrayList ClusterToRemove = new ArrayList();
            foreach (Cluster cl in clusterSet)
            {
                if (cl.Joints.Count < 2) ClusterToRemove.Add(cl);
            }
            foreach (Cluster cl in ClusterToRemove)
            {
                Cluster NextCluster = cl;
                while (NextCluster != null && NextCluster.Joints.Count < 2)
                {
                    clusterTree.RemoveObject(NextCluster);
                    clusterSet.Remove(NextCluster);
                    if (NextCluster.Joints.Count > 0)
                    {
                        graph.DeadObjects.Add(NextCluster.Joints[0].curve.MakeGeoObject(Plane.XYPlane));
                        Joint lp = NextCluster.Joints[0];
                        if (lp.StartCluster == NextCluster) NextCluster = lp.EndCluster;
                        else NextCluster = lp.StartCluster;
                        if (NextCluster != null)
                        {
                            for (int i = 0; i < NextCluster.Joints.Count; ++i)
                            {
                                if (NextCluster.Joints[i] == lp)
                                {
                                    NextCluster.Joints.RemoveAt(i);
                                    break;
                                }
                            }
                        }
                    }
                    else break;
                }
            }
        }

        /// <summary>
        /// The clusters of the set and of the tree, each described by its center and number of joints, sorted.
        /// </summary>
        private static (List<string> set, List<string> tree) DescribeClusters(CurveGraph graph)
        {
            HashSet<Cluster> clusterSet = (HashSet<Cluster>)clusterSetField.GetValue(graph);
            QuadTree clusterTree = (QuadTree)clusterTreeField.GetValue(graph);
            HashSet<Cluster> inTree = new HashSet<Cluster>();
            foreach (object o in clusterTree) inTree.Add((Cluster)o);
            static string D(Cluster cl) => string.Format(CultureInfo.InvariantCulture, "({0:R}, {1:R}) {2}", cl.center.x, cl.center.y, cl.Joints.Count);
            List<string> set = clusterSet.Select(D).OrderBy(s => s, StringComparer.Ordinal).ToList();
            List<string> tree = inTree.Select(D).OrderBy(s => s, StringComparer.Ordinal).ToList();
            return (set, tree);
        }

        /// <summary>
        /// Removes the dead ends with the current and with the original implementation and compares the dead objects
        /// (including their order) and the clusters that remain in the set and in the tree.
        /// </summary>
        private static void AssertSameDeadEnds(ICurve2D[] curves, string what)
        {
            CurveGraph original = new CurveGraph(CloneCurves(curves), MaxGap);
            original.InsertGapFillers();
            OriginalRemoveAllDeadEnds(original);

            CurveGraph current = new CurveGraph(CloneCurves(curves), MaxGap);
            current.InsertGapFillers();
            current.RemoveAllDeadEnds();

            AssertSameLines(Describe(original.DeadObjects), Describe(current.DeadObjects), what + ", dead objects");
            var (originalSet, originalTree) = DescribeClusters(original);
            var (currentSet, currentTree) = DescribeClusters(current);
            AssertSameLines(originalSet, currentSet, what + ", cluster set");
            AssertSameLines(originalTree, currentTree, what + ", cluster tree");
            // no cluster without its counterpart: the tree holds only clusters of the set
            HashSet<Cluster> clusterSet = (HashSet<Cluster>)clusterSetField.GetValue(current);
            foreach (object o in (QuadTree)clusterTreeField.GetValue(current))
            {
                Assert.IsTrue(clusterSet.Contains((Cluster)o), what + ": the tree contains a removed cluster");
            }
            // every remaining cluster is found in the tree at its position
            QuadTree tree = (QuadTree)clusterTreeField.GetValue(current);
            foreach (Cluster cl in clusterSet)
            {
                bool found = false;
                foreach (object o in tree.GetObjectsFromRect(cl.GetExtent())) found |= o == cl;
                Assert.IsTrue(found, what + ": a remaining cluster is not found in the tree");
            }
        }
        #endregion

        [TestMethod]
        public void ContainmentForest_GivesTheResultOfPairwiseNesting_RandomDrawings()
        {
            int usedForest = 0, total = 0;
            for (int seed = 0; seed < 120; seed++)
            {
                ICurve2D[] curves = SheetGenerator.Random(seed, seed % 2 == 0);
                foreach (bool partInPart in new[] { true, false })
                {
                    total++;
                    if (AssertSameAsPairwise(curves, partInPart, "seed " + seed + ", partInPart " + partInPart)) usedForest++;
                }
            }
            // these drawings are cleanly nested and have several borders, so the fallback must not be needed
            Assert.AreEqual(total, usedForest, "number of cases that used the containment forest");
        }

        [TestMethod]
        public void ContainmentForest_GivesTheResultOfPairwiseNesting_DeepNesting()
        {
            // contours nested 5 deep, alternating rectangles and circles, three parts side by side
            foreach (bool partInPart in new[] { true, false })
            {
                SheetGenerator g = new SheetGenerator(7);
                for (int p = 0; p < 3; p++)
                {
                    double x = p * 250;
                    g.Rect(x, 0, x + 200, 200);
                    g.Circle(x + 100, 100, 95);
                    g.Rect(x + 40, 40, x + 160, 160);
                    g.Circle(x + 100, 100, 50);
                    g.Rect(x + 75, 75, x + 125, 125);
                    g.Circle(x + 15, 15, 8); // a second hole in the outermost part
                    g.Rect(x + 45, 45, x + 60, 60); // a part next to the circle of depth 3
                }
                ICurve2D[] curves = g.Finish();
                Assert.IsTrue(AssertSameAsPairwise(curves, partInPart, "partInPart " + partInPart));
                CompoundShape cs = Create(curves, partInPart, true, out _, out _);
                // with partInPart: the parts at depth 0, 2 and 4 of each nest; without: only depth 0
                Assert.AreEqual(partInPart ? 9 : 3, cs.SimpleShapes.Length);
                Assert.AreEqual(partInPart ? 12 : 6, cs.SimpleShapes.Sum(s => s.NumHoles));
            }
        }

        [TestMethod]
        public void ContainmentForest_GivesTheResultOfPairwiseNesting_PerforatedSheet()
        {
            foreach (bool partInPart in new[] { true, false })
            {
                SheetGenerator g = new SheetGenerator(11);
                g.Perforated(0, 0, 400, 300, 20);
                g.Perforated(450, 0, 200, 150, 6);
                g.Rect(2, 2, 8, 8); // a hole near the corner of the first part
                ICurve2D[] curves = g.Finish();
                Assert.IsTrue(AssertSameAsPairwise(curves, partInPart, "partInPart " + partInPart));
                CompoundShape cs = Create(curves, partInPart, true, out _, out _);
                Assert.AreEqual(2, cs.SimpleShapes.Length);
                Assert.AreEqual(401, cs.SimpleShapes[0].NumHoles);
                Assert.AreEqual(36, cs.SimpleShapes[1].NumHoles);
            }
        }

        [TestMethod]
        public void ContainmentForest_KeepsUnsplittedOutline()
        {
            // a circular part with a circular hole and a circular part in the hole, all single curves
            foreach (bool partInPart in new[] { true, false })
            {
                ICurve2D[] curves = {
                    new Circle2D(new GeoPoint2D(0, 0), 50), new Circle2D(new GeoPoint2D(0, 0), 30), new Circle2D(new GeoPoint2D(0, 0), 10),
                    new Circle2D(new GeoPoint2D(200, 0), 40), new Circle2D(new GeoPoint2D(210, 0), 5) };
                Assert.IsTrue(AssertSameAsPairwise(curves, partInPart, "partInPart " + partInPart));
                CompoundShape cs = Create(curves, partInPart, true, out _, out _);
                Assert.AreEqual(partInPart ? 3 : 2, cs.SimpleShapes.Length);
                foreach (SimpleShape ss in cs.SimpleShapes)
                {
                    Assert.IsNotNull(ss.Outline.UnsplittedOutline);
                    foreach (Border hole in ss.Holes) Assert.IsNotNull(hole.UnsplittedOutline);
                }
            }
        }

        [TestMethod]
        public void ContainmentForest_FallsBackToPairwiseNesting()
        {
            List<(string name, ICurve2D[] curves)> inputs = new List<(string, ICurve2D[])>();

            SheetGenerator g = new SheetGenerator(1);
            g.Rect(0, 0, 100, 100);
            g.Rect(50, 50, 150, 150); // crosses the first rectangle
            g.Rect(300, 0, 400, 100);
            g.Circle(350, 50, 20);
            inputs.Add(("crossing", g.Finish()));

            g = new SheetGenerator(2);
            g.Rect(0, 0, 100, 100);
            g.Rect(20, 20, 60, 60);
            // a quadrangle inside the second rectangle, one edge on the edge of the rectangle without a common vertex
            g.Curves.Add(new Line2D(new GeoPoint2D(25, 60), new GeoPoint2D(30, 40)));
            g.Curves.Add(new Line2D(new GeoPoint2D(30, 40), new GeoPoint2D(45, 40)));
            g.Curves.Add(new Line2D(new GeoPoint2D(45, 40), new GeoPoint2D(50, 60)));
            g.Curves.Add(new Line2D(new GeoPoint2D(50, 60), new GeoPoint2D(25, 60)));
            inputs.Add(("touching along an edge", g.Finish()));

            g = new SheetGenerator(3);
            g.Rect(0, 0, 100, 100);
            // the same circle twice, split at different points
            g.Curves.Add(new Arc2D(new GeoPoint2D(50, 50), 20, Angle.A0, SweepAngle.Opposite));
            g.Curves.Add(new Arc2D(new GeoPoint2D(50, 50), 20, Angle.A180, SweepAngle.Opposite));
            g.Curves.Add(new Arc2D(new GeoPoint2D(50, 50), 20, Angle.A90, SweepAngle.Opposite));
            g.Curves.Add(new Arc2D(new GeoPoint2D(50, 50), 20, Angle.A270, SweepAngle.Opposite));
            inputs.Add(("identical", g.Finish()));

            g = new SheetGenerator(4);
            g.Rect(0, 0, 100, 100);
            g.Rect(-50, 200, 50, 300);
            // a square whose edge lies on the edge of the first rectangle, outside of it, without common vertices
            g.Rect(20, 100, 40, 120);
            inputs.Add(("touching outside", g.Finish()));

            foreach (var (name, curves) in inputs)
            {
                foreach (bool partInPart in new[] { true, false })
                {
                    Assert.IsFalse(AssertSameAsPairwise(curves, partInPart, name + ", partInPart " + partInPart),
                        name + ", partInPart " + partInPart + ": the input is not cleanly nested, the pairwise nesting must be used");
                }
            }
        }

        [TestMethod]
        public void ContainmentForest_TouchingAtAPoint()
        {
            // a triangle inside a rectangle whose tip touches the edge of the rectangle, without a common vertex.
            // BorderOperation finds the rectangle covering the triangle, so this counts as cleanly nested for both the
            // containment forest and the pairwise nesting.
            SheetGenerator g = new SheetGenerator(2);
            g.Rect(0, 0, 100, 100);
            g.Rect(20, 20, 60, 60);
            g.Curves.Add(new Line2D(new GeoPoint2D(30, 30), new GeoPoint2D(50, 30)));
            g.Curves.Add(new Line2D(new GeoPoint2D(50, 30), new GeoPoint2D(35, 60)));
            g.Curves.Add(new Line2D(new GeoPoint2D(35, 60), new GeoPoint2D(30, 30)));
            ICurve2D[] curves = g.Finish();
            foreach (bool partInPart in new[] { true, false })
            {
                AssertSameAsPairwise(curves, partInPart, "partInPart " + partInPart);
            }
        }

        [TestMethod]
        public void RemoveDeadEnds_GivesTheResultOfTheOriginalCode()
        {
            for (int seed = 0; seed < 60; seed++)
            {
                AssertSameDeadEnds(SheetGenerator.Random(seed, true), "seed " + seed);
            }

            SheetGenerator g = new SheetGenerator(5);
            for (int i = 0; i < 300; i++) g.OpenChain(i % 20 * 12, i / 20 * 15, 1 + i % 4); // isolated lines and open chains
            for (int i = 0; i < 5; i++) g.PartWithBranches(300 + i * 80, 0, 60); // branches
            for (int i = 0; i < 5; i++) g.Gaps(300 + i * 80, 200, 50, MaxGap / 2); // ends closer than maxGap
            for (int i = 0; i < 5; i++) g.Gaps(300 + i * 80, 400, 50, MaxGap * 2); // ends further apart than maxGap
            ICurve2D[] curves = g.Finish();
            AssertSameDeadEnds(curves, "open curves");
            foreach (bool partInPart in new[] { true, false })
            {
                AssertSameAsPairwise(curves, partInPart, "open curves, partInPart " + partInPart);
            }
        }

        [TestMethod]
        public void RemoveDeadEnds_ExpectedDeadObjects()
        {
            SheetGenerator g = new SheetGenerator(6);
            g.PartWithBranches(0, 0, 60); // 4 dead lines: the stub, the chain of 2 and the isolated line
            // ends closer than maxGap fall into the same cluster: the square is closed, the 2 lines below form an open chain
            g.Gaps(100, 0, 50, MaxGap / 2);
            g.Gaps(200, 0, 50, MaxGap * 2); // not closed: the 4 lines of the square and the 2 lines below are dead
            g.OpenChain(-100, -100, 3);
            ICurve2D[] curves = g.Finish();
            CompoundShape cs = CompoundShape.CreateFromList(CloneCurves(curves), MaxGap, true, out GeoObjectList dead);
            Assert.AreEqual(4 + 2 + 6 + 3, dead.Count);
            Assert.AreEqual(2, cs.SimpleShapes.Length);
        }

        [TestMethod]
        public void CreateFromList_LargeSheetFinishes()
        {
            // 300 parts with 9 holes each, a part in a hole of every tenth part and 5000 open lines: quadratic before
            SheetGenerator g = new SheetGenerator(8);
            for (int i = 0; i < 300; i++)
            {
                double x = (i % 20) * 110, y = (i / 20) * 90;
                g.Perforated(x, y, 100, 80, 3);
                if (i % 10 == 0) g.Circle(x + 50, y + 40, 1); // a small part inside the middle hole
            }
            Random r = new Random(9);
            for (int i = 0; i < 5000; i++)
            {
                double x = r.NextDouble() * 2200, y = -50 - r.NextDouble() * 500;
                g.Curves.Add(new Line2D(new GeoPoint2D(x, y), new GeoPoint2D(x + 5, y + 3)));
            }
            ICurve2D[] curves = g.Finish();
            CompoundShape cs = CompoundShape.CreateFromList(curves, MaxGap, true, out GeoObjectList dead);
            Assert.AreEqual(300 + 30, cs.SimpleShapes.Length);
            Assert.AreEqual(300 * 9, cs.SimpleShapes.Sum(s => s.NumHoles));
            // all open lines, and the lines that close gaps between them
            Assert.IsTrue(dead.Count >= 5000, "dead objects: " + dead.Count);
        }
    }
}
