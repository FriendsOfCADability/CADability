using CADability.GeoObject;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CADability.UserInterface
{
    /// <summary>
    /// Defines a base class for editable properties shown on a property page (IPropertyPage) in the controlCenter (properties explorer).
    /// It is a single entry in the property page which may have subentries.
    /// Usually a property of type <typeparamref name="T"/> of some object is connected with this EditableProperty, e.g. the startpoint of a line 
    /// or the distance of hatch lines or some setting value. The communication with the object which hold the property is either done via delegates
    /// or via reflection.
    /// When the user clicks on the value part of the <see cref="IPropertyEntry"/>, an editBox is activated to edit the value of the property. There must be 
    /// a <see cref="ValueToText(T)"/> and <see cref="TextToValue(string, out T)"/> method defined in the derived class, which
    /// convert the property value in a string and vice versa. The value of the property may be changed by the editBox (typing) or from the outside.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class EditableProperty<T> : PropertyEntryImpl, IConstructProperty
    {
        private class EmptyDisposable : IDisposable
        {
            public void Dispose()
            {
                // nothing to do
            }
        }
        /// <summary>
        /// Delegate which gets the value of the property from the object holding it, see <see cref="OnGetValue"/>.
        /// </summary>
        /// <returns>The current value of the property</returns>
        public delegate T GetValueDelegate();
        /// <summary>
        /// Delegate which sets the value of the property on the object holding it, see <see cref="OnSetValue"/>.
        /// </summary>
        /// <param name="val">The new value</param>
        public delegate void SetValueDelegate(T val);
        private GetValueDelegate getValueDelegate;
        private SetValueDelegate setValueDelegate;
        private object objectWithProperty;
        private PropertyInfo propertyInfo;
        private T currentValue; // the current value, maybe during typing when immediate update is deferred, the object doesn't have this value yet
        private bool updateDeferred = false;
        private UserData userData;

        /// <summary>
        /// The position of the hotspot of this property in the drawing area, if it has one (see
        /// <see cref="SetHotspotPosition(GeoPoint)"/>). Not all derived classes use it.
        /// </summary>
        protected GeoPoint hotspotPosition; // position of the Hotspot, if there is one
        private string contextMenuId; // MenueId for ContextMenu may be overridden
        private ICommandHandler contextMenuHandler; // command handler for the overridden contextMenuId
        /// <summary>
        /// Callback invoked by <see cref="StartModifyWithMouse"/> (with startModifying set to true) when the user wants to
        /// modify the value with the mouse, e.g. by clicking the mouse button of the entry or selecting its hotspot. The owner
        /// of the property typically starts an action which sets the value from the mouse position. null when the value
        /// cannot be modified with the mouse.
        /// </summary>
        public ModifyWithMouseDelegate ModifyWithMouse;
        /// <summary>
        /// Delegate for <see cref="LockedChanged"/>.
        /// </summary>
        /// <param name="locked">The new value of <see cref="Locked"/></param>
        public delegate void LockedChangedDelegate(bool locked);
        /// <summary>
        /// Callback invoked when <see cref="Locked"/> changes, either by code or by the user clicking the lock button of the
        /// entry (see <see cref="Lockable"/>). The inputs of the construction actions use it to fix a value.
        /// </summary>
        public LockedChangedDelegate LockedChanged;


        /// <summary>
        /// Creates an editable property entry (for the control center), which communicates with the object (which holds the property) via delegates.
        /// </summary>
        /// <param name="getValueDelegate">Get the value of the property from the object, which holds the property</param>
        /// <param name="setValueDelegate">Set the value of th eproperty</param>
        /// <param name="resourceId">A resource id for the text of the label</param>
        /// <param name="contextMenuId">Menu id (from the menu resource) of the context menu of this property, or null if there is no context menu</param>
        public EditableProperty(GetValueDelegate getValueDelegate, SetValueDelegate setValueDelegate, string resourceId = null, string contextMenuId = null) : this(resourceId, contextMenuId)
        {
            this.getValueDelegate = getValueDelegate;
            this.setValueDelegate = setValueDelegate;
        }
        /// <summary>
        /// Creates an editable property entry (for the control center), which communicates with the object (which holds the property) via reflection.
        /// The object must have an accessible property with the provided <paramref name="getSetProperty"/> name.
        /// </summary>
        /// <param name="objectWithProperty">The object, which holds the property</param>
        /// <param name="getSetProperty">The Name of the property</param>
        /// <param name="resourceId">A resource id for the text of the label</param>
        /// <param name="contextMenuId">Menu id (from the menu resource) of the context menu of this property, or null if there is no context menu</param>
        public EditableProperty(object objectWithProperty, string getSetProperty, string resourceId = null, string contextMenuId = null) : this(resourceId, contextMenuId)
        {
            this.objectWithProperty = objectWithProperty;
            propertyInfo = objectWithProperty.GetType().GetProperty(getSetProperty, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        }
        /// <summary>
        /// Creates an EditableProperty without specifying the communication with the object holding the property.
        /// This must be specified before using the object by setting <see cref="EditableProperty{T}.OnGetValue"/> and <see cref="EditableProperty{T}.OnSetValue"/>
        /// </summary>
        /// <param name="resourceId">A resource id for the text of the label</param>
        /// <param name="contextMenuId">Menu id (from the menu resource) of the context menu of this property, or null if there is no context menu</param>
        public EditableProperty(string resourceId = null, string contextMenuId = null) : base(resourceId)
        {
            this.contextMenuId = contextMenuId;
            this.contextMenuHandler = this as ICommandHandler; // maybe contextMenuId and contextMenuHandler is null, then there is no standerd context menu for the derived class
        }
        /// <summary>
        /// Sets the delegate which provides the value of the property. It replaces the reflection access to the object given
        /// to the constructor, if any, and must be set (together with <see cref="OnSetValue"/>) when the property was created
        /// without a connected object.
        /// </summary>
        public GetValueDelegate OnGetValue { set { getValueDelegate = value; } }
        /// <summary>
        /// Sets the delegate which receives the new value whenever the user has entered one (or an action sets it). It
        /// replaces the reflection access to the object given to the constructor, if any.
        /// </summary>
        public SetValueDelegate OnSetValue { set { setValueDelegate = value; } }
        /// <summary>
        /// Returns the object whose property is edited: the object given to the constructor, or else the target object of the
        /// <see cref="OnSetValue"/> delegate.
        /// </summary>
        /// <returns>The connected object, or null when there is none</returns>
        public object GetConnectedObject()
        {
            if (objectWithProperty != null) return objectWithProperty;
            if (setValueDelegate != null) return setValueDelegate.Target;
            return null;
        }
        /// <summary>
        /// Returns the name of the edited property: the property name given to the constructor, or else the name of the
        /// method behind the <see cref="OnSetValue"/> delegate. It is passed to <see cref="IGeoObject.ModifyWithMouse"/> to
        /// identify the property.
        /// </summary>
        /// <returns>The property name, or null when there is no connected object</returns>
        public string GetConnectedPropertyName()
        {
            if (propertyInfo != null) return propertyInfo.Name;
            if (setValueDelegate != null) return setValueDelegate.Method.Name;
            return null;
        }
        /// <summary>
        /// A container for arbitrary data which the owner of the property may attach to it (e.g. an index identifying the
        /// entry in a list); the property itself does not use it. Created on first access.
        /// </summary>
        public UserData UserData
        {
            get
            {
                if (userData == null) userData = new UserData();
                return userData;
            }
        }
        /// <summary>
        /// The text of the error which occurred when the last entered text was converted or applied (returned by
        /// <see cref="GetErrorText"/> and shown by the property page). Empty when the last value was accepted.
        /// </summary>
        public string ErrorMessage { get; set; } = ""; // if this string contains text, then there was an error setting this value
        /// <summary>
        /// true when the user may also edit the label (the left side) of the entry. After the label has been edited,
        /// <see cref="LabelChanged(string)"/> is called, which invokes <see cref="LabelTextChanged"/>.
        /// </summary>
        public bool LabelIsEditable { get; set; } = false;

        /// <summary>
        /// Returns the position of the hotspot of this property, as set by <see cref="SetHotspotPosition(GeoPoint)"/>
        /// (implements <see cref="IConstructProperty.GetHotspotPosition"/>).
        /// </summary>
        /// <returns>The position in world coordinates</returns>
        public GeoPoint GetHotspotPosition()
        {
            return hotspotPosition;
        }
        /// <summary>
        /// Starts the modification of the value with the mouse by invoking <see cref="ModifyWithMouse"/> with startModifying
        /// set to true (implements <see cref="IConstructProperty.StartModifyWithMouse"/>). Called when the user clicks the
        /// mouse button of the entry or selects its hotspot.
        /// </summary>
        public void StartModifyWithMouse()
        {
            ModifyWithMouse?.Invoke(this, true);
        }
        /// <summary>
        /// Sets the position of the hotspot which represents this property in the drawing area (implements
        /// <see cref="IConstructProperty.SetHotspotPosition(GeoPoint)"/>). The owner of the property updates it when the
        /// object changes.
        /// </summary>
        /// <param name="Here">The position in world coordinates</param>
        public void SetHotspotPosition(GeoPoint Here)
        {
            hotspotPosition = Here;
        }
        /// <summary>
        /// additional menu items for the context menu
        /// </summary>
        public MenuWithHandler[] PrependContextMenu
        {
            private get; set;
        }
        /// <summary>
        /// Whether this entry is the selected entry of its property page. Setting it to true selects the entry, setting it to
        /// false deselects it if it is currently selected. The entry must be displayed on a property page.
        /// </summary>
        public bool IsSelected
        {
            get
            {
                return propertyPage.Selected == this;
            }
            set
            {
                if (value) propertyPage.SelectEntry(this);
                else
                {
                    if (propertyPage.Selected == this) propertyPage.SelectEntry(null);
                }
            }
        }
        /// <summary>
        /// Whether the entry should offer a button to modify the value with the mouse. The value is only stored; the current
        /// property page implementations do not evaluate it.
        /// </summary>
        public bool ShowMouseButton { get; set; }
        /// <summary>
        /// true when the entry shows a lock button (<see cref="PropertyEntryType.Lockable"/>) with which the user toggles
        /// <see cref="Locked"/>. The inputs of the construction actions use it to let the user fix a value.
        /// </summary>
        public bool Lockable { get; set; }
        private bool locked = false;
        /// <summary>
        /// true when the value is locked: the entry is not editable (<see cref="PropertyEntryType.ValueEditable"/> is missing
        /// in <see cref="Flags"/>) and construction actions keep the value fixed. Changing it invokes
        /// <see cref="LockedChanged"/>.
        /// </summary>
        public bool Locked
        {
            get { return locked; }
            set
            {
                if (locked == value) return;
                locked = value;
                LockedChanged?.Invoke(locked);
            }
        }
        private bool highlight = false;
        /// <summary>
        /// true when the entry is displayed highlighted (<see cref="PropertyEntryType.Highlight"/>), e.g. the input of a
        /// construction action which currently expects a value. Setting it refreshes the entry.
        /// </summary>
        public bool Highlight
        {
            get
            {
                return highlight;
            }
            set
            {
                highlight = value;
                propertyPage?.Refresh(this);
            }
        }
        /// <summary>
        /// Replaces the context menu of the entry: <paramref name="contextMenuId"/> names a menu definition in the menu
        /// resource and <paramref name="contextMenuHandler"/> executes its commands. Pass null as menu id to remove the
        /// context menu.
        /// </summary>
        /// <param name="contextMenuId">Id of the menu definition in the menu resource, or null</param>
        /// <param name="contextMenuHandler">Handles the commands of that menu</param>
        public void SetContextMenu(string contextMenuId, ICommandHandler contextMenuHandler)
        {
            this.contextMenuId = contextMenuId;
            this.contextMenuHandler = contextMenuHandler;
        }
        /// <summary>
        /// Returns the id of the context menu in the menu resource, as given to the constructor or to
        /// <see cref="SetContextMenu(string, ICommandHandler)"/>.
        /// </summary>
        /// <returns>The menu id, or null when there is no context menu</returns>
        protected string GetContextMenuId()
        {
            return contextMenuId;
        }
        /// <summary>
        /// Forwards the start or the end of a mouse modification to the connected <see cref="IGeoObject"/> by calling
        /// <see cref="IGeoObject.ModifyWithMouse"/> with the name of the connected property. Called by the general actions
        /// (e.g. GeneralLengthAction) which modify a value with the mouse. Does nothing when the connected object is not an
        /// IGeoObject.
        /// </summary>
        /// <param name="start">true when the modification starts, false when it ends</param>
        public void FireModifyWithMouse(bool start)
        {	// called by GeneralGeoPointAction to start mouse modification
            if (GetConnectedObject() is IGeoObject go)
            {
                go.ModifyWithMouse(this, GetConnectedPropertyName(), start);
            }
        }

        /// <summary>
        /// Selects this entry on its property page so that it receives the focus (implements
        /// <see cref="IConstructProperty.SetFocus"/>). The entry must be displayed on a property page.
        /// </summary>
        public void SetFocus()
        {
            propertyPage.SelectEntry(this);
        }

        /// <summary>
        /// Delegate for <see cref="LabelTextChanged"/>.
        /// </summary>
        /// <param name="sender">The property whose label was edited</param>
        /// <param name="newLabel">The text entered by the user</param>
        public delegate void LabelChangedDelegate(EditableProperty<T> sender, string newLabel);
        /// <summary>
        /// Callback invoked when the user has edited the label of the entry (see <see cref="LabelIsEditable"/>).
        /// </summary>
        public LabelChangedDelegate LabelTextChanged;
        /// <summary>
        /// Called when the user has finished editing the label of the entry (see <see cref="LabelIsEditable"/>). Invokes
        /// <see cref="LabelTextChanged"/>; override it to react in a derived class.
        /// </summary>
        /// <param name="newText">The text entered by the user</param>
        protected virtual void LabelChanged(string newText)
        {
            LabelTextChanged?.Invoke(this, newText);
        }
        /// <summary>
        /// Tries to get the value of the property being handled from the object 
        /// </summary>
        /// <returns></returns>
        protected T GetValue()
        {
            if (getValueDelegate != null) return getValueDelegate();
            else if (propertyInfo != null)
            {
                MethodInfo mi = propertyInfo.GetGetMethod();
                if (mi == null) mi = propertyInfo.GetGetMethod(true);
                return (T)mi.Invoke(objectWithProperty, new object[0]);
            }
            return currentValue; // if there is no connected object only currentValue contains the value
        }
        /// <summary>
        /// Called, when the editbox or some other mechanism has a new value for the property. Transfers this value to the object holding the property.
        /// </summary>
        /// <param name="val"></param>
        /// <param name="notify"></param>
        protected void SetValue(T val, bool notify)
        {
            currentValue = val; // current value contains the value, which is not transferred to the object if notify==false
            if (notify)
            {
                if (setValueDelegate != null) setValueDelegate(val);
                else if (propertyInfo != null)
                {
                    MethodInfo mi = propertyInfo.GetSetMethod();
                    if (mi == null) mi = propertyInfo.GetSetMethod(true);
                    mi.Invoke(objectWithProperty, new object[] { val });
                }
            }
            propertyPage?.Refresh(this);
        }
        /// <summary>
        /// Returns the text representation of the value.
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        protected abstract string ValueToText(T val);
        /// <summary>
        /// Converts the text to a value. Returns true if possible, false otherwise (maybe invalid characters or outside the limit)
        /// </summary>
        /// <param name="text"></param>
        /// <param name="val"></param>
        /// <returns></returns>
        protected abstract bool TextToValue(string text, out T val);
        /// <summary>
        /// Override for more functionality. Only returns PropertyEntryType.ValueEditable and PropertyEntryType.LabelEditable if apropriate
        /// </summary>
        public override PropertyEntryType Flags
        {
            get
            {
                PropertyEntryType res = PropertyEntryType.Selectable;
                if (!ReadOnly && !Locked) res |= PropertyEntryType.ValueEditable;
                if (contextMenuId != null) res |= PropertyEntryType.ContextMenu;
                if (LabelIsEditable) res |= PropertyEntryType.LabelEditable;
                if (Highlight) res |= PropertyEntryType.Highlight;
                if (Lockable) res |= PropertyEntryType.Lockable;
                return res;
            }
        }
        /// <inheritdoc/>
        public override void Added(IPropertyPage pp)
        {
            base.Added(pp);
            currentValue = GetValue();
        }
        /// <inheritdoc/>
        public override string Value
        {
            get
            {
                if (updateDeferred) return ValueToText(currentValue);
                else return ValueToText(GetValue());
            }
        }
        /// <inheritdoc/>
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                Frame.ContextMenuSource = this;
                List<MenuWithHandler> res = new List<MenuWithHandler>();
                if (contextMenuId != null)
                {
                    res.AddRange(MenuResource.LoadMenuDefinition(contextMenuId, false, contextMenuHandler));
                }
                if (PrependContextMenu != null)
                {
                    res.InsertRange(0, PrependContextMenu);
                }
                return res.ToArray();
            }
        }

        /// <summary>
        /// The value before the first keystroke
        /// </summary>
        private T valueBeforeEdit;
        /// <summary>
        /// in contrast to is editing label
        /// </summary>
        bool isEditingValue;
        /// <summary>
        /// Only handles value editing. If the label may also be edited, override this method and call this base implementation;
        /// </summary>
        /// <param name="editValue"></param>
        public override void StartEdit(bool editValue)
        {
            updateDeferred = false;
            isEditingValue = editValue;
            if (editValue)
            {
                valueBeforeEdit = GetValue();
            }
        }
        /// <inheritdoc/>
        public override bool EditTextChanged(string newValue)
        {
            if (isEditingValue)
            {
                if (!TextToValue(newValue, out T val))
                {
                    ErrorMessage = StringTable.GetString("TextToValue.Failed", StringTable.Category.label);
                    return false;
                }
                else
                {
                    ErrorMessage = "";
                    updateDeferred = DeferUpdate;
                    // ContextFrame and value changes:
                    // A editable property often represents and is connected to a property of an IGeoObject. The value of this property may be changed
                    // in different scenarios: typing with the keyboard, executing a menu command (e.g. dividing the length by two) or starting an action, which
                    // works on the property by setting the value.
                    // When typing or using an action, there will be multiple or many changes of the property, which would each create an undo step and each need
                    // a recalculation of the octtree (when the object is in a model). So we use a "context" for the changes. Changes within the same context frame
                    // have the following effect: the first change in a new frame saves the objects state for possible undo actions, and removes the object from
                    // the octtree. The last change of the frame re-inserts the object to the octtree.
                    using (Frame.Project.Undo.ContextFrame(this))
                    {
                        SetValue(val, !DeferUpdate);
                    }
                }
                return String.IsNullOrEmpty(ErrorMessage);
            }
            return false;
        }
        /// <inheritdoc/>
        public override string GetErrorText()
        {
            return ErrorMessage;
        }
        /// <inheritdoc/>
        public override void EndEdit(bool aborted, bool modified, string newValue)
        {
            if (isEditingValue)
            {
                // ContextFrame: each keystroke results in a SetValue call. these calls all use the same context object (namely "this")
                // to only create one undo step. With the first keystroke, the  object is also removed from the models octTree (continuous change)
                // and has to be inserted again, which is done by ClearContext. It would also be done with the next change with a different context,
                // but it is clean and correct to do it here
                using (Frame.Project.Undo.ContextFrame(this)) // all keystrokes belong to the same context frame
                {
                    if (aborted) SetValue(valueBeforeEdit, true);
                    if (modified || DeferUpdate)
                    {
                        if (!TextToValue(newValue, out T val) && !DeferUpdate) SetValue(valueBeforeEdit, true);
                        else SetValue(val, true);
                    }
                }
                Frame.Project.Undo.ClearContext();
                DeferUpdate = false;
                PropertyEntryChangedState(new StateChangedArgs(StateChangedArgs.State.EditEnded));
            }
            else
            {
                if (!aborted) LabelChanged(newValue);
            }
        }
        /// <inheritdoc/>
        public override bool IsLocked { get => Locked; set => Locked = value; }
        /// <inheritdoc/>
        public override void ButtonClicked(PropertyEntryButton button)
        {
            if (button == PropertyEntryButton.locked)
            {
                Locked = !Locked;
            }
        }
    }
}
