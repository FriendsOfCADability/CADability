using System;
using System.Drawing;
using System.Reflection;

namespace CADability.UserInterface
{
    /// <summary>
    /// Property entry which shows and edits a string property (typically "Name") of an arbitrary object via reflection: the
    /// property is looked up by name on the type of the object (or its base type) and its getter and setter are invoked when the
    /// entry is displayed or the user enters a new text. Used e.g. for the names of blocks, faces, shells and solids.
    /// </summary>

    public class NameProperty : IShowPropertyImpl
    {
        private object ObjectWithName;
        private PropertyInfo TheProperty;

        /// <summary>
        /// Creates the entry for the string property <paramref name="PropertyName"/> of <paramref name="ObjectWithName"/>. The
        /// property is looked up on the type of the object and, when it is not found there, on its base type.
        /// </summary>
        /// <param name="ObjectWithName">The object holding the property</param>
        /// <param name="PropertyName">Name of the public string property</param>
        /// <param name="resourceId">Resource id of the label text</param>
        public NameProperty(object ObjectWithName, string PropertyName, string resourceId)
        {
            this.ObjectWithName = ObjectWithName;
            base.resourceIdInternal = resourceId;
            TheProperty = ObjectWithName.GetType().GetProperty(PropertyName);
            if (TheProperty == null) TheProperty = ObjectWithName.GetType().BaseType.GetProperty(PropertyName); // besser rekursiv
        }

        private void SetName(string s)
        {
            MethodInfo mi = TheProperty.GetSetMethod();
            object[] prm = new object[1];
            prm[0] = s;
            try
            {
                mi.Invoke(ObjectWithName, prm);
            }
            finally
            {

            }

        }
        private string GetName()
        {
            MethodInfo mi = TheProperty.GetGetMethod();
            object[] prm = new Object[0];
            return (string)mi.Invoke(ObjectWithName, prm);
        }

        #region IShowPropertyImpl Overrides


        /// <inheritdoc/>
        public override void UnSelected()
        {
            base.UnSelected();
        }
        #endregion
        /// <inheritdoc/>
        public override string Value
        {
            get
            {
                string res = GetName();
                if (res == null) return ""; // if null is returned, no edit field will be displayed
                else return res;
            }
        }
        private string oldName;
        /// <inheritdoc/>
        public override void StartEdit(bool editValue)
        {
            oldName = GetName();
        }
        /// <inheritdoc/>
        public override bool EditTextChanged(string newValue)
        {
            using (Frame.Project.Undo.ContextFrame(this))
            {
                SetName(newValue);
                return true;
            }
        }
        /// <inheritdoc/>
        public override void EndEdit(bool aborted, bool modified, string newValue)
        {
            if (aborted)
            {
                SetName(oldName);
            }
            else if (modified)
            {
                SetName(newValue);
            }
            Frame.Project.Undo.ClearContext();
        }
    }
}
