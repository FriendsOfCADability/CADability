using System;
using System.Drawing;
using System.Reflection;
using CheckState = CADability.Substitutes.CheckState;


namespace CADability.UserInterface
{
    public class CheckProperty : IShowPropertyImpl
    {
        private object objectWithProperty;
        private PropertyInfo propertyInfoBool;
        private PropertyInfo propertyInfoCheckState;
        private MethodInfo propertyInfoGetBool;
        private MethodInfo propertyInfoSetBool;
        private int state;

        public CheckProperty(string resourceID, CheckState state)
        {
            base.resourceIdInternal = resourceID;
            this.state = (int)state;
        }
        public CheckProperty(object ObjectWithProperty, string PropertyName, string resourceId)
        {

            objectWithProperty = ObjectWithProperty;
            base.resourceIdInternal = resourceId;

            propertyInfoBool = objectWithProperty.GetType().GetProperty(PropertyName, typeof(bool));
            propertyInfoCheckState = objectWithProperty.GetType().GetProperty(PropertyName, typeof(CheckState));
            propertyInfoGetBool = objectWithProperty.GetType().GetMethod("Get"+PropertyName, new Type[] { typeof(string) });
            propertyInfoSetBool = objectWithProperty.GetType().GetMethod("Set"+PropertyName, new Type[] { typeof(string), typeof(bool) });
        }
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

        public delegate void CheckStateChangedDelegate(string label, CheckState state);
        public event CheckStateChangedDelegate CheckStateChangedEvent;
        #region IShowPropertyImpl overrides
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.EntryType"/>, 
        /// returns <see cref="ShowPropertyEntryType.Control"/>.
        /// </summary>
        public override PropertyEntryType Flags => PropertyEntryType.Checkable;
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
