using CADability.Attribute;
using CADability.GeoObject;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;

namespace CADability.UserInterface
{

    /// <summary>
    /// Property page entry to choose a <see cref="ColorDef"/> from a <see cref="ColorList"/> in a drop down list which
    /// shows a color box next to each name. It either controls the color of an <see cref="IColorDef"/> object (the object
    /// is updated when the selection changes and the entry follows changes of the object), accesses a ColorDef property of
    /// an arbitrary object via reflection, or only reports the selection via <see cref="ColorDefChangedEvent"/>. The
    /// <see cref="ColorList.StaticFlags"/> decide whether an "undefined" entry and the colors "from parent" and "from
    /// style" are offered.
    /// </summary>
    public class ColorSelectionProperty : MultipleChoiceProperty
    {
        private IGeoObject toWatch;
        private ColorList colorList;
        private PropertyInfo propertyInfo;
        private object objectWithProperty;
        private ColorList.StaticFlags useFlags;
        private IColorDef iColorDef;
        private ColorDef selectedCD;
        private bool showUnselectedGray;
        /// <summary>
        /// Creates an entry which is not connected to an object: the selection is reported via
        /// <see cref="ColorDefChangedEvent"/>.
        /// </summary>
        /// <param name="resourceId">Resource id of the label text</param>
        /// <param name="clrTable">The list providing the choices</param>
        /// <param name="select">The initially selected color, or null for "undefined"</param>
        /// <param name="flags">Which special colors are offered, see <see cref="ColorList.StaticFlags"/></param>
        public ColorSelectionProperty(string resourceId, ColorList clrTable, ColorDef select, ColorList.StaticFlags flags)
        {
            selectedCD = select;
            useFlags = flags;
            clrTable.Usage = flags;
            base.resourceIdInternal = resourceId;
            colorList = clrTable;
            choices = clrTable.Names;
            ExtendChoices();
            if (select != null)
            {
                SelectedText = select.Name;
            }
            else
            {
                SelectedText = unselectedText;
            }
            unselectedText = StringTable.GetString("ColorDef.Undefined");
            showUnselectedGray = true;
        }
        /// <summary>
        /// Creates an entry which controls the color of <paramref name="iColorDef"/>. The current color of the object is
        /// preselected; if the object is an <see cref="IGeoObject"/>, the entry follows changes of its color.
        /// </summary>
        /// <param name="iColorDef">The object whose color is edited</param>
        /// <param name="resourceId">Resource id of the label text</param>
        /// <param name="clrTable">The list providing the choices</param>
        /// <param name="flags">Which special colors are offered, see <see cref="ColorList.StaticFlags"/></param>
        public ColorSelectionProperty(IColorDef iColorDef, string resourceId, ColorList clrTable, ColorList.StaticFlags flags)
        {
            useFlags = flags;
            clrTable.Usage = flags;
            base.resourceIdInternal = resourceId;
            colorList = clrTable;
            ColorDef selectedColor = iColorDef.ColorDef;
            selectedCD = selectedColor;
            this.iColorDef = iColorDef;
            choices = clrTable.Names;
            ExtendChoices();
            if (selectedColor != null)
            {
                SelectedText = selectedColor.Name;
            }
            else
            {
                SelectedText = unselectedText;
            }
            toWatch = iColorDef as IGeoObject; // may be null
            showUnselectedGray = true;
        }
        private void ExtendChoices()
        {
            List<string> lchoices = new List<string>(choices);
            if (useFlags.HasFlag(ColorList.StaticFlags.allowUndefined))
            {
                unselectedText = StringTable.GetString("ColorDef.Undefined");
                // this name should not be a name of a defined color. But if it is so, we prefix the text by "-" as often as necessary
                while (colorList.Find(unselectedText) != null) unselectedText = "-" + unselectedText + "-";
                lchoices.Insert(0, unselectedText);
            }
            //if (useFlags.HasFlag(ColorList.StaticFlags.allowFromParent))
            //{
            //    lchoices.Add(ColorDef.CDfromParent.Name);
            //}
            //if (useFlags.HasFlag(ColorList.StaticFlags.allowFromStyle))
            //{
            //    lchoices.Add(ColorDef.CDfromStyle.Name);
            //}
            choices = lchoices.ToArray();
        }
        /// <summary>
        /// Creates an entry which edits a property of type <see cref="ColorDef"/> of an arbitrary object via reflection. The
        /// current value of the property is preselected; if the object is an <see cref="IGeoObject"/>, the entry follows
        /// changes of its color.
        /// </summary>
        /// <param name="go">The object holding the property</param>
        /// <param name="propertyName">Name of the ColorDef property of that object</param>
        /// <param name="resourceId">Resource id of the label text</param>
        /// <param name="clrTable">The list providing the choices</param>
        /// <param name="flags">Which special colors are offered, see <see cref="ColorList.StaticFlags"/></param>
        public ColorSelectionProperty(object go, string propertyName, string resourceId, ColorList clrTable, ColorList.StaticFlags flags)
        {
            useFlags = flags;
            flags = clrTable.Usage;
            clrTable.Usage = useFlags;
            base.resourceIdInternal = resourceId;
            colorList = clrTable;
            choices = clrTable.Names;

            objectWithProperty = go;
            propertyInfo = objectWithProperty.GetType().GetProperty(propertyName);
            MethodInfo mi = propertyInfo.GetGetMethod();
            object[] prm = new object[0];
            ColorDef selectedColor = (ColorDef)mi.Invoke(objectWithProperty, prm);
            if (selectedColor != null)
            {
                SelectedText = selectedColor.Name;
            }
            selectedCD = selectedColor;
            clrTable.Usage = flags;
            unselectedText = StringTable.GetString("ColorDef.Undefined");
            toWatch = go as IGeoObject; // may be null
        }
        /// <inheritdoc/>
        public override void SetSelection(int toSelect)
        {
            if (toSelect >= 0 && toSelect < choices.Length)
                selectedCD = colorList[toSelect];
            base.SetSelection(toSelect);
        }
        /// <summary>
        /// Whether the "undefined" entry should be displayed in gray. The value is only stored; the current implementation
        /// does not evaluate it.
        /// </summary>
        public bool ShowAllowUndefinedGray
        {
            get
            {
                return showUnselectedGray;
            }
            set
            {
                showUnselectedGray = value;
            }
        }
        /// <inheritdoc/>
        protected override void OnSelectionChanged(string selected)
        {
            ColorList.StaticFlags flags = colorList.Usage;
            colorList.Usage = useFlags;
            ColorDef found = colorList.Find(selected);
            if (iColorDef != null)
            {
                iColorDef.ColorDef = found;
            }
            else if (propertyInfo != null)
            {
                MethodInfo mi = propertyInfo.GetSetMethod();
                object[] prm = new object[1];
                prm[0] = colorList.Find(selected);
                mi.Invoke(objectWithProperty, prm);
            }
            else if (ColorDefChangedEvent != null)
            {
                ColorDefChangedEvent(found);
            }
            base.OnSelectionChanged(selected);
            colorList.Usage = flags;
        }
        private void colorList_DidModify(object sender, EventArgs args)
        {
            Choices = colorList.Names;
            ExtendChoices();
            ColorDef selectedColor = null;
            if (iColorDef != null)
            {
                selectedColor = iColorDef.ColorDef;
            }
            else if (propertyInfo != null)
            {
                MethodInfo mi = propertyInfo.GetGetMethod();
                object[] prm = new object[0];
                selectedColor = (ColorDef)mi.Invoke(objectWithProperty, prm);
            }
            if (selectedColor != null)
            {
                SelectedText = selectedColor.Name;
            }
            else if (selectedCD != null)
                SelectedText = selectedCD.Name;
        }
        /// <summary>
        /// Delegate for <see cref="ColorDefChangedEvent"/>.
        /// </summary>
        /// <param name="selected">The selected color, or null when the "undefined" entry was selected</param>
        public delegate void ColorDefChangedDelegate(ColorDef selected);
        /// <summary>
        /// Raised when the user selects a color and the entry is neither connected to an <see cref="IColorDef"/> object nor
        /// to a property via reflection.
        /// </summary>
        public event ColorDefChangedDelegate ColorDefChangedEvent;
        private void GeoObjectDidChange(IGeoObject Sender, GeoObjectChange Change)
        {
            if (Sender == toWatch && Change.OnlyAttributeChanged && propertyPage != null)
            {
                if ((Change as GeoObjectChange).MethodOrPropertyName == "ColorDef" ||
                    (Change as GeoObjectChange).MethodOrPropertyName == "Style")
                {
                    if ((toWatch as IColorDef).ColorDef != null) selectedCD = (toWatch as IColorDef).ColorDef;
                    else selectedCD = null;
                    if (propertyPage != null) propertyPage.Refresh(this);
                }
            }
        }
        /// <summary>
        /// The <see cref="IGeoObject"/> whose color is controlled by this entry. Setting it connects the entry to the new
        /// object (which must implement <see cref="IColorDef"/>): its color is changed when the user selects an entry, and the
        /// entry is refreshed when the color of the object changes.
        /// </summary>
        public IGeoObject Connected
        {   // a IGeoObject with a IColorDef to modify the color of that object
            get { return toWatch; }
            set
            {
                if (base.propertyPage != null)
                {   // when there already was an object
                    if (toWatch != null) toWatch.DidChangeEvent -= new ChangeDelegate(GeoObjectDidChange);
                }
                toWatch = value;
                iColorDef = value as IColorDef;
                if (toWatch != null) toWatch.DidChangeEvent += new ChangeDelegate(GeoObjectDidChange);
            }
        }
        #region IPropertyEntry
        /// <inheritdoc/>
        public override void Added(IPropertyPage pp)
        {
            base.Added(pp);
            colorList.DidModifyEvent += new DidModifyDelegate(colorList_DidModify);
            if (toWatch != null) toWatch.DidChangeEvent += new ChangeDelegate(GeoObjectDidChange);
        }
        /// <inheritdoc/>
        public override void Removed(IPropertyPage propertyTreeView)
        {
            base.Removed(propertyTreeView);
            colorList.DidModifyEvent -= new DidModifyDelegate(colorList_DidModify);
            if (toWatch != null) toWatch.DidChangeEvent -= new ChangeDelegate(GeoObjectDidChange);
        }
        /// <inheritdoc/>
        public override string[] GetDropDownList()
        {
            string[] res = new string[choices.Length];
            for (int i = 0; i < choices.Length; i++)
            {
                ColorDef cd = colorList.Find(choices[i]);
                if (cd != null && cd.Source != ColorDef.ColorSource.fromParent && cd.Source != ColorDef.ColorSource.fromStyle)
                {
                    res[i] = "[[ColorBox:" + cd.Color.R.ToString() + ":" + cd.Color.G.ToString() + ":" + cd.Color.B.ToString() + "]]" + cd.Name;
                }
                else
                {
                    res[i] = choices[i];
                }
            }
            return res;
        }
        /// <inheritdoc/>
        public override string Value
        {
            get
            {
                if (selectedCD == null) return "";
                if (selectedCD.Source != ColorDef.ColorSource.fromParent && selectedCD.Source != ColorDef.ColorSource.fromStyle)
                    return "[[ColorBox:" + selectedCD.Color.R.ToString() + ":" + selectedCD.Color.G.ToString() + ":" + selectedCD.Color.B.ToString() + "]]" + selectedCD.Name;
                else return selectedCD.Name;
            }
        }
        /// <inheritdoc/>
        public override void ListBoxSelected(int selectedIndex)
        {
            if (selectedIndex < 0) return;
            selectedCD = colorList.Find(choices[selectedIndex]);
            if (selectedCD == null) return;
            if (iColorDef != null)
            {
                iColorDef.ColorDef = selectedCD;
            }
            else if (propertyInfo != null)
            {
                MethodInfo mi = propertyInfo.GetSetMethod();
                object[] prm = new object[1];
                prm[0] = selectedCD;
                mi.Invoke(objectWithProperty, prm);
            }
            else if (ColorDefChangedEvent != null)
            {
                ColorDefChangedEvent(selectedCD);
            }
            if (propertyPage != null) propertyPage.Refresh(this);
        }
        #endregion

    }
}
