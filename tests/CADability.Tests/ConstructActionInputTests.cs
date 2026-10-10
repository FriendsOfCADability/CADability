using System.Reflection;
using CADability.Actions;
using CADability.UserInterface;

namespace CADability.Tests
{
    /// <summary>
    /// Issue #253: selecting a <see cref="ConstructAction.MultipleChoiceInput"/> or <see cref="ConstructAction.BooleanInput"/>
    /// did not make it the current input, so mouse moves in the view kept going to the input that was current before.
    /// With <c>CapturesMouse</c> an action can ask for the behaviour of the other inputs; the default stays as it was, because
    /// many actions rely on it (e.g. moving objects while the "copy" option is selected).
    /// </summary>
    [TestClass]
    public class ConstructActionInputTests
    {
        private class TestAction : ConstructAction
        {
            public override string GetID() => "ConstructActionInputTests";
            public int CurrentIndex => (int)typeof(ConstructAction).GetField("currentInputIndex", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(this);
        }

        private static readonly Type inputObjectInterface = typeof(ConstructAction).GetNestedType("IInputObject", BindingFlags.NonPublic);

        /// <summary>Does what <see cref="ConstructAction.OnSetAction"/> does for each input: Init and BuildShowProperty.</summary>
        private static IPropertyEntry Build(ConstructAction action, object input)
        {
            inputObjectInterface.GetMethod("Init").Invoke(input, new object[] { action });
            return (IPropertyEntry)inputObjectInterface.GetMethod("BuildShowProperty").Invoke(input, null);
        }

        private static (TestAction action, IPropertyEntry choice, IPropertyEntry boolean) Setup(bool capturesMouse)
        {
            TestAction action = new TestAction();
            // stands for the input that receives the mouse (a DoubleInput or GeoPointInput would need a frame here)
            ConstructAction.BooleanInput first = new ConstructAction.BooleanInput("Test.First", "YesNo.Values");
            ConstructAction.MultipleChoiceInput mode = new ConstructAction.MultipleChoiceInput("Test.Mode", new string[] { "a", "b" }, 0);
            ConstructAction.BooleanInput copy = new ConstructAction.BooleanInput("Test.Copy", "YesNo.Values");
            mode.CapturesMouse = capturesMouse;
            copy.CapturesMouse = capturesMouse;
            action.SetInput(first, mode, copy);
            Build(action, first);
            IPropertyEntry choice = Build(action, mode);
            IPropertyEntry boolean = Build(action, copy);
            return (action, choice, boolean);
        }

        [TestMethod]
        public void selecting_a_choice_keeps_the_current_input_by_default()
        {
            (TestAction action, IPropertyEntry choice, IPropertyEntry boolean) = Setup(false);
            int before = action.CurrentIndex;
            choice.Selected(null);
            Assert.AreEqual(before, action.CurrentIndex, "MultipleChoiceInput");
            boolean.Selected(null);
            Assert.AreEqual(before, action.CurrentIndex, "BooleanInput");
        }

        [TestMethod]
        public void selecting_a_choice_that_captures_the_mouse_makes_it_the_current_input()
        {
            (TestAction action, IPropertyEntry choice, IPropertyEntry boolean) = Setup(true);
            choice.Selected(null);
            Assert.AreEqual(1, action.CurrentIndex, "MultipleChoiceInput");
            boolean.Selected(null);
            Assert.AreEqual(2, action.CurrentIndex, "BooleanInput");
        }

        /// <summary>
        /// Issue #254: the choices of a MultipleChoiceInput can be replaced while the action runs. The selected entry is kept
        /// if it is still offered, otherwise the first choice is selected and reported through SetChoiceEvent.
        /// </summary>
        [TestMethod]
        public void the_choices_can_be_replaced_while_the_action_runs()
        {
            TestAction action = new TestAction();
            ConstructAction.MultipleChoiceInput mode = new ConstructAction.MultipleChoiceInput("Test.Mode", new string[] { "a", "b", "c" }, 1);
            action.SetInput(mode);
            MultipleChoiceProperty property = (MultipleChoiceProperty)Build(action, mode);
            List<int> reported = new List<int>();
            mode.SetChoiceEvent += val => reported.Add(val);

            mode.SetChoices(new string[] { "b", "c" }); // "b" is still offered
            Assert.AreEqual(0, mode.Choice, "the index of b in the new choices");
            CollectionAssert.AreEqual(new string[] { "b", "c" }, property.Choices);
            Assert.AreEqual("b", property.Value);
            Assert.AreEqual(0, reported.Count, "the selection did not change");

            mode.SetChoices(new string[] { "x", "y" }); // "b" is gone
            Assert.AreEqual(0, mode.Choice);
            Assert.AreEqual("x", property.Value);
            CollectionAssert.AreEqual(new int[] { 0 }, reported, "the new selection is reported");
        }

        [TestMethod]
        public void the_choices_can_be_replaced_before_the_property_is_built()
        {
            TestAction action = new TestAction();
            ConstructAction.MultipleChoiceInput mode = new ConstructAction.MultipleChoiceInput("Test.Mode", new string[] { "a", "b", "c" }, 2);
            action.SetInput(mode);
            mode.SetChoices(new string[] { "c", "d" });
            MultipleChoiceProperty property = (MultipleChoiceProperty)Build(action, mode);
            CollectionAssert.AreEqual(new string[] { "c", "d" }, property.Choices);
            Assert.AreEqual("c", property.Value);
            Assert.ThrowsException<ArgumentException>(() => mode.SetChoices(new string[0]));
        }
    }
}
