using CADability.Actions;
using CADability.Attribute;
using CADability.GeoObject;
using System.Collections.Generic;

namespace CADability.UserInterface
{
    /// <summary>
    /// 
    /// </summary>
    internal class ShowPropertyHatch : PropertyEntryImpl, ICommandHandler, IGeoObjectShowProperty
    {
        private Hatch hatch;
        private IPropertyEntry[] subEntries;
        private IPropertyEntry[] attributeProperties; // Anzeigen für die Attribute (Ebene, Farbe u.s.w)
        public ShowPropertyHatch(Hatch hatch, IFrame frame): base(frame)
        {
            this.hatch = hatch;
            base.resourceIdInternal = "Hatch.Object";
            attributeProperties = hatch.GetAttributeProperties(Frame);
        }
        public IPropertyEntry GetHatchStyleProperty()
        {
            return SubItems[0];
        }
        #region PropertyEntryImpl Overrides
        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (subEntries == null)
                {
                    IPropertyEntry[] mainProps = new IPropertyEntry[1];
                    DoubleProperty dp = new DoubleProperty("Hatch.Area", Frame);
                    dp.GetDoubleEvent += new CADability.UserInterface.DoubleProperty.GetDoubleDelegate(OnGetArea);
                    dp.Refresh();
                    mainProps[0] = dp;
                    subEntries = PropertyEntryImpl.Concat(mainProps, attributeProperties);
                }
                return subEntries;
            }
        }
        public override PropertyEntryType Flags => PropertyEntryType.ContextMenu | PropertyEntryType.Selectable | PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries;
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                List<MenuWithHandler> items = new List<MenuWithHandler>(MenuResource.LoadMenuDefinition("MenuId.Object.Hatch", false, this));
                CreateContextMenueEvent?.Invoke(this, items);
                hatch.GetAdditionalContextMenue(this, Frame, items);
                return items.ToArray();
            }
        }
        public override void Added(IPropertyPage propertyPage)
        {	// die events müssen in Added angemeldet und in Removed wieder abgemeldet werden,
            // sonst bleibt die ganze ShowProperty für immer an der Linie hängen
            hatch.UserData.UserDataAddedEvent += new UserData.UserDataAddedDelegate(OnUserDataAdded);
            hatch.UserData.UserDataRemovedEvent += new UserData.UserDataRemovedDelegate(OnUserDataAdded);
            base.Added(propertyPage);
        }
        void OnUserDataAdded(string name, object value)
        {
            this.subEntries = null;
            attributeProperties = hatch.GetAttributeProperties(Frame);
            propertyPage.Refresh(this);
        }
        public override void Removed(IPropertyPage propertyPage)
        {
            hatch.UserData.UserDataAddedEvent -= new UserData.UserDataAddedDelegate(OnUserDataAdded);
            hatch.UserData.UserDataRemovedEvent -= new UserData.UserDataRemovedDelegate(OnUserDataAdded);
            base.Removed(propertyPage);
        }

#endregion
        private void OnHatchStyleSelectionChanged(HatchStyle SelectedHatchstyle)
        {
            hatch.HatchStyle = SelectedHatchstyle;
        }
#region ICommandHandler Members
        virtual public bool OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.Explode":
                    if (Frame.ActiveAction is SelectObjectsAction)
                    {
                        using (Frame.Project.Undo.UndoFrame)
                        {
                            IGeoObjectOwner addTo = hatch.Owner;
                            if (addTo == null) addTo = Frame.ActiveView.Model;
                            GeoObjectList toSelect = hatch.Decompose();
                            addTo.Remove(hatch);
                            for (int i = 0; i < toSelect.Count; ++i)
                            {
                                addTo.Add(toSelect[i]);
                            }
                            SelectObjectsAction soa = Frame.ActiveAction as SelectObjectsAction;
                            soa.SetSelectedObjects(toSelect); // alle Teilobjekte markieren
                        }
                    }
                    return true;
            }
            return false;
        }
        virtual public bool OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            switch (MenuId)
            {
                case "MenuId.Explode":
                    CommandState.Enabled = true; // hier müssen die Flächen rein
                    return true;
            }
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        #endregion
        #region IGeoObjectShowProperty Members
        public event CADability.GeoObject.CreateContextMenueDelegate CreateContextMenueEvent;
        IGeoObject IGeoObjectShowProperty.GetGeoObject()
        {
            return hatch;
        }
        string IGeoObjectShowProperty.GetContextMenuId()
        {
            return "MenuId.Object.Hatch";
        }
#endregion
        private double OnGetArea(DoubleProperty sender)
        {
            if (hatch != null && hatch.CompoundShape != null)
                return hatch.CompoundShape.Area;
            return 0.0;
        }
    }
}
