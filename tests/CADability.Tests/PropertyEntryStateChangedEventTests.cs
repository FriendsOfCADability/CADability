using CADability.UserInterface;

namespace CADability.Tests
{
    /// <summary>
    /// Issue #432: the remove accessor of <see cref="PropertyEntryImpl.StateChangedEvent"/> created a new forwarding
    /// delegate instead of removing the one the add accessor had registered, so a handler could never be unsubscribed.
    /// </summary>
    [TestClass]
    public class PropertyEntryStateChangedEventTests
    {
        private class Entry : PropertyEntryImpl
        {
            public override PropertyEntryType Flags => default;
            public void Raise() => PropertyEntryChangedState(new StateChangedArgs(StateChangedArgs.State.Selected));
        }

        [TestMethod]
        public void RemovedHandlerIsNotCalledAnyMore()
        {
            Entry entry = new Entry();
            int calls = 0;
            StateChangedDelegate handler = (sender, args) => calls++;

            entry.StateChangedEvent += handler;
            entry.Raise();
            Assert.AreEqual(1, calls);

            entry.StateChangedEvent -= handler;
            entry.Raise();
            Assert.AreEqual(1, calls, "the handler was removed");
        }

        [TestMethod]
        public void HandlerAddedTwiceIsCalledTwiceUntilRemovedTwice()
        {
            Entry entry = new Entry();
            int calls = 0;
            StateChangedDelegate handler = (sender, args) => calls++;

            entry.StateChangedEvent += handler;
            entry.StateChangedEvent += handler;
            entry.Raise();
            Assert.AreEqual(2, calls);

            entry.StateChangedEvent -= handler;
            entry.Raise();
            Assert.AreEqual(3, calls, "one subscription is left");

            entry.StateChangedEvent -= handler;
            entry.StateChangedEvent -= handler; // removing more often than added is harmless
            entry.Raise();
            Assert.AreEqual(3, calls);
        }

        [TestMethod]
        public void SenderIsTheEntry()
        {
            Entry entry = new Entry();
            IShowProperty sender = null;
            entry.StateChangedEvent += (s, args) => sender = s;
            entry.Raise();
            Assert.AreSame(entry, sender);
        }
    }
}
