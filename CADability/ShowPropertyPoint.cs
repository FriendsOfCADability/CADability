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
		private readonly Point point;
		private readonly GeoPointProperty locationProperty;
		private IPropertyEntry[] subEntries;
		private IPropertyEntry[] attributeProperties; // Anzeigen für die Attribute (Ebene, Farbe u.s.w)

		public ShowPropertyPoint(Point point, IFrame frame) : base(frame)
		{
			this.point = point;
			locationProperty = new GeoPointProperty(Frame, "Point.Location");
			locationProperty.OnGetValue = () => point.Location;
			locationProperty.OnSetValue = (p) => point.Location = p;
			locationProperty.GeoPointChanged(); // Initialisierung
			locationProperty.ModifyWithMouse += ModifyLocationWithMouse;
			locationProperty.StateChangedEvent += OnStateChanged;

			attributeProperties = point.GetAttributeProperties(Frame);

			resourceIdInternal = "Point.Object";
		}

		private void OnPointDidChange(IGeoObject sender, GeoObjectChange change)
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
		{   // dient dazu, die Hotspots anzuzeigen bzw. zu verstecken wenn die SubProperties
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
		{   // die events müssen in Added angemeldet und in Removed wieder abgemeldet werden,
			// sonst bleibt die ganze ShowProperty für immer an der Linie hängen
			this.point.DidChangeEvent += OnPointDidChange;
			point.UserData.UserDataAddedEvent += OnUserDataAdded;
			point.UserData.UserDataRemovedEvent += OnUserDataAdded;
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
			this.point.DidChangeEvent -= OnPointDidChange;
			point.UserData.UserDataAddedEvent -= OnUserDataAdded;
			point.UserData.UserDataRemovedEvent -= OnUserDataAdded;
			base.Removed(propertyPage);
		}
		#endregion
		private void ModifyLocationWithMouse(IPropertyEntry sender, bool startModifying)
		{
			GeneralGeoPointAction gpa = new GeneralGeoPointAction(locationProperty, point);
			Frame.SetAction(gpa);
		}
		#region ICommandHandler Members

		public virtual bool OnCommand(string menuId)
		{
			switch (menuId)
			{
				case "MenuId.Reverse":
					{
					}

					;
					return true;
			}
			return false;
		}

		public virtual bool OnUpdateCommand(string menuId, CommandState commandState)
		{
			switch (menuId)
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
