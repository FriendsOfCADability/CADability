using CADability.Attribute;
using CADability.GeoObject;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;

namespace CADability.UserInterface
{

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
        public override void SetSelection(int toSelect)
        {
            if (toSelect >= 0 && toSelect < choices.Length)
                selectedCD = colorList[toSelect];
            base.SetSelection(toSelect);
        }
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
        public delegate void ColorDefChangedDelegate(ColorDef selected);
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
        public override void Added(IPropertyPage pp)
        {
            base.Added(pp);
            colorList.DidModifyEvent += new DidModifyDelegate(colorList_DidModify);
            if (toWatch != null) toWatch.DidChangeEvent += new ChangeDelegate(GeoObjectDidChange);
        }
        public override void Removed(IPropertyPage propertyTreeView)
        {
            base.Removed(propertyTreeView);
            colorList.DidModifyEvent -= new DidModifyDelegate(colorList_DidModify);
            if (toWatch != null) toWatch.DidChangeEvent -= new ChangeDelegate(GeoObjectDidChange);
        }
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
