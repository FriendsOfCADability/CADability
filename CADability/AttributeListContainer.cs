using CADability.GeoObject;

namespace CADability.Attribute
{
    /// <summary>
    /// ApplicationException class raised by various operations with attributes.
    /// </summary>

    public class AttributeException : System.ApplicationException
    {
        /// <summary>
        /// Type of exception
        /// </summary>
        public enum AttributeExceptionType
        {
            /// <summary>
            /// Unspecific
            /// </summary>
            General,
            /// <summary>
            /// Invalid Argument
            /// </summary>
            InvalidArg
        };
        /// <summary>
        /// Type of exception
        /// </summary>
        public AttributeExceptionType ExceptionType;
        internal AttributeException(string message, AttributeExceptionType tp) : base(message)
        {
            ExceptionType = tp;
        }
    }
    /// <summary>
    /// Interface implemented by <see cref="Project"/> and <see cref="Settings"/>.
    /// Helps to get all kind of attribute lists, e.g. <see cref="ColorList"/>, <see cref="LayerList"/>
    /// </summary>

    public interface IAttributeListContainer
    {
        /// <summary>
        /// The list of colours of this container (key "ColorList").
        /// </summary>
        ColorList ColorList { get; }
        /// <summary>
        /// The list of layers of this container (key "LayerList").
        /// </summary>
        LayerList LayerList { get; }
        /// <summary>
        /// The list of hatch styles of this container (key "HatchStyleList").
        /// </summary>
        HatchStyleList HatchStyleList { get; }
        /// <summary>
        /// The list of dimension styles of this container (key "DimensionStyleList").
        /// </summary>
        DimensionStyleList DimensionStyleList { get; }
        /// <summary>
        /// The list of line widths of this container (key "LineWidthList").
        /// </summary>
        LineWidthList LineWidthList { get; }
        /// <summary>
        /// The list of line patterns of this container (key "LinePatternList").
        /// </summary>
        LinePatternList LinePatternList { get; }
        /// <summary>
        /// The list of styles of this container (key "StyleList").
        /// </summary>
        StyleList StyleList { get; }
        /// <summary>
        /// Returns the attribute list registered under the given key. The standard lists use the keys "ColorList",
        /// "LayerList", "HatchStyleList", "DimensionStyleList", "LineWidthList", "LinePatternList" and "StyleList";
        /// applications may add lists under their own keys with <see cref="Add(string, IAttributeList)"/>.
        /// </summary>
        /// <param name="KeyName">the key of the list</param>
        /// <returns>the list, or null when there is no list with that key</returns>
        IAttributeList GetList(string KeyName);
        /// <summary>
        /// Returns the attribute list at the given position, to enumerate all lists together with
        /// <see cref="ListCount"/> and <see cref="ListKeyName(int)"/>.
        /// </summary>
        /// <param name="KeyIndex">index in the range 0 to <see cref="ListCount"/>-1</param>
        /// <returns>the list, or null when the index is out of range</returns>
        IAttributeList List(int KeyIndex);
        /// <summary>
        /// Returns the key under which the list at the given position is registered.
        /// </summary>
        /// <param name="KeyIndex">index in the range 0 to <see cref="ListCount"/>-1</param>
        /// <returns>the key, or null when the index is out of range</returns>
        string ListKeyName(int KeyIndex);
        /// <summary>
        /// The number of attribute lists in this container.
        /// </summary>
        int ListCount { get; }
        /// <summary>
        /// Registers an attribute list under the given key and makes this container its <see cref="IAttributeList.Owner"/>.
        /// Throws an <see cref="AttributeException"/> when the key is already in use.
        /// </summary>
        /// <param name="KeyName">the key, unique within the container</param>
        /// <param name="ToAdd">the list to add; null is ignored</param>
        void Add(string KeyName, IAttributeList ToAdd);
        /// <summary>
        /// Removes the attribute list with the given key. The <see cref="Project"/> throws an
        /// <see cref="AttributeException"/> when one of the standard lists is to be removed.
        /// </summary>
        /// <param name="KeyName">the key of the list to remove</param>
        void Remove(string KeyName);
        /// <summary>
        /// Called by an attribute list when a property of one of its attributes was changed (e.g. the RGB value of a
        /// colour, the visibility of a layer). The project refreshes the objects that use the attribute, the settings
        /// mark themselves as modified. Not meant to be called by applications.
        /// </summary>
        /// <param name="list">the list containing the attribute</param>
        /// <param name="attribute">the attribute that was changed</param>
        /// <param name="change">describes the change and can undo it</param>
        void AttributeChanged(IAttributeList list, INamedAttribute attribute, ReversibleChange change);
        /// <summary>
        /// Called by an attribute list before it removes an attribute, to give the container the chance to veto. The
        /// project and the settings refuse to remove the last entry of a list; the project additionally lets the user
        /// decide when the attribute is still used by objects of a model.
        /// </summary>
        /// <param name="list">the list that wants to remove the attribute</param>
        /// <param name="attribute">the attribute to be removed</param>
        /// <param name="resourceId">prefix of the string table entries for the messages shown to the user, e.g. "LayerList"</param>
        /// <returns>true when the attribute may be removed, false to keep it</returns>
        bool RemovingItem(IAttributeList list, INamedAttribute attribute, string resourceId);
        /// <summary>
        /// Adds all attributes used by the objects in the models of the container to the given list, provided they
        /// are of the type the list holds. The settings, which have no models, do nothing.
        /// </summary>
        /// <param name="list">the list to complete</param>
        void UpdateList(IAttributeList list);
    }


    /// <summary>
    /// static functions to manage IAttributeListContainer and IAttributeList objects
    /// </summary>

