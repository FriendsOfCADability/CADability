using CADability.Attribute;

namespace CADability.Tests
{
    /// <summary>
    /// Issue #428: <see cref="DimensionStyle.Clone"/> did not copy the angle text mode and the user data, and the setter
    /// of <see cref="DimensionStyle.DimBreakDimLine"/> reported its change under the name "DimBreakDimLine " (trailing
    /// space), so the undo could not find the property.
    /// </summary>
    [TestClass]
    public class DimensionStyleCloneTests
    {
        [TestMethod]
        public void CloneCopiesAngleTextAndUserData()
        {
            DimensionStyle style = new DimensionStyle();
            style.AngleText = DimensionStyle.EAngleText.Radian;
            style.UserData.Add("key", "value");

            DimensionStyle clone = style.Clone();
            Assert.AreEqual(DimensionStyle.EAngleText.Radian, clone.AngleText);
            Assert.IsTrue(clone.UserData.ContainsData("key"));
            Assert.AreEqual("value", clone.UserData["key"]);
            Assert.AreNotSame(style.UserData, clone.UserData, "the user data is cloned, not shared");
        }

        [TestMethod]
        public void DimBreakDimLineReportsItsPropertyName()
        {
            DimensionStyle style = new DimensionStyle();
            string reported = null;
            style.PropertyChangedEvent += (sender, propertyName, newValue, oldValue) => reported = propertyName;
            style.DimBreakDimLine = true;
            Assert.AreEqual("DimBreakDimLine", reported);
            Assert.IsNotNull(typeof(DimensionStyle).GetProperty(reported), "the reported name must be a property, the undo finds it by reflection");

            // the undo step that FireDidChange creates for a list-owned style sets the property back by that name
            ReversibleChange undo = new ReversibleChange(style, reported, false);
            Assert.IsTrue(undo.Undo());
            Assert.IsFalse(style.DimBreakDimLine);
        }
    }
}
