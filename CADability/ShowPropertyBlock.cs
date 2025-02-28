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
		private readonly Block block;
		private IGeoObject contextMenuSource;
		private MultiObjectsProperties multiObjectsProperties;
		private IPropertyEntry[] subEntries;
		private IPropertyEntry[] attributeProperties; // Anzeigen für die Attribute (Ebene, Farbe u.s.w)
		public ShowPropertyBlock(Block blk, IFrame frame) : base(frame)
		{
			block = blk;
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
					GeoPointProperty refPointPro = new GeoPointProperty(Frame, "Block.RefPoint");
					refPointPro.OnGetValue = () => block.RefPoint;
					refPointPro.OnSetValue = p => block.RefPoint = p;
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
								(sp as IGeoObjectShowProperty).CreateContextMenueEvent += OnCreateContextMenueChild;
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
			contextMenuSource = sender.GetGeoObject();
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
		public override void Added(IPropertyPage page)
		{   // die events müssen in Added angemeldet und in Removed wieder abgemeldet werden,
			// sonst bleibt die ganze ShowProperty für immer an der Linie hängen
			block.UserData.UserDataAddedEvent += OnUserDataAdded;
			block.UserData.UserDataRemovedEvent += OnUserDataAdded;
			base.Added(page);
		}
		void OnUserDataAdded(string name, object value)
		{
			this.subEntries = null;
			attributeProperties = block.GetAttributeProperties(Frame);
			propertyPage.Refresh(this);
		}
		public override void Removed(IPropertyPage page)
		{
			block.UserData.UserDataAddedEvent -= OnUserDataAdded;
			block.UserData.UserDataRemovedEvent -= OnUserDataAdded;
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
						SelectObjectsAction soa = Frame.ActiveAction as SelectObjectsAction;
						soa.SetSelectedObjects(new GeoObjectList());
						//Application.DoEvents();
						using (Frame.Project.Undo.UndoFrame)
						{
							IGeoObjectOwner addTo = block.Owner ?? Frame.ActiveView.Model;
							addTo.Remove(block);
							GeoObjectList toSelect = block.Decompose();
							foreach (var t in toSelect)
							{
								addTo.Add(t);
							}

							soa.SetSelectedObjects(toSelect); // alle Teilobjekte markieren
						}
					}
					return true;
				case "MenuId.SelectedObject.ToBackground":
					if (contextMenuSource != null)
					{
						block.MoveToBack(contextMenuSource);
						subEntries = null;
						if (propertyPage != null) propertyPage.Refresh(this);
					}
					return true;
				case "MenuId.SelectedObject.ToForeground":
					if (contextMenuSource != null)
					{
						block.MoveToFront(contextMenuSource);
						subEntries = null;
						if (propertyPage != null) propertyPage.Refresh(this);
					}
					return true;

			}
			return false;
		}
		public virtual bool OnUpdateCommand(string menuId, CommandState commandState)
		{
			switch (menuId)
			{
				case "MenuId.SelectedObject.ToBackground":
				case "MenuId.SelectedObject.ToForeground":
				case "MenuId.Explode":
					commandState.Enabled = true; // naja isses ja immer
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
