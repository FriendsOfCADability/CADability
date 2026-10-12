using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// <see cref="Dimension.Modify"/> moved the measured points, the dimension line and the
    /// plane, but not the direction of the dimension line. A linear dimension that was rotated
    /// went on measuring along its old direction: turned by 90°, a dimension over 100 read 0.
    /// </summary>
    [TestClass]
    public class DimensionModifyTests
    {
        [TestMethod]
        public void RotatedLinearDimensionKeepsItsMeasurement()
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
            dim.GetBoundingCube(); // draws the dimension, which establishes the plane it measures in
            Assert.AreEqual("100", dim.GetDimText(0));

            dim.Modify(ModOp.Rotate(GeoPoint.Origin, GeoVector.ZAxis, new SweepAngle(Math.PI / 2)));
            Assert.AreEqual(0.0, (dim.DimLineDirection.Normalized - GeoVector.YAxis).Length, 1e-12,
                "the dimension line turns with the dimension");
            dim.GetBoundingCube();
            Assert.AreEqual("100", dim.GetDimText(0), "the rotated dimension measures the same distance");

            ModOp tilt = ModOp.Rotate(new GeoPoint(5, 5, 5), new GeoVector(1, 1, 0.3), new SweepAngle(0.65));
            GeoVector expected = tilt * dim.DimLineDirection;
            dim.Modify(tilt);
            Assert.AreEqual(0.0, (dim.DimLineDirection - expected).Length, 1e-12);
            Assert.AreEqual(0.0, dim.DimLineDirection * dim.Normal, 1e-12, "it stays in the plane of the dimension");
            dim.GetBoundingCube();
            Assert.AreEqual("100", dim.GetDimText(0));
        }
    }
}
