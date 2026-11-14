namespace CADability.UserInterface
{
    /// <summary>
    /// Helpers for the tree of entries shown in a property page. Entries that used to react to focus changes via
    /// <see cref="IPropertyTreeView.FocusChangedEvent"/> listen to <see cref="IPropertyPage.OnSelectionChanged"/> now and use
    /// these helpers to find out whether the selection moved into or out of them.
    /// </summary>
    internal static class PropertyEntryTree
    {
        /// <summary>
        /// True if <paramref name="entry"/> is <paramref name="root"/> or lies below it, following <see cref="IPropertyEntry.Parent"/>.
        /// False if either of them is null.
        /// </summary>
        /// <param name="entry">the entry to test, typically the entry that was selected or unselected</param>
        /// <param name="root">the entry whose subtree is tested</param>
        public static bool IsWithin(IPropertyEntry entry, IPropertyEntry root)
        {
            if (root == null) return false;
            for (IPropertyEntry e = entry; e != null; e = e.Parent as IPropertyEntry)
            {
                if (e == root) return true;
            }
            return false;
        }
    }
}
