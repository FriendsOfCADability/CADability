using CADability.GeoObject;
using System;
using System.Collections;

namespace CADability.Attribute
{
    /// <summary>
    /// Comparer to sort lists of INamedAttributes (e.g. <see cref="LayerList"/>).
    /// </summary>

    public class NamedAttributeComparer : IComparer
    {
        /// <summary>
        /// Empty constructor
        /// </summary>
        public NamedAttributeComparer() { }
        #region IComparer Members

        int IComparer.Compare(object x, object y)
        {
            INamedAttribute xNA = x as INamedAttribute;
            if (xNA == null)
                throw new ArgumentException("No valid INamedAttribute", "object x");
            INamedAttribute yNA = y as INamedAttribute;
            if (yNA == null)
                throw new ArgumentException("No valid INamedAttribute", "object y");
            return xNA.Name.CompareTo(yNA.Name);
        }

        #endregion
    }

    /// <summary>
    /// Interface used by named objects, like attributes (e.g. FilterList)
    /// </summary>

    public interface INameChange
    {
        /// <summary>
        /// Check whether name may be changed
        /// </summary>
        /// <param name="namedObject"></param>
        /// <param name="newName"></param>
        /// <returns></returns>
        bool MayChangeName(object namedObject, string newName);
        /// <summary>
        /// Notify that name has changed
        /// </summary>
        /// <param name="namedObject"></param>
        /// <param name="oldName"></param>
        void NameChanged(object namedObject, string oldName);
    }

    /// <summary>
    /// Interface implemented by lists of attributes, e.g. <see cref="LayerList"/>.
    /// </summary>

    public interface IAttributeList
    {
        /// <summary>
        /// Gets the count of items in the list.
        /// </summary>
        int Count { get; }
        /// <summary>
        /// Returns the item with the given index.
        /// </summary>
        /// <param name="Index">Index of the item</param>
        /// <returns>The item</returns>
        INamedAttribute Item(int Index);
        /// <summary>
        /// Returns an item with the given name or null.
        /// </summary>
        /// <param name="name">name of the requested item</param>
        /// <returns>the item or null if not found</returns>
        INamedAttribute Find(string name);
        /// <summary>
        /// Returns the current item of that list. May also be null.
        /// </summary>
        INamedAttribute Current { get; }
        /// <summary>
        /// Adds a named attribute to that list
        /// </summary>
        /// <param name="toAdd">the attribute to add</param>
        void Add(INamedAttribute toAdd);
        /// <summary>
        /// Internal use only.
        /// </summary>
        IAttributeListContainer Owner { get; set; }
        // event AttributeChangedDelegate OnAttributeChanged;
        /// <summary>
        /// Internal use only.
        /// </summary>
        void AttributeChanged(INamedAttribute attribute, ReversibleChange Change);
        /// <summary>
        /// Initializes this list with default items.
        /// </summary>
        void Initialize();
        /// <summary>
        /// Creates a clone of both the list and the items. 
        /// </summary>
        /// <returns></returns>
        IAttributeList Clone();
        /// <summary>
        /// When attributes refer to other attributes these should be in the same
        /// <see cref="AttributeListContainer"/>. This method checks this consistency,
        /// changes the references where needed and adds new attributes to the corresponding
        /// lists when necessary.
        /// </summary>
        /// <param name="AddMissingToList">true: add referenced attributes when not found in a list</param>
        void Update(bool AddMissingToList);
        /// <summary>
        /// substitutes the attribute to the list member with the same name or adds objects attribute to the list.
        /// </summary>
        /// <param name="Object2Update">the Object with Attribute to substitute</param>
        void Update(IGeoObject Object2Update);

        /// <summary>
        /// Determines, whether a named attribute may change its name. It may not change
        /// its name if there is already an attribute with that desired name.
        /// </summary>
        /// <param name="attribute">the attribute that wants to change its name</param>
        /// <param name="newName">the new name</param>
        /// <returns>true, if name change is possible, false otherwise</returns>
        bool MayChangeName(INamedAttribute attribute, string newName);
        /// <summary>
        /// Notifies the list, that an attribute changed it's name.
        /// </summary>
        /// <param name="attribute">the attribute</param>
        /// <param name="oldName">the old name (the new name can be accessed from the attribute)</param>
        void NameChanged(INamedAttribute attribute, string oldName);
    }


    public delegate void AttributeChangedDelegate(object Attribute, ReversibleChange Change);

}
