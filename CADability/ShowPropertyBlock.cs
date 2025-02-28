using CADability.Actions;
using CADability.GeoObject;
using System;
using System.Collections.Generic;


namespace CADability.UserInterface
{
    /// <summary>
    /// 
    /// </summary>
    internal class ShowPropertyBlock : PropertyEntryImpl, ICommandHandler, IGeoObjectShowProperty
    {
        private Block block;
        private IGeoObject ContextMenuSource;
        MultiObjectsProperties multiObjectsProperties;
        private IPropertyEntry[] subEntries;
        private IPropertyEntry[] attributeProperties; // Anzeigen für die Attribute (Ebene, Farbe u.s.w)
        public ShowPropertyBlock(Block Block, IFrame frame): base(frame)
        {
            block = Block;
            attributeProperties = block.GetAttributeProperties(Frame);
            base.resourceIdInternal = "Block.Object";
        }
        public void EditName()
        {
            (subEntries[0] as NameProperty).StartEdit(true);
        }
        #region PropertyEntryImpl Overrides
        public override PropertyEntryType Flags => PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries | PropertyEntryType.ContextMenu | PropertyEntryType.Selectable;
        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (subEntries == null)
                {
                    List<IPropertyEntry> prop = new List<IPropertyEntry>();
                    prop.Add(new NameProperty(this.block, "Name", "Block.Name"));
                    GeoPointProperty refPointPro = new GeoPointProperty("Block.RefPoint", Frame, true);
                    refPointPro.GetGeoPointEvent += new CADability.UserInterface.GeoPointProperty.GetGeoPointDelegate(OnGetRefPoint);
                    refPointPro.SetGeoPointEvent += new CADability.UserInterface.GeoPointProperty.SetGeoPointDelegate(OnSetRefPoint);
                    prop.Add(refPointPro);
                    if (!block.UserData.ContainsData("Block.HideCommonProperties") || !(bool)block.UserData.GetData("Block.HideCommonProperties")) // with this UserData you can disable the common properties of the block
                    {   
                        multiObjectsProperties = new MultiObjectsProperties(Frame, block.Children);
                        prop.Add(multiObjectsProperties.attributeProperties);
                        ShowPropertyGroup spg = new ShowPropertyGroup("Block.Children");
                        for (int i = 0; i < block.Count; ++i)
                        {
                            IPropertyEntry sp = block.Item(i).GetShowProperties(Frame);
                            if (sp is IGeoObjectShowProperty)
                            {
                                (sp as IGeoObjectShowProperty).CreateContextMenueEvent += new CreateContextMenueDelegate(OnCreateContextMenueChild);
                            }
                            spg.AddSubEntry(sp);
                        }
                        prop.Add(spg);
                    }
                    IPropertyEntry[] mainProps = prop.ToArray();
                    subEntries = PropertyEntryImpl.Concat(mainProps, attributeProperties);
                }
                return subEntries;
            }
        }

        void OnCreateContextMenueChild(IGeoObjectShowProperty sender, List<MenuWithHandler> toManipulate)
        {
            ContextMenuSource = sender.GetGeoObject();
            MenuWithHandler[] toAdd = MenuResource.LoadMenuDefinition("MenuId.SelectedObject", false, Frame.CommandHandler);
            toManipulate.AddRange(toAdd);
        }
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                List<MenuWithHandler> items = new List<MenuWithHandler>(MenuResource.LoadMenuDefinition("MenuId.Object.Block", false, this));
                CreateContextMenueEvent?.Invoke(this, items);
                block.GetAdditionalContextMenue(this, Frame, items);
                return items.ToArray();
            }
        }
        public override void Added(IPropertyPage propertyPage)
        {	// die events müssen in Added angemeldet und in Removed wieder abgemeldet werden,
            // sonst bleibt die ganze ShowProperty für immer an der Linie hängen
            block.UserData.UserDataAddedEvent += new UserData.UserDataAddedDelegate(OnUserDataAdded);
            block.UserData.UserDataRemovedEvent += new UserData.UserDataRemovedDelegate(OnUserDataAdded);
            base.Added(propertyPage);
        }
        void OnUserDataAdded(string name, object value)
        {
            this.subEntries = null;
            attributeProperties = block.GetAttributeProperties(Frame);
            propertyPage.Refresh(this);
        }
        public override void Removed(IPropertyPage propertyPage)
        {
            block.UserData.UserDataAddedEvent -= new UserData.UserDataAddedDelegate(OnUserDataAdded);
            block.UserData.UserDataRemovedEvent -= new UserData.UserDataRemovedDelegate(OnUserDataAdded);
            base.Removed(propertyPage);
        }
#endregion
        private GeoPoint OnGetRefPoint(GeoPointProperty sender)
        {
            return block.RefPoint;
        }
        private void OnSetRefPoint(GeoPointProperty sender, GeoPoint p)
        {
            block.RefPoint = p;
        }
#region ICommandHandler Members
        virtual public bool OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.Explode":
                    if (Frame.ActiveAction is SelectObjectsAction)
                    {
                        SelectObjectsAction soa = Frame.ActiveAction as SelectObjectsAction;
                        soa.SetSelectedObjects(new GeoObjectList());
                        //Application.DoEvents();
                        using (Frame.Project.Undo.UndoFrame)
                        {
                            IGeoObjectOwner addTo = block.Owner;
                            if (addTo == null) addTo = Frame.ActiveView.Model;
                            addTo.Remove(block);
                            GeoObjectList toSelect = block.Decompose();
                            for (int i = 0; i < toSelect.Count; i++)
                            {
                                addTo.Add(toSelect[i]);
                            }

                            soa.SetSelectedObjects(toSelect); // alle Teilobjekte markieren
                        }
                    }
                    return true;
                case "MenuId.SelectedObject.ToBackground":
                    if (ContextMenuSource != null)
                    {
                        block.MoveToBack(ContextMenuSource);
                        subEntries = null;
                        if (propertyPage != null) propertyPage.Refresh(this);
                    }
                    return true;
                case "MenuId.SelectedObject.ToForeground":
                    if (ContextMenuSource != null)
                    {
                        block.MoveToFront(ContextMenuSource);
                        subEntries = null;
                        if (propertyPage != null) propertyPage.Refresh(this);
                    }
                    return true;

            }
            return false;
        }
        virtual public bool OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            switch (MenuId)
            {
                case "MenuId.SelectedObject.ToBackground":
                case "MenuId.SelectedObject.ToForeground":
                case "MenuId.Explode":
                    CommandState.Enabled = true; // naja isses ja immer
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
            return block;
        }
        string IGeoObjectShowProperty.GetContextMenuId()
        {
            return "MenuId.Object.Block";
        }
#endregion

    }
}
