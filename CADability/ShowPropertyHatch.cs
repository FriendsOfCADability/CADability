using CADability.Actions;
using CADability.GeoObject;
using System.Collections.Generic;

namespace CADability.UserInterface
{
    /// <summary>
    /// 
    /// </summary>
    internal class ShowPropertyHatch : PropertyEntryImpl, ICommandHandler, IGeoObjectShowProperty
    {
        private readonly Hatch hatch;
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
                    DoubleProperty dp = new DoubleProperty(Frame, "Hatch.Area");
                    dp.OnGetValue = () => hatch?.CompoundShape?.Area ?? 0.0;
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
        public override void Added(IPropertyPage page)
        {	// die events müssen in Added angemeldet und in Removed wieder abgemeldet werden,
            // sonst bleibt die ganze ShowProperty für immer an der Linie hängen
            hatch.UserData.UserDataAddedEvent += OnUserDataAdded;
            hatch.UserData.UserDataRemovedEvent += OnUserDataAdded;
            base.Added(page);
        }
        private void OnUserDataAdded(string name, object value)
        {
            this.subEntries = null;
            attributeProperties = hatch.GetAttributeProperties(Frame);
            propertyPage.Refresh(this);
        }
        public override void Removed(IPropertyPage page)
        {
            hatch.UserData.UserDataAddedEvent -= OnUserDataAdded;
            hatch.UserData.UserDataRemovedEvent -= OnUserDataAdded;
            base.Removed(page);
        }

#endregion
#region ICommandHandler Members
        public virtual bool OnCommand(string menuId)
        {
            switch (menuId)
            {
                case "MenuId.Explode":
                    if (Frame.ActiveAction is SelectObjectsAction)
                    {
                        using (Frame.Project.Undo.UndoFrame)
                        {
                            IGeoObjectOwner addTo = hatch.Owner ?? Frame.ActiveView.Model;
                            GeoObjectList toSelect = hatch.Decompose();
                            addTo.Remove(hatch);
                            foreach (var t in toSelect)
	                            addTo.Add(t);
                            
                            SelectObjectsAction soa = Frame.ActiveAction as SelectObjectsAction;
                            soa.SetSelectedObjects(toSelect); // alle Teilobjekte markieren
                        }
                    }
                    return true;
            }
            return false;
        }
        public virtual bool OnUpdateCommand(string menuId, CommandState commandState)
        {
            switch (menuId)
            {
                case "MenuId.Explode":
                    commandState.Enabled = true; // hier müssen die Flächen rein
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
    }
}
