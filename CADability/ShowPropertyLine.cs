using CADability.Actions;
using CADability.GeoObject;
using System.Collections.Generic;

namespace CADability.UserInterface
{
    /// <summary>
    /// Shows the properties of a line.
    /// </summary>

    public class ShowPropertyLine : PropertyEntryImpl, IDisplayHotSpots, ICommandHandler, IGeoObjectShowProperty
    {
        private readonly Line line; // the displayed line
        private readonly GeoPointProperty startPointProperty; // display start point, also a hotspot
        private readonly GeoPointProperty endPointProperty; // display end point, also a hotspot
        private readonly LengthProperty lengthProperty; // display length
        private readonly LengthHotSpot lengthHotSpot; // hotspot for length
        private readonly GeoVectorProperty directionProperty; // display direction
        private readonly GeoVectorHotSpot directionHotSpot; // hotspot for direction
        private IPropertyEntry[] subEntries;
        private IPropertyEntry[] attributeProperties; // displays for the attributes (layer, color, etc.)

        public ShowPropertyLine(Line line, IFrame frame) : base(frame)
        {
            this.line = line;

            startPointProperty = new GeoPointProperty(Frame, "Line.StartPoint");
            startPointProperty.OnGetValue = () => line.StartPoint;
            startPointProperty.OnSetValue = (value) => line.StartPoint = value;
            startPointProperty.ModifyWithMouse += ModifyStartPointWithMouse;
            startPointProperty.PropertyEntryChangedStateEvent += OnStateChanged;

            endPointProperty = new GeoPointProperty(Frame, "Line.EndPoint");
            endPointProperty.OnGetValue = () => line.EndPoint;
            endPointProperty.OnSetValue = (value) => line.EndPoint = value;
            endPointProperty.ModifyWithMouse += ModifyEndPointWithMouse;
            endPointProperty.PropertyEntryChangedStateEvent += OnStateChanged;
            lengthProperty = new LengthProperty(Frame, "Line.Length");
            lengthProperty.OnGetValue = () => line.Length;
            lengthProperty.OnSetValue = (value) =>
            {
                if (value != 0.0)
                    line.Length = value;
            };
            lengthProperty.ModifyWithMouse += ModifyLengthWithMouse;
            lengthProperty.PropertyEntryChangedStateEvent += OnStateChanged;
            lengthHotSpot = new LengthHotSpot(lengthProperty);
            lengthHotSpot.Position = line.PointAt(2.0 / 3.0);

            directionProperty = new GeoVectorProperty(Frame, "Line.Direction");
            directionProperty.IsNormedVector = true;
            directionProperty.IsAngle = true;
            directionProperty.OnGetValue = () => line.StartDirection;
            directionProperty.OnSetValue = value =>
            {
                value.Norm();
                line.EndPoint = line.StartPoint + line.Length * value;
            };
            directionProperty.ModifyWithMouse += ModifyDirectionWithMouse;
            directionProperty.PropertyEntryChangedStateEvent += OnStateChanged;
            directionHotSpot = new GeoVectorHotSpot(directionProperty);
            directionHotSpot.Position = line.PointAt(1.0 / 3.0);

            IPropertyEntry[] sp = line.GetAttributeProperties(Frame); // change signature of GetAttributeProperties
            attributeProperties = new IPropertyEntry[sp.Length];
            for (int i = 0; i < sp.Length; i++)
            {
                attributeProperties[i] = sp[i] as IPropertyEntry;
            }

            base.resourceIdInternal = "Line.Object";
        }
        private void OnGeoObjectDidChange(IGeoObject sender, GeoObjectChange change)
        {
            // Called when the line changes, synchronizes the displays
            startPointProperty.Refresh();
            endPointProperty.Refresh();
            lengthProperty.Refresh();
            lengthHotSpot.Position = line.PointAt(2.0 / 3.0); // positioned at 2/3 of the line
            directionProperty.Refresh();
            directionHotSpot.Position = line.PointAt(1.0 / 3.0); // positioned at 1/3 of the line
            if (HotspotChangedEvent != null)
            {
                HotspotChangedEvent(startPointProperty, HotspotChangeMode.Moved);
                HotspotChangedEvent(endPointProperty, HotspotChangeMode.Moved);
                HotspotChangedEvent(lengthHotSpot, HotspotChangeMode.Moved);
                HotspotChangedEvent(directionHotSpot, HotspotChangeMode.Moved);
            }
        }

