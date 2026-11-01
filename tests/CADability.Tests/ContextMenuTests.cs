using CADability.GeoObject;
using CADability.UserInterface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CADability.Tests
{
    /// <summary>
    /// The context menu of a selected object: <see cref="CADability.Actions.SelectObjectsAction"/> loads the menu whose id the object's
    /// <see cref="IGeoObjectShowProperty"/> returns. Pictures, icons and unscaled objects have no menu of their own and
    /// return null; a right click on a selected picture threw a NullReferenceException in
    /// <see cref="MenuResource.LoadMenuDefinition"/>.
    /// </summary>
    [TestClass]
    public class ContextMenuTests
    {
        [TestMethod]
        public void AMissingMenuIsEmpty()
        {
            Assert.AreEqual(0, MenuResource.LoadMenuDefinition(null, false, null).Length);
            Assert.AreEqual(0, MenuResource.LoadMenuDefinition("MenuId.DoesNotExist", false, null).Length);
            Assert.AreEqual(0, MenuResource.LoadMenuDefinition("No Such Main Menu", true, null).Length);
        }

        [TestMethod]
        public void AnExistingMenuIsLoaded()
        {
            // the context menu of the path of a picture
            MenuWithHandler[] menu = MenuResource.LoadMenuDefinition("MenuId.Picture.Path", false, null);
            CollectionAssert.AreEqual(new[] { "MenuId.Picture.Path.Reload", "MenuId.Picture.Path.Open" }, menu.Select(m => m.ID).ToArray());
        }

        [TestMethod]
        [DataRow(typeof(ShowPropertyPicture))]
        [DataRow(typeof(ShowPropertyUnscaledIcon))]
        [DataRow(typeof(ShowPropertyUnscaledGeoObject))]
        public void TheContextMenuOfObjectsWithoutAMenuOfTheirOwnCanBeLoaded(Type showPropertyType)
        {
            // what SelectObjectsAction.OnMouseUp does for a single selected object; the id does not depend on the state
            // of the property, so an uninitialized one (without a frame) is enough
            IGeoObjectShowProperty showProperty = (IGeoObjectShowProperty)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(showPropertyType);
            MenuWithHandler[] menu = MenuResource.LoadMenuDefinition(showProperty.GetContextMenuId(), false, showProperty as ICommandHandler);
            Assert.AreEqual(0, menu.Length);
        }
    }
}
