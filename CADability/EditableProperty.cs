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
        public delegate T GetValueDelegate();
        public delegate void SetValueDelegate(T val);
        private GetValueDelegate getValueDelegate;
        private SetValueDelegate setValueDelegate;
        private object objectWithProperty;
        private PropertyInfo propertyInfo;
        private T currentValue; // the current value, maybe during typing when immediate update is deferred, the object doesn't have this value yet
        private bool updateDeferred = false;
        private UserData userData;

        protected GeoPoint hotspotPosition; // position of the Hotspot, if there is one
        private string contextMenuId; // MenueId for ContextMenu may be overridden
        private ICommandHandler contextMenuHandler; // command handler for the overridden contextMenuId
        public ModifyWithMouseDelegate ModifyWithMouse;
        public delegate void LockedChangedDelegate(bool locked);
        public LockedChangedDelegate LockedChanged;


        /// <summary>
        /// Creates an editable property entry (for the control center), which communicates with the object (which holds the property) via delegates.
        /// </summary>
        /// <param name="getValueDelegate">Get the value of the property from the object, which holds the property</param>
        /// <param name="setValueDelegate">Set the value of th eproperty</param>
        /// <param name="resourceId">A resource id for the text of the label</param>
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
        public EditableProperty(object objectWithProperty, string getSetProperty, string resourceId = null, string contextMenuId = null) : this(resourceId, contextMenuId)
        {
            this.objectWithProperty = objectWithProperty;
            propertyInfo = objectWithProperty.GetType().GetProperty(getSetProperty, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        }
        /// <summary>
        /// Creates an EditableProperty without specifying the communication with the object holding the property.
        /// This must be specified before using the object by setting <see cref="EditableProperty{T}.OnGetValue"/> and <see cref="EditableProperty{T}.OnSetValue"/>
        /// </summary>
        /// <param name="resourceId"></param>
        public EditableProperty(string resourceId = null, string contextMenuId = null) : base(resourceId)
        {
            this.contextMenuId = contextMenuId;
            this.contextMenuHandler = this as ICommandHandler; // maybe contextMenuId and contextMenuHandler is null, then there is no standerd context menu for the derived class
        }

        public GetValueDelegate OnGetValue { set { getValueDelegate = value; } }
        public SetValueDelegate OnSetValue { set { setValueDelegate = value; } }
        public object GetConnectedObject()
        {
            if (objectWithProperty != null) return objectWithProperty;
            if (setValueDelegate != null) return setValueDelegate.Target;
            return null;
        }
        public string GetConnectedPropertyName()
        {
            if (propertyInfo != null) return propertyInfo.Name;
            if (setValueDelegate != null) return setValueDelegate.Method.Name;
            return null;
        }
        public UserData UserData
        {
            get
            {
                if (userData == null) userData = new UserData();
                return userData;
            }
        }
        public bool LabelIsEditable { get; set; } = false;

        public GeoPoint GetHotspotPosition()
        {
            return hotspotPosition;
        }
        public void StartModifyWithMouse()
        {
            ModifyWithMouse?.Invoke(this, true);
        }
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
        public bool ShowMouseButton { get; set; }
        public bool Lockable { get; set; }
        private bool locked = false;
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
        public void SetContextMenu(string contextMenuId, ICommandHandler contextMenuHandler)
        {
            this.contextMenuId = contextMenuId;
            this.contextMenuHandler = contextMenuHandler;
        }
        protected string GetContextMenuId()
        {
            return contextMenuId;
        }
        public void FireModifyWithMouse(bool start)
        {	// called by GeneralGeoPointAction to start mouse modification
            if (GetConnectedObject() is IGeoObject go)
            {
                go.ModifyWithMouse(this, GetConnectedPropertyName(), start);
            }
        }

        public void SetFocus()
        {
            propertyPage.SelectEntry(this);
        }

        public delegate void LabelChangedDelegate(EditableProperty<T> sender, string newLabel);
        public LabelChangedDelegate LabelTextChanged;
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
        public override void Added(IPropertyPage pp)
        {
            base.Added(pp);
            currentValue = GetValue();
        }
        public override string Value
        {
            get
            {
                if (updateDeferred) return ValueToText(currentValue);
                else return ValueToText(GetValue());
            }
        }
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
        public override bool EditTextChanged(string newValue)
        {
            if (isEditingValue)
            {
                if (!TextToValue(newValue, out T val)) return false;
                else
                {
                    updateDeferred = DeferUpdate;
                    /// ContextFrame and value changes:
                    /// A editable property often represents and is connected to a property of an <see cref="IGeoObject"/>. The value of this property may be changed
                    /// in different scenarios: typing with the keyboard, executing a menu command (e.g. dividing the length by two) or starting an action, which
                    /// works on the property by setting the value.
                    /// When typing or using an action, there will be multiple or many changes of the property, which would each create an undo step and each need
                    /// a recalculation of the octtree (when the object is in a model). So we use a "context" for the changes. Changes within the same context frame
                    /// have the following effect: the first change in a new frame saves the objects state for possible undo actions, and removes the object from
                    /// the octtree. The last change of the frame re-inserts the object to the octtree.
                    using (Frame.Project.Undo.ContextFrame(this))
                    {
                        SetValue(val, !DeferUpdate);
                    }
                }
                return true;
            }
            return false;
        }
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
        public override bool IsLocked { get => Locked; set => Locked = value; }
        public override void ButtonClicked(PropertyEntryButton button)
        {
            if (button == PropertyEntryButton.locked)
            {
                Locked = !Locked;
            }
        }
    }
}
