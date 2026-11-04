using CADability.Attribute;
using CADability.GeoObject;
using CADability.Shapes;

namespace CADability.Tests
{
    /// <summary>
    /// Issue #518: <see cref="GeoObjectList.DecomposeAll"/> threw an <see cref="ArgumentOutOfRangeException"/> for an empty
    /// <see cref="Block"/> or <see cref="BlockRef"/> at the end of the list. After removing an object it went on testing
    /// <c>this[i]</c>, which then was the next object or did not exist at all. A <see cref="Hatch"/> was decomposed without
    /// calculating its hatch lines, so a new hatch vanished from the list or caused the same exception, and a block reference
    /// inside a block was decomposed only by chance, by the repeated test of the next object.
    /// <see cref="GeoObjectList.DecomposeBlocks(bool)"/> had the same mistake.
    /// </summary>
    [TestClass]
    public class DecomposeAllTests
    {
        private static Line LineAt(double y) => Line.TwoPoints(new GeoPoint(0, y, 0), new GeoPoint(10, y, 0));

        /// <summary>a new hatch of the square 10 x 10 with lines 1 apart, whose hatch lines are not calculated yet</summary>
        private static Hatch NewHatch()
        {
            Hatch hatch = Hatch.Construct();
            hatch.CompoundShape = new CompoundShape(new SimpleShape(Border.MakeRectangle(0, 10, 0, 10)));
            hatch.Plane = Plane.XYPlane;
            HatchStyleLines style = new HatchStyleLines();
            style.LineDistance = 1;
            style.LineAngle = new Angle(Math.PI / 4);
            hatch.HatchStyle = style;
            return hatch;
        }

        /// <summary>a block with a line at y = 5 and a reference to a block with a line at y = 7, in the given order</summary>
        private static Block BlockWithReference(bool referenceFirst)
        {
            Block inner = Block.Construct();
            inner.Add(LineAt(7));
            Block outer = Block.Construct();
            if (referenceFirst) outer.Add(BlockRef.Construct(inner));
            outer.Add(LineAt(5));
            if (!referenceFirst) outer.Add(BlockRef.Construct(inner));
            return outer;
        }

        private static void AssertOnlyLinesAt(GeoObjectList list, params double[] ys)
        {
            Assert.IsTrue(list.All(g => g is Line), "not decomposed: " + string.Join(", ", list.Select(g => g.GetType().Name)));
            CollectionAssert.AreEquivalent(ys, list.Cast<Line>().Select(l => l.StartPoint.y).ToArray());
        }

        [TestMethod]
        public void an_empty_block_at_the_end_is_removed()
        {
            GeoObjectList list = new GeoObjectList(LineAt(0), Block.Construct());
            list.DecomposeAll();
            AssertOnlyLinesAt(list, 0);
        }

        [TestMethod]
        public void empty_blocks_anywhere_are_removed()
        {
            GeoObjectList list = new GeoObjectList(Block.Construct(), LineAt(0), Block.Construct(), LineAt(1), Block.Construct());
            list.DecomposeAll();
            AssertOnlyLinesAt(list, 0, 1);
        }

        [TestMethod]
        public void a_reference_to_an_empty_block_at_the_end_is_removed()
        {
            GeoObjectList list = new GeoObjectList(LineAt(0), BlockRef.Construct(Block.Construct()));
            list.DecomposeAll();
            AssertOnlyLinesAt(list, 0);
        }

        [DataTestMethod]
        [DataRow(true, DisplayName = "hatch at the end")]
        [DataRow(false, DisplayName = "hatch at the start")]
        public void a_new_hatch_is_decomposed_into_its_lines(bool hatchLast)
        {
            GeoObjectList list = hatchLast ? new GeoObjectList(LineAt(20), NewHatch()) : new GeoObjectList(NewHatch(), LineAt(20));
            list.DecomposeAll();
            Assert.IsTrue(list.All(g => g is Line), "not decomposed: " + string.Join(", ", list.Select(g => g.GetType().Name)));
            List<Line> hatchLines = list.Cast<Line>().Where(l => l.StartPoint.y < 15).ToList();
            Assert.IsTrue(hatchLines.Count > 10, $"the hatch lines are missing, {hatchLines.Count} found");
            foreach (Line l in hatchLines)
            {
                Assert.AreEqual(Math.PI / 4, Math.Atan2(Math.Abs(l.EndPoint.y - l.StartPoint.y), Math.Abs(l.EndPoint.x - l.StartPoint.x)), 1e-9);
            }
            Assert.AreEqual(1, list.Count - hatchLines.Count, "the line at y = 20 is kept");
        }

        [DataTestMethod]
        [DataRow(true, DisplayName = "reference first")]
        [DataRow(false, DisplayName = "reference last")]
        public void a_reference_inside_a_block_is_decomposed(bool referenceFirst)
        {
            GeoObjectList list = new GeoObjectList(LineAt(0), BlockWithReference(referenceFirst));
            list.DecomposeAll();
            AssertOnlyLinesAt(list, 0, 5, 7);
        }

        [TestMethod]
        public void paths_and_polylines_are_decomposed_into_their_segments()
        {
            Polyline polyline = Polyline.Construct();
            polyline.SetPoints(new[] { new GeoPoint(0, 1, 0), new GeoPoint(10, 1, 0), new GeoPoint(10, 2, 0) }, false);
            GeoObject.Path path = GeoObject.Path.Construct();
            Assert.IsTrue(path.Set(new ICurve[] { LineAt(3), Line.TwoPoints(new GeoPoint(10, 3, 0), new GeoPoint(10, 4, 0)) }));
            Block block = Block.Construct();
            block.Add(polyline);
            GeoObjectList list = new GeoObjectList(path, block);
            list.DecomposeAll();
            AssertOnlyLinesAt(list, 1, 1, 3, 3);
        }

        [TestMethod]
        public void a_dimension_is_decomposed_without_blocks_paths_or_polylines()
        {
            Project project = Project.CreateSimpleProject();
            Dimension dim = Dimension.Construct();
            dim.DimType = Dimension.EDimType.DimPoints;
            dim.DimensionStyle = project.DimensionStyleList.Current;
            dim.Normal = GeoVector.ZAxis;
            dim.DimLineRef = new GeoPoint(0, 20, 0);
            dim.DimLineDirection = GeoVector.XAxis;
            dim.AddPoint(new GeoPoint(0, 0, 0));
            dim.AddPoint(new GeoPoint(100, 0, 0));
            GeoObjectList list = dim.Decompose();
            list.DecomposeAll();
            Assert.IsTrue(list.Count > 0);
            Assert.IsFalse(list.Any(g => g is Block || g is BlockRef || g is GeoObject.Path || g is Polyline),
                "not decomposed: " + string.Join(", ", list.Select(g => g.GetType().Name)));
        }

        [TestMethod]
        public void decompose_blocks_removes_an_empty_block_at_the_end_and_decomposes_references_in_blocks()
        {
            GeoObjectList list = new GeoObjectList(LineAt(0), BlockWithReference(false), Block.Construct());
            list.DecomposeBlocks(true);
            AssertOnlyLinesAt(list, 0, 5, 7);
        }

        [TestMethod]
        public void decompose_blocks_keeps_hatches_when_asked_to()
        {
            GeoObjectList list = new GeoObjectList(LineAt(20), NewHatch(), Block.Construct());
            list.DecomposeBlocks(false);
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual(1, list.Count(g => g is Hatch));
        }
    }
}
