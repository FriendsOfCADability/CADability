namespace CADability
{
    /// <summary>
    /// Thrown when the name of an object (e.g. a layer or a colour) is changed and the list that holds the object (e.g. the
    /// <see cref="CADability.Attribute.LayerList"/>), in which names must be unique, already contains an object with that
    /// name. The user interface catches this exception when a name is edited and restores the old name.
    /// </summary>

    public class NameAlreadyExistsException : System.Exception
    {
        /// <summary>
        /// The new name that was attempted to be set
        /// </summary>
        public string NewName;
        /// <summary>
        /// The old name, which was valid
        /// </summary>
        public string OldName;
        /// <summary>
        /// The list that contains the object and that already contains another object with the new name
        /// </summary>
        public object ContainingList;
        /// <summary>
        /// The object (e.g. a layer) whose renaming caused the conflict
        /// </summary>
        public object OffendingObject;
        public NameAlreadyExistsException(object ContainingList, object OffendingObject, string NewName, string OldName) : this(ContainingList, OffendingObject, NewName)
        {
            this.OldName = OldName;
        }
        public NameAlreadyExistsException(object ContainingList, object OffendingObject, string NewName) : base("Name '" + NewName + "' already exists in List")
        {
            this.NewName = NewName;
            this.ContainingList = ContainingList;
            this.OffendingObject = OffendingObject;
        }
    }
}
