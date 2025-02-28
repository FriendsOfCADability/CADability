using CADability.Actions;
using CADability.GeoObject;
using System.Collections.Generic;

namespace CADability.UserInterface
{
    /// <summary>
    /// Shows the properties of a point.
    /// </summary>

    public class ShowPropertyPoint : PropertyEntryImpl, ICommandHandler, IGeoObjectShowProperty, IDisplayHotSpots
    {
        private Point point;
        private GeoPointProperty locationProperty;
        private IPropertyEntry[] subEntries;
        private IPropertyEntry[] attributeProperties; // Anzeigen für die Attribute (Ebene, Farbe u.s.w)

        public ShowPropertyPoint(Point point, IFrame frame): base(frame)
        {
            this.point = point;
            locationProperty = new GeoPointProperty("Point.Location", Frame, true);
            locationProperty.GetGeoPointEvent += new CADability.UserInterface.GeoPointProperty.GetGeoPointDelegate(OnGetLocation);
            locationProperty.SetGeoPointEvent += new CADability.UserInterface.GeoPointProperty.SetGeoPointDelegate(OnSetLocation);
            locationProperty.GeoPointChanged(); // Initialisierung
            locationProperty.ModifyWithMouseEvent += new ModifyWithMouseDelegate(ModifyLocationWithMouse);
            locationProperty.StateChangedEvent += new StateChangedDelegate(OnStateChanged);

            attributeProperties = point.GetAttributeProperties(Frame);

            resourceIdInternal = "Point.Object";
        }

        private void OnPointDidChange(IGeoObject Sender, GeoObjectChange Change)
        {
            locationProperty.GeoPointChanged();
        }

        #region PropertyEntryImpl Overrides
        public override PropertyEntryType Flags => PropertyEntryType.ContextMenu | PropertyEntryType.Selectable | PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries;
        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (subEntries == null)
                {
                    IPropertyEntry[] mainProps = { locationProperty, new NameProperty(this.point, "Name", "Block.Name") };
                    subEntries = PropertyEntryImpl.Concat(mainProps, attributeProperties);
                }
                return subEntries;
            }
        }
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                List<MenuWithHandler> items = new List<MenuWithHandler>(MenuResource.LoadMenuDefinition("MenuId.Object.Point", false, this));
                CreateContextMenueEvent?.Invoke(this, items);
                point.GetAdditionalContextMenue(this, Frame, items);
                return items.ToArray();
            }
        }
        public override void Opened(bool IsOpen)
        {	// dient dazu, die Hotspots anzuzeigen bzw. zu verstecken wenn die SubProperties
            // aufgeklappt bzw. zugeklappt werden. Wenn also mehrere Objekte markiert sind
            // und diese Linie aufgeklappt wird.
            if (HotspotChangedEvent != null)
            {
                if (IsOpen)
                {
                    HotspotChangedEvent(locationProperty, HotspotChangeMode.Visible);
                }
                else
                {
                    HotspotChangedEvent(locationProperty, HotspotChangeMode.Invisible);
                }
            }
            base.Opened(IsOpen);
        }
        public override void Added(IPropertyPage propertyPage)
        {	// die events müssen in Added angemeldet und in Removed wieder abgemeldet werden,
            // sonst bleibt die ganze ShowProperty für immer an der Linie hängen
            this.point.DidChangeEvent += new ChangeDelegate(OnPointDidChange);
            point.UserData.UserDataAddedEvent += new UserData.UserDataAddedDelegate(OnUserDataAdded);
            point.UserData.UserDataRemovedEvent += new UserData.UserDataRemovedDelegate(OnUserDataAdded);
            base.Added(propertyPage);
        }
        void OnUserDataAdded(string name, object value)
        {
            this.subEntries = null;
            attributeProperties = point.GetAttributeProperties(Frame);
            propertyPage.Refresh(this);
        }
        public override void Removed(IPropertyPage propertyPage)
        {
            this.point.DidChangeEvent -= new ChangeDelegate(OnPointDidChange);
            point.UserData.UserDataAddedEvent -= new UserData.UserDataAddedDelegate(OnUserDataAdded);
            point.UserData.UserDataRemovedEvent -= new UserData.UserDataRemovedDelegate(OnUserDataAdded);
            base.Removed(propertyPage);
        }
        #endregion
        private GeoPoint OnGetLocation(GeoPointProperty sender)
        {
            return point.Location;
        }
        private void OnSetLocation(GeoPointProperty sender, GeoPoint p)
        {
            point.Location = p;
        }
        private void ModifyLocationWithMouse(IPropertyEntry sender, bool StartModifying)
        {
            GeneralGeoPointAction gpa = new GeneralGeoPointAction(locationProperty, point);
            Frame.SetAction(gpa);
        }
        #region ICommandHandler Members

        virtual public bool OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.Reverse":
                    {
                    }

                    ;
                    return true;
            }
            return false;
        }

        virtual public bool OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            switch (MenuId)
            {
                case "MenuId.Reverse":
                    {
                    }
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
            return point;
        }
        string IGeoObjectShowProperty.GetContextMenuId()
        {
            return "MenuId.Object.Point";
        }
        #endregion
        #region IDisplayHotSpots Members
        public event CADability.HotspotChangedDelegate HotspotChangedEvent;
        void IDisplayHotSpots.ReloadProperties()
        {
            base.propertyPage.Refresh(this);
        }
        private void OnStateChanged(IShowProperty sender, StateChangedArgs args)
        {
            if (HotspotChangedEvent != null)
            {
                if (args.EventState == StateChangedArgs.State.Selected || args.EventState == StateChangedArgs.State.SubEntrySelected)
                {
                    if (sender == locationProperty)
                    {
                        HotspotChangedEvent(locationProperty, HotspotChangeMode.Selected);
                    }
                }
                else if (args.EventState == StateChangedArgs.State.UnSelected)
                {
                    if (sender == locationProperty)
                    {
                        HotspotChangedEvent(locationProperty, HotspotChangeMode.Deselected);
                    }
                }
            }
        }
        #endregion
    }
}
