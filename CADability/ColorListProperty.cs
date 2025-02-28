using CADability.Attribute;
using System;
#if WEBASSEMBLY
using CADability.WebDrawing;
using Point = CADability.WebDrawing.Point;
#else
using System.Drawing;
using Point = System.Drawing.Point;
#endif

namespace CADability.UserInterface
{
    /// <summary>
    /// 
    /// </summary>

    public class ColorListProperty : PropertyEntryImpl, ICommandHandler
    {
        private ColorList colorList; // the ColorList in which it is contained
        private int index; // index in the ColorList
        public ColorListProperty(ColorList ColorList, int Index)
        {
            colorList = ColorList;
            index = Index;
            base.resourceIdInternal = "ColorName";
        }
        #region PropertyEntry
        public override PropertyEntryType Flags
        {
            get
            {
                PropertyEntryType flags = PropertyEntryType.Selectable | PropertyEntryType.LabelEditable | PropertyEntryType.ContextMenu | PropertyEntryType.ValueAsButton; // why was there: | PropertyEntryType.DropDown ?
                if (colorList.CurrentIndex == index)
                    flags |= PropertyEntryType.Bold;
                return flags;
            }
        }
        public override string LabelText
        {
            get
            {
                return colorList.GetName(index);
            }
        }
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                return MenuResource.LoadMenuDefinition("MenuId.ColorListEntry", false, this);
            }
        }
        public override void ButtonClicked(PropertyEntryButton button)
        {
                    OnClick(null, null);
        }
        public override string[] GetDropDownList()
        {
            string[] res = new string[colorList.Count];
            for (int i = 0; i < colorList.Count; i++)
            {
                res[i] = "[[ColorBox:" + colorList[i].Color.R.ToString() + ":" + colorList[i].Color.G.ToString() + ":" + colorList[i].Color.B.ToString() + "]]" + colorList[i].Name;
            }
            return res;
        }
        public override string Value
        {
            get
            {
                if (index < 0) return "";
                return "[[ColorBox:" + colorList[index].Color.R.ToString() + ":" + colorList[index].Color.G.ToString() + ":" + colorList[index].Color.B.ToString() + "]]" + colorList[index].Name;
            }
        }
        public override bool EditTextChanged(string newValue)
        {
            return true;
        }
        public override void EndEdit(bool aborted, bool modified, string newValue)
        {
            if (!aborted && !colorList.IsStatic(index))
                colorList.SetName(index, newValue);
        }
        #endregion
        #region ICommandHandler Members
        private void OnClick(object sender, EventArgs e)
        {
            if (!colorList.IsStatic(index))
            {
                Color color = colorList.GetColor(index);

                if (Frame.UIService.ShowColorDialog(ref color) == Substitutes.DialogResult.OK)
                {
                    colorList.SetColor(index, color);
                    propertyPage.Refresh(colorList);
                }
            }
        }
        bool ICommandHandler.OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.ColorListEntry.Delete":
                    colorList.RemoveAt(index);
                    propertyPage.Refresh(colorList);
                    return true;
                case "MenuId.ColorListEntry.Current":
                    colorList.CurrentIndex = index;
                    propertyPage.Refresh(colorList);
                    return true;
                case "MenuId.ColorListEntry.ChangeColor":
                    OnClick(null, null);
                    return true;
                case "MenuId.ColorListEntry.Edit":
                    this.StartEdit(false);
                    return true;
            }
            return false;
        }
        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            switch (MenuId)
            {
                case "MenuId.ColorListEntry.Delete":
                    if (colorList.CurrentIndex == index)
                        CommandState.Enabled = false;
                    break;
                case "MenuId.ColorListEntry.Current":
                    if (colorList.CurrentIndex == index)
                    {
                        CommandState.Checked = true;
                        CommandState.Enabled = false;
                    }
                    else
                    {
                        CommandState.Checked = false;
                        CommandState.Enabled = true;
                    }
                    break;
            }
            return true;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        #endregion

        public override void ListBoxSelected(int selectedIndex)
        {
        }
    }
}