    public class AttributeListContainer
    {
        /// <summary>
        /// There is no need to create an instance: the class only has static methods.
        /// </summary>
        public AttributeListContainer()
        {
            // 
            // TODO: Add constructor logic here
            //
        }
        /// <summary>
        /// Gives the container <paramref name="To"/> a copy of the attribute list with the given name. The list is cloned
        /// from <paramref name="From"/> when that container has one; otherwise a new empty list of the standard type
        /// belonging to the name ("ColorList", "LayerList", "HatchStyleList", "DimensionStyleList", "LineWidthList",
        /// "LinePatternList" or "StyleList") is created. A new <see cref="Project"/> gets its lists this way from the
        /// global <see cref="Settings"/>.
        /// </summary>
        /// <param name="From">the container to copy the list from; may be null</param>
        /// <param name="To">the container that receives the list; must not already have a list with that name</param>
        /// <param name="ListName">the key of the list</param>
        /// <param name="Initialize">true: fill a newly created list with its default entries (<see cref="IAttributeList.Initialize"/>)</param>
        /// <returns>true when a list was added, false when <paramref name="From"/> has no such list and the name is not one of the standard lists</returns>
        static public bool CloneAttributeList(IAttributeListContainer From, IAttributeListContainer To, string ListName, bool Initialize)
        {
            IAttributeList toAdd = null;
            if (From != null)
            {
                toAdd = From.GetList(ListName);
                if (toAdd != null)
                {
                    toAdd = toAdd.Clone();
                }
            }
            if (toAdd == null)
            {
                switch (ListName)
                {
                    case "ColorList":
                        toAdd = new ColorList();
                        break;
                    case "LayerList":
                        toAdd = new LayerList();
                        break;
                    case "HatchStyleList":
                        toAdd = new HatchStyleList();
                        break;
                    case "DimensionStyleList":
                        toAdd = new DimensionStyleList();
                        break;
                    case "LineWidthList":
                        toAdd = new LineWidthList();
                        break;
                    case "LinePatternList":
                        toAdd = new LinePatternList();
                        break;
                    case "StyleList":
                        toAdd = new StyleList();
                        break;
                    default:
                        return false;
                }
                if (Initialize)
                    toAdd.Initialize();
            }
            To.Add(ListName, toAdd);
            return true;
        }

        /// <summary>
        /// Makes the cross references between the lists of a container consistent: every attribute that refers to
        /// another attribute (e.g. a <see cref="Style"/> to a <see cref="Layer"/>, a <see cref="HatchStyleLines"/> to a
        /// <see cref="ColorDef"/>) is made to refer to the entry with the same name in the corresponding list of the
        /// container (see <see cref="IAttributeList.Update(bool)"/>). Called after a project has been loaded or created.
        /// </summary>
        /// <param name="container">the container whose lists are updated; null is ignored</param>
        /// <param name="AddMissingToList">true: a referenced attribute that is not found in the list is added to it, false: the reference is left as it is</param>
        static public void UpdateLists(IAttributeListContainer container, bool AddMissingToList)
        {
            if (container == null) return;
            for (int i = 0; i < container.ListCount; i++)
                container.List(i).Update(AddMissingToList); ;
        }

        /// <summary>
        /// Adds the entries of the list named <paramref name="ListName"/> of <paramref name="Included"/> to the list
        /// with the same name of <paramref name="Including"/>, skipping the names that already exist there. When
        /// something was added, the lists of <paramref name="Including"/> are made consistent with
        /// <see cref="UpdateLists(IAttributeListContainer, bool)"/>.
        /// </summary>
        /// <param name="Including">the container whose list is extended</param>
        /// <param name="Included">the container that provides the entries</param>
        /// <param name="ListName">the key of the list, e.g. "LayerList"</param>
        /// <param name="Initialize">not used</param>
        /// <returns>true when at least one entry was added, false when nothing was added or one of the containers or lists does not exist</returns>
        static public bool IncludeAttributeList(IAttributeListContainer Including, IAttributeListContainer Included, string ListName, bool Initialize)
        {
            bool res = false;
            if (Including == null || Included == null) return res;
            IAttributeList toAdd = Included.GetList(ListName);
            IAttributeList addTo = Including.GetList(ListName);
            if (toAdd == null || addTo == null) return res;
            for (int i = 0; i < toAdd.Count; i++)
            {
                INamedAttribute na = toAdd.Item(i);
                if (addTo.Find(na.Name) == null)
                {
                    addTo.Add(na);
                    res = true;
                }
            }
            if (res)
                UpdateLists(Including, false);
            return res;
        }
        /// <summary>
        /// Replaces the attributes of an object (and of its direct children) by the entries with the same name in the
        /// lists of the container, or adds the attributes to the lists when there is no such entry (see
        /// <see cref="IAttributeList.Update(IGeoObject)"/>). Called when objects from another project are inserted,
        /// e.g. by paste or drag and drop, so that they share the attributes of the project. The name of the method
        /// keeps its historical misspelling.
        /// </summary>
        /// <param name="container">the container with the lists, usually the project; null is ignored</param>
        /// <param name="Object2Update">the object to update</param>
        static public void UpdateObjectAttrinutes(IAttributeListContainer container, IGeoObject Object2Update)
        {
            if (container == null) return;
            for (int i = 0; i < container.ListCount; i++)
            {
                container.List(i).Update(Object2Update);
                if (Object2Update.HasChildren())
                    for (int j = 0; j < Object2Update.NumChildren; j++)
                        container.List(i).Update(Object2Update.Child(j));

            }
        }


    }
}