        #region IShowPropertyImpl Overrides
        public override void Added(IPropertyPage pp)
        {
            // The events must be registered in Added and deregistered in Removed,  
            // otherwise, the entire ShowProperty remains attached to the line forever  
            line.DidChangeEvent += OnGeoObjectDidChange;
            line.UserData.UserDataAddedEvent += OnUserDataAdded;
            line.UserData.UserDataRemovedEvent += OnUserDataAdded;
            base.Added(pp);
            OnGeoObjectDidChange(line, null); // Reactivate the hotspots once in case there was  
                                                    // another intermediate change
        }

        void OnUserDataAdded(string name, object value)
        {
            this.subEntries = null;
            IPropertyEntry[] sp = line.GetAttributeProperties(Frame); // change signature of GetAttributeProperties
            attributeProperties = new IPropertyEntry[sp.Length];
            for (int i = 0; i < sp.Length; i++)
            {
                attributeProperties[i] = sp[i] as IPropertyEntry;
            }
            propertyPage.Refresh(this);
        }
        public override void Removed(IPropertyPage pp)
        {
            line.DidChangeEvent -= OnGeoObjectDidChange;
            line.UserData.UserDataAddedEvent -= OnUserDataAdded;
            line.UserData.UserDataRemovedEvent -= OnUserDataAdded;
            base.Removed(pp);
        }
        public override PropertyEntryType Flags => PropertyEntryType.ContextMenu | PropertyEntryType.Selectable | PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries;
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                List<MenuWithHandler> items = new List<MenuWithHandler>(MenuResource.LoadMenuDefinition("MenuId.Object.Line", false, this));
                CreateContextMenueEvent?.Invoke(this, items);
                line.GetAdditionalContextMenue(this, Frame, items);
                return items.ToArray();
            }
        }
        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (subEntries == null)
                {
                    // Refresh the displays here, as it was not done the first time
                    startPointProperty.GeoPointChanged();
                    endPointProperty.GeoPointChanged();
                    lengthProperty.LengthChanged();
                    directionProperty.GeoVectorChanged();

                    IPropertyEntry[] mainProps = {
                                                     startPointProperty,
                                                     endPointProperty,
                                                     lengthProperty,
                                                     directionProperty
                                                 };
                    subEntries = Concat(mainProps, attributeProperties);
                }
                return subEntries;
            }
        }
        public override void Opened(bool isOpen)
        {
            // Used to show or hide the hotspots when the sub-properties  
            // are expanded or collapsed. This applies when multiple objects  
            // are selected and this line is expanded.
            if (HotspotChangedEvent != null)
            {
                if (isOpen)
                {
                    HotspotChangedEvent(startPointProperty, HotspotChangeMode.Visible);
                    HotspotChangedEvent(endPointProperty, HotspotChangeMode.Visible);
                    HotspotChangedEvent(lengthHotSpot, HotspotChangeMode.Visible);
                    HotspotChangedEvent(directionHotSpot, HotspotChangeMode.Visible);
                }
                else
                {
                    HotspotChangedEvent(startPointProperty, HotspotChangeMode.Invisible);
                    HotspotChangedEvent(endPointProperty, HotspotChangeMode.Invisible);
                    HotspotChangedEvent(lengthHotSpot, HotspotChangeMode.Invisible);
                    HotspotChangedEvent(directionHotSpot, HotspotChangeMode.Invisible);
                }
            }
            base.Opened(isOpen);
        }
        #endregion

        #region IDisplayHotSpots Members
        public event CADability.HotspotChangedDelegate HotspotChangedEvent;
        public void ReloadProperties()
        {
            // TODO:  Add ShowPropertyLine.ReloadProperties implementation
            // ich glaube, hier muss man nix machen, da sich ja nie was ändert, oder?
            // TODO: aus dem IDisplayHotSpots interface entfernen und 
            if (propertyPage != null) propertyPage.Refresh(this);
        }

        #endregion

        #region Events der Properties

        // ModifyXxxWithMouse is reported by IShowProperties when "with the mouse"  
        // is selected in their context menu. Likewise, when dragging a hotspot via  
        // IHotSpot.StartDrag, the corresponding IShowProperty is triggered for this event.  
        // GeneralXxxActions are started, which communicate via IShowProperties.
        private void ModifyStartPointWithMouse(IPropertyEntry sender, bool startModifying)
        {
            GeneralGeoPointAction gpa = new GeneralGeoPointAction(startPointProperty, line);
            Frame.SetAction(gpa);
        }

        private void ModifyEndPointWithMouse(IPropertyEntry sender, bool startModifying)
        {
            GeneralGeoPointAction gpa = new GeneralGeoPointAction(endPointProperty, line);
            Frame.SetAction(gpa);
        }
        private void ModifyLengthWithMouse(IPropertyEntry sender, bool startModifying)
        {
            GeneralLengthAction gla = new GeneralLengthAction(lengthProperty, line.StartPoint, line);
            Frame.SetAction(gla);
        }

        private void ModifyDirectionWithMouse(IPropertyEntry sender, bool startModifying)
        {
            // Triggered either by menu selection in the GeoVectorProperty or by dragging the HotSpot  
            // (which also operates through the GeoVectorProperty). The GeneralGeoVectorAction  
            // works directly via the GeoVectorProperty, so no additional events are needed.
            GeneralGeoVectorAction gva = new GeneralGeoVectorAction(sender as GeoVectorProperty, line.StartPoint, line);
            Frame.SetAction(gva);
        }

        // OnStateChanged is registered equally for all IShowProperties.  
        // Therefore, differentiation based on the "sender" is necessary.  
        // This determines which hotspot should be displayed as the selected hotspot.  
        // For MultiGeoPointProperty, a slightly different approach is required  
        // (see e.g., ShowPropertyBSpline).
        private void OnStateChanged(IPropertyEntry sender, StateChangedArgs args)
        {
            if (HotspotChangedEvent != null)
            {
                if (args.EventState == StateChangedArgs.State.Selected || args.EventState == StateChangedArgs.State.SubEntrySelected)
                {
                    if (sender == startPointProperty)
                    {
                        HotspotChangedEvent(startPointProperty, HotspotChangeMode.Selected);
                    }
                    else if (sender == endPointProperty)
                    {
                        HotspotChangedEvent(endPointProperty, HotspotChangeMode.Selected);
                    }
                    else if (sender == lengthProperty)
                    {
                        HotspotChangedEvent(lengthHotSpot, HotspotChangeMode.Selected);
                    }
                    else if (sender == directionProperty)
                    {
                        HotspotChangedEvent(directionHotSpot, HotspotChangeMode.Selected);
                    }
                }
                else if (args.EventState == StateChangedArgs.State.UnSelected)
                {
                    if (sender == startPointProperty)
                    {
                        HotspotChangedEvent(startPointProperty, HotspotChangeMode.Deselected);
                    }
                    else if (sender == endPointProperty)
                    {
                        HotspotChangedEvent(endPointProperty, HotspotChangeMode.Deselected);
                    }
                    else if (sender == lengthProperty)
                    {
                        HotspotChangedEvent(lengthHotSpot, HotspotChangeMode.Deselected);
                    }
                    else if (sender == directionProperty)
                    {
                        HotspotChangedEvent(directionHotSpot, HotspotChangeMode.Deselected);
                    }
                }
            }
        }
        #endregion

        #region ICommandHandler Members
        bool ICommandHandler.OnCommand(string menuId)
        {
            switch (menuId)
            {
                case "MenuId.Reverse":
                    (line as ICurve).Reverse();
                    return true;
                case "MenuId.CurveSplit":
                    Frame.SetAction(new ConstrSplitCurve(line));
                    return true;
            }
            return false;
        }
        bool ICommandHandler.OnUpdateCommand(string menuId, CommandState commandState)
        {
            switch (menuId)
            {
                case "MenuId.Reverse":
                    commandState.Enabled = true;
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
            return line;
        }
        string IGeoObjectShowProperty.GetContextMenuId()
        {
            return "MenuId.Object.Line";
        }
        #endregion
    }
}


