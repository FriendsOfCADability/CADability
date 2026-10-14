using CADability.Attribute;
using CADability.GeoObject;
using CADability.Substitutes;
using CADability.UserInterface;
using System;


namespace CADability
{
    /// <summary>
    /// Prototype for the notification of modifications. The project uses it for its IsModified flag, which is set
    /// whenever this event arrives from anywhere (GeoObjects, attribute lists).
    /// </summary>
    public delegate void DidModifyDelegate(object sender, EventArgs args);
    /// <summary>
    /// Objects implementing this interface raise the <see cref="DidModifyEvent"/> (a <see cref="DidModifyDelegate"/>)
    /// whenever they are modified in a relevant way.
    /// </summary>

    public interface INotifyModification
    {
        event DidModifyDelegate DidModifyEvent;
    }

    /// <summary>
    /// Used as a parameter in the event <see cref="RemovingFromListDelegate"/>.
    /// When handling an appropriate event, you can prevent the object beeing removed from the list.
    /// </summary>

    public class RemovingFromListEventArgs : EventArgs
    {
        /// <summary>
        /// The list from which the object is going to be removed.
        /// </summary>
        public object List;
        /// <summary>
        /// The object that is going to be removed.
        /// </summary>
        public object Item;
        /// <summary>
        /// Set this to true if you want to prevent the object to be removed.
        /// </summary>
        public bool Refuse;
        /// <summary>
        /// Name of the Item.
        /// </summary>
        public string Name;
        /// <summary>
        /// Resource ID for errormessages that will be displayed in <see cref="ItemIsUsed"/>.
        /// ResourceID+".ItemIsUsed" is the formattable string like "Item with the name {0} is used. Do you want to remove it anyway".
        /// ResourceID+".Label" is used for the Title of the message.
        /// </summary>
        public string ResourceID;
        /// <summary>
        /// Creates an RemovingFromListEventArgs object.
        /// </summary>
        /// <param name="List">The List, from which the object is going to be removed</param>
        /// <param name="Item">The object that is going to be removed</param>
        /// <param name="Name">Name of the item</param>
        /// <param name="ResourceID">Resource ID for the messages displayed in <see cref="ItemIsUsed"/></param>
        public RemovingFromListEventArgs(object List, object Item, string Name, string ResourceID)
        {
            this.List = List;
            this.Item = Item;
            this.Refuse = false;
            this.Name = Name;
            this.ResourceID = ResourceID;
        }
        /// <summary>
        /// Displays a massage telling the user that the item is still in use
        /// and asks the user whether the item should be removed anyway.
        /// </summary>
        public virtual void ItemIsUsed()
        {

            if (DialogResult.No == FrameImpl.MainFrame.UIService.ShowMessageBox(StringTable.GetFormattedString(ResourceID + ".ItemIsUsed", Name), StringTable.GetString(ResourceID + ".Label"), MessageBoxButtons.YesNo))
            {
                Refuse = true;
            }
        }
        /// <summary>
        /// Displays a massage telling the user that the last item cannot be removed
        /// </summary>
        public virtual void DontRemoveLastItem()
        {
            FrameImpl.MainFrame.UIService.ShowMessageBox(StringTable.GetString(ResourceID + ".DontRemoveLastItem"), StringTable.GetString(ResourceID + ".Label"), MessageBoxButtons.OK);
            Refuse = true;
        }
    }
    /// <summary>
    /// List objects (e.g. <see cref="HatchStyleList"/>) raise this event, when an
    /// object is going to be removed. You may prevent this by setting the <see cref="RemovingFromListEventArgs.Refuse"/>
    /// member in <see cref="RemovingFromListEventArgs"/> to true.
    /// </summary>
    public delegate void RemovingFromListDelegate(IAttributeList Sender, RemovingFromListEventArgs EventArg);

    /// <summary>
    /// All Attributes for IGeoObject objects (e.g. Layer) are identified by name. 
    /// They all implement this interface
    /// </summary>

    public interface INamedAttribute
    {
        /// <summary>
        /// The unique name of the attribute
        /// </summary>
        string Name { get; set; }
        /// <summary>
        /// The list that contains this attribute (may also be null)
        /// </summary>
        IAttributeList Parent { get; set; }
        /// <summary>
        /// Gets a selection property to select an appropriate attribute from a list. 
        /// The current attribute can be retrieved by calling IGeoObject.GetNamedAttribute(key) and set by
        /// calling IGeoObject.SetNamedAttribute(key, newValue). List list of available properties should be retreived
        /// for the project by calling IAttributeListContainer.GetList(key).
        /// </summary>
        /// <param name="key">The unique key of the attribute</param>
        /// <param name="project">The project that contains the attributeList</param>
        /// <param name="geoObjectList">The objects that keep the attribute</param>
        /// <returns></returns>
        IPropertyEntry GetSelectionProperty(string key, Project project, GeoObjectList geoObjectList);
    }

}


