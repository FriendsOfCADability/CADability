namespace CADability.UserInterface
{
    /// <summary>
    /// A separator entry for the property page (tree view): it is displayed as a separator line and has neither a
    /// label, nor a value, nor sub entries.
    /// </summary>

    public class SeperatorProperty : IShowPropertyImpl
    {
        /// <summary>
        /// Creates the separator.
        /// </summary>
        /// <param name="resourceId">Resource id identifying the entry; a separator normally displays no text</param>
        public SeperatorProperty(string resourceId)
        {
            base.resourceIdInternal = resourceId;
        }
        #region IShowProperty Members
        /// <inheritdoc/>
        public override ShowPropertyLabelFlags LabelType
        {
            get
            {
                return (ShowPropertyLabelFlags)0;
            }
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.EntryType"/>, 
        /// returns <see cref="ShowPropertyEntryType.Seperator"/>.
        /// </summary>
        public override ShowPropertyEntryType EntryType
        {
            get
            {
                return ShowPropertyEntryType.Seperator;
            }
        }
        #endregion
    }
}
