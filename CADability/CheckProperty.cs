using System;
using System.Drawing;
using System.Reflection;
using CheckState = CADability.Substitutes.CheckState;


namespace CADability.UserInterface
{
    /// <summary>
    /// Property page entry with a check box. It either holds its own check state or reflects a boolean or
    /// <see cref="CADability.Substitutes.CheckState"/> property of another object, which is read and written via
    /// reflection (see the constructors). Clicking the box toggles the state (an indeterminate state is left unchanged)
    /// and raises <see cref="CheckStateChangedEvent"/>. Used e.g. for the visible layers of a view and for the entries of
    /// the attribute filters.
    /// </summary>
    public class CheckProperty : IShowPropertyImpl
    {
        private object objectWithProperty;
        private PropertyInfo propertyInfoBool;
        private PropertyInfo propertyInfoCheckState;
        private MethodInfo propertyInfoGetBool;
        private MethodInfo propertyInfoSetBool;
        private int state;

        /// <summary>
        /// Creates an entry which holds its own check state. Changes are reported by <see cref="CheckStateChangedEvent"/>.
        /// </summary>
        /// <param name="resourceID">Resource id of the label text</param>
        /// <param name="state">The initial check state</param>
        public CheckProperty(string resourceID, CheckState state)
        {
            base.resourceIdInternal = resourceID;
            this.state = (int)state;
        }
        /// <summary>
        /// Creates an entry which reflects a property of <paramref name="ObjectWithProperty"/> via reflection. The object must
        /// either have a property named <paramref name="PropertyName"/> of type bool or
        /// <see cref="CADability.Substitutes.CheckState"/>, or a pair of methods "Get" + PropertyName(string) and
        /// "Set" + PropertyName(string, bool), which receive the label text of the entry to identify it.
        /// </summary>
        /// <param name="ObjectWithProperty">The object holding the property</param>
        /// <param name="PropertyName">Name of the property, or the suffix of the Get/Set methods</param>
        /// <param name="resourceId">Resource id of the label text</param>
        public CheckProperty(object ObjectWithProperty, string PropertyName, string resourceId)
        {

            objectWithProperty = ObjectWithProperty;
            base.resourceIdInternal = resourceId;

            propertyInfoBool = objectWithProperty.GetType().GetProperty(PropertyName, typeof(bool));
            propertyInfoCheckState = objectWithProperty.GetType().GetProperty(PropertyName, typeof(CheckState));
            propertyInfoGetBool = objectWithProperty.GetType().GetMethod("Get"+PropertyName, new Type[] { typeof(string) });
            propertyInfoSetBool = objectWithProperty.GetType().GetMethod("Set"+PropertyName, new Type[] { typeof(string), typeof(bool) });
        }
        /// <summary>
        /// Refreshes the display of the entry, e.g. after the check state of the connected object was changed from elsewhere.
        /// </summary>
        public void Update()
        {
            propertyPage?.Refresh(this);
        }
        private int CheckState
        {
            get
            {
                if (propertyInfoCheckState != null)
                {
                    MethodInfo mi = propertyInfoCheckState.GetGetMethod();
                    object[] prm = new Object[0];
                    return (int)mi.Invoke(objectWithProperty, prm);
                }
                else if (propertyInfoBool != null)
                {
                    MethodInfo mi = propertyInfoBool.GetGetMethod();
                    object[] prm = new Object[0];
                    bool check = (bool)mi.Invoke(objectWithProperty, prm);
                    if (check) return 1;
                    else return 0;
                } else if (propertyInfoGetBool != null)
                {
                    MethodInfo mi = propertyInfoGetBool;
                    object[] prm = new Object[1];
                    prm[0] = LabelText;
                    bool check = (bool)mi.Invoke(objectWithProperty, prm);
                    if (check) return 1;
                    else return 0;
                }
                return state;
            }
            set
            {
                if (objectWithProperty != null)
                {
                    if (propertyInfoCheckState != null)
                    {
                        MethodInfo mi = propertyInfoCheckState.GetSetMethod();
                        object[] prm = new Object[1];
                        prm[0] = value;
                        mi.Invoke(objectWithProperty, prm);
                    }
                    else if (propertyInfoBool != null)
                    {
                        MethodInfo mi = propertyInfoBool.GetSetMethod();
                        object[] prm = new Object[1];
                        if (value==1) prm[0] = true;
                        else prm[0] = false;
                        mi.Invoke(objectWithProperty, prm);
                    }
                    else if (propertyInfoSetBool != null)
                    {
                        MethodInfo mi = propertyInfoSetBool;
                        object[] prm = new Object[2];
                        prm[0] = labelTextInternal;
                        if (value == 1) prm[1] = true;
                        else prm[1] = false;
                        mi.Invoke(objectWithProperty, prm);
                    }
                    else
                    {
                        state = value;
                    }
                }
                else
                {
                    state = value;
                }
                propertyPage?.Refresh(this);
            }
        }

        /// <summary>
        /// Delegate for <see cref="CheckStateChangedEvent"/>.
        /// </summary>
        /// <param name="label">The label text of the entry, to identify it</param>
        /// <param name="state">The new check state</param>
        public delegate void CheckStateChangedDelegate(string label, CheckState state);
        /// <summary>
        /// Raised when the user has clicked the check box and the state was toggled.
        /// </summary>
        public event CheckStateChangedDelegate CheckStateChangedEvent;
        #region IShowPropertyImpl overrides
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.EntryType"/>, 
        /// returns <see cref="ShowPropertyEntryType.Control"/>.
        /// </summary>
        public override PropertyEntryType Flags => PropertyEntryType.Checkable;
        /// <inheritdoc/>
        public override string Value
        {
            get
            {
                // Value == "0"(not checked), "1"(checked), "2" (indetermined or disabled)
                switch (CheckState)
                {
                    case 0: return "0";
                    case 1: return "1";
                    default: return "2";
                }
            }
        }
        /// <inheritdoc/>
        public override void ButtonClicked(PropertyEntryButton button)
        {
            if (button==PropertyEntryButton.check)
            {
                int cs = CheckState;
                if (cs != 2) cs = 1 - cs;
                CheckState = cs;
                propertyPage?.Refresh(this);
                CheckStateChangedEvent?.Invoke(labelTextInternal, (
                    CheckState)cs);
            }
        }
        #endregion
    }
}
