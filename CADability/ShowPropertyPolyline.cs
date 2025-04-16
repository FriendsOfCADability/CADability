using CADability.Actions;
using CADability.GeoObject;
using System;
using System.Collections.Generic;

namespace CADability.UserInterface
{
	/// <summary>
	/// Shows the properties of a polyline.
	/// </summary>

	public class ShowPropertyPolyline : PropertyEntryImpl, IDisplayHotSpots, ICommandHandler, IGeoObjectShowProperty
	{
		private readonly Polyline polyline;
		private MultiGeoPointProperty vertexProperty;
		private IPropertyEntry[] subEntries; // abhängig von der Form, also Rechteck, Parallelogramm
		private IPropertyEntry[] attributeProperties; // Anzeigen für die Attribute (Ebene, Farbe u.s.w)
		private GeoPointProperty locationProperty; // wg. hotspot merken 
		private GeoPointProperty locationParallelProperty; // wg. hotspot merken 
		private ShowPropertyHotSpot widthHotSpot;
		private ShowPropertyHotSpot heightHotSpot;
		private GeoVectorHotSpot directionHotSpot;
		private GeoVectorHotSpot directionXParallelHotSpot;
		private GeoVectorHotSpot directionYParallelHotSpot;
		private GeoPointHotSpot sizeHotSpot;
		private IPropertyEntry rectangleProperty; // nur gesetzt, wenn es ein Rechteck ist, die GroupProperty des Rechtecks
		private IPropertyEntry parallelProperty; // nur gesetzt, wenn es ein Rechteck ist, die GroupProperty des Rechtecks
		private class VertexIndexedGeoPoint : IIndexedGeoPoint
		{
			ShowPropertyPolyline showPropertyPolyline; // nach außen
			public VertexIndexedGeoPoint(ShowPropertyPolyline showPropertyPolyline)
			{
				this.showPropertyPolyline = showPropertyPolyline;
			}
			#region IIndexedGeoPoint Members
			public void SetGeoPoint(int Index, GeoPoint ThePoint)
			{
				showPropertyPolyline.polyline.SetPoint(Index, ThePoint);
			}
			public GeoPoint GetGeoPoint(int Index)
			{
				return showPropertyPolyline.polyline.GetPoint(Index);
			}
			public void InsertGeoPoint(int Index, GeoPoint ThePoint)
			{
				showPropertyPolyline.polyline.InsertPoint(Index, ThePoint);
				showPropertyPolyline.vertexProperty.Refresh();
				showPropertyPolyline.vertexProperty.SubEntries[Index].SetFocus();
			}
			public void RemoveGeoPoint(int Index)
			{
				showPropertyPolyline.polyline.RemovePoint(Index);
				showPropertyPolyline.vertexProperty.Refresh();
			}
			public int GetGeoPointCount()
			{
				return showPropertyPolyline.polyline.PointCount;
			}
			bool IIndexedGeoPoint.MayInsert(int Index)
			{
				if (showPropertyPolyline.polyline.IsRectangle || showPropertyPolyline.polyline.IsParallelogram) return false;
				if (Index == 0 || Index == -1) return false;
				return true;
			}
			bool IIndexedGeoPoint.MayDelete(int Index)
			{   // TODO: noch implementieren, nicht weniger als 2 Punkte
				if (showPropertyPolyline.polyline.IsRectangle || showPropertyPolyline.polyline.IsParallelogram) return false;
				return showPropertyPolyline.polyline.PointCount > 2;
			}
			#endregion
		}
		public ShowPropertyPolyline(Polyline polyline, IFrame frame) : base(frame)
		{
			this.polyline = polyline;
			base.resourceIdInternal = "Polyline.Object";
			if (polyline.IsParallelogram) base.resourceIdInternal = "Polyline.Parallel";
			if (polyline.IsRectangle) base.resourceIdInternal = "Polyline.Rectangle";
			InitSubEntries();
		}
		private void InitSubEntries()
		{
			vertexProperty = new MultiGeoPointProperty(new VertexIndexedGeoPoint(this), "Polyline.Vertex", this.Frame);
			vertexProperty.ModifyWithMouseEvent += OnModifyVertexWithMouse;
			vertexProperty.GeoPointSelectionChangedEvent += OnPointsSelectionChanged;
			(vertexProperty as IPropertyEntry).PropertyEntryChangedStateEvent += OnVertexPropertyStateChanged;
			vertexProperty.PrependContextMenue = MenuResource.LoadMenuDefinition("MenuId.Path.Vertex", false, this);

			if (polyline.IsRectangle)
			{
				subEntries = new IPropertyEntry[2];
				locationProperty = new GeoPointProperty(Frame, "Rectangle.Location");
				locationProperty.OnGetValue = () => polyline.RectangleLocation;
				locationProperty.OnSetValue = (GeoPoint p) => polyline.RectangleLocation = p;
				locationProperty.ModifyWithMouse += ModifyLocationWithMouse;

				LengthProperty width = new LengthProperty(Frame, "Rectangle.Width");
				width.OnGetValue = () => polyline.RectangleWidth;
				width.OnSetValue = l =>
				{
					if (!Precision.IsNull(l))
						polyline.RectangleWidth = l;
				};

				width.ModifyWithMouse += ModifyWidthWithMouse;
				widthHotSpot = new ShowPropertyHotSpot(width, Frame);
				widthHotSpot.Position = polyline.RectangleLocation + 0.5 * polyline.ParallelogramSecondaryDirection + polyline.ParallelogramMainDirection;
				widthHotSpot.PositionChangedEvent += OnWidthPositionChanged;

				LengthProperty height = new LengthProperty(Frame, "Rectangle.Height");
				height.OnGetValue = () => polyline.RectangleHeight;
				height.OnSetValue = l =>
				{
					if (!Precision.IsNull(l))
						polyline.RectangleHeight = l;
				};
				height.ModifyWithMouse += ModifyHeightWithMouse;
				heightHotSpot = new ShowPropertyHotSpot(height, Frame);
				heightHotSpot.Position = polyline.RectangleLocation + polyline.ParallelogramSecondaryDirection + 0.5 * polyline.ParallelogramMainDirection;
				heightHotSpot.PositionChangedEvent += OnHeightPositionChanged;

				GeoVectorProperty direction = new GeoVectorProperty(Frame, "Rectangle.Direction");
				direction.OnGetValue = () => polyline.ParallelogramMainDirection;
				direction.OnSetValue = v =>
				{
					if (Precision.IsNullVector(v))
						return;
					polyline.ParallelogramMainDirection = v; // übernimmt nicht die Länge!
				};
				direction.ModifyWithMouse += ModifyDirectionXWithMouse;
				directionHotSpot = new GeoVectorHotSpot(direction);
				directionHotSpot.Position = polyline.RectangleLocation + polyline.ParallelogramMainDirection;

				sizeHotSpot = new GeoPointHotSpot(polyline.RectangleLocation + polyline.ParallelogramSecondaryDirection + polyline.ParallelogramMainDirection);
				sizeHotSpot.StartDragHotspotEvent += OnSizeHotSpotStartDragHotspot;

				rectangleProperty = new GroupProperty("Polyline.Rectangle", new IPropertyEntry[] { locationProperty, width, height, direction });
				subEntries[0] = rectangleProperty;
				rectangleProperty.PropertyEntryChangedStateEvent += OnRectanglePropertyStateChanged;

				subEntries[1] = vertexProperty;
			}
			else

				if (polyline.IsParallelogram)
			{
				subEntries = new IPropertyEntry[2];
				locationParallelProperty = new GeoPointProperty(Frame, "Rectangle.Location");
				locationParallelProperty.OnGetValue = () => polyline.ParallelogramLocation;
				locationParallelProperty.OnSetValue = p => polyline.ParallelogramLocation = p;
				locationParallelProperty.ModifyWithMouse += ModifyLocationWithMouse;
				
				GeoVectorProperty directionXParallel = new GeoVectorProperty(Frame, "Parallelogram.DirectionX");
				directionXParallel.OnGetValue = () => polyline.ParallelogramMainDirection;
				directionXParallel.OnSetValue = v =>
				{
					if (v.IsNullVector()) 
						return;
					if (!Precision.SameDirection(v, polyline.ParallelogramSecondaryDirection, false))
						polyline.SetParallelogram(polyline.ParallelogramLocation, v, polyline.ParallelogramSecondaryDirection);
				};
				directionXParallel.ModifyWithMouse += ModifyDirectionXParallelWithMouse;
				directionXParallelHotSpot = new GeoVectorHotSpot(directionXParallel);
				directionXParallelHotSpot.Position = polyline.ParallelogramLocation + polyline.ParallelogramMainDirection;

				GeoVectorProperty directionYParallel = new GeoVectorProperty(Frame, "Parallelogram.DirectionY");
				directionYParallel.OnGetValue = () => polyline.ParallelogramSecondaryDirection;
				directionYParallel.OnSetValue = v =>
				{
					if (v.IsNullVector()) 
						return;
					if (!Precision.SameDirection(v, polyline.ParallelogramMainDirection, false))
						polyline.ParallelogramSecondaryDirection = v; // übernimmt nicht die Länge!
				};
				directionYParallel.ModifyWithMouse += ModifyDirectionYParallelWithMouse;
				directionYParallelHotSpot = new GeoVectorHotSpot(directionYParallel);
				directionYParallelHotSpot.Position = polyline.ParallelogramLocation + polyline.ParallelogramSecondaryDirection;

				sizeHotSpot = new GeoPointHotSpot(polyline.ParallelogramLocation + polyline.ParallelogramSecondaryDirection + polyline.ParallelogramMainDirection);
				sizeHotSpot.StartDragHotspotEvent += OnSizeHotSpotStartDragHotspot;

				parallelProperty = new GroupProperty("Polyline.Parallel", new IPropertyEntry[] { locationParallelProperty, directionXParallel, directionYParallel });
				subEntries[0] = parallelProperty;
				parallelProperty.PropertyEntryChangedStateEvent += OnParallelPropertyStateChanged;

				subEntries[1] = vertexProperty;
			}
			else
			{
				subEntries = new IPropertyEntry[1];
				subEntries[0] = vertexProperty;
			}
			attributeProperties = polyline.GetAttributeProperties(Frame);
		}

		void OnSizeHotSpotStartDragHotspot(GeoPointHotSpot sender)
		{
			GeneralGeoPointAction gpa = new GeneralGeoPointAction(polyline.ParallelogramLocation + polyline.ParallelogramSecondaryDirection + polyline.ParallelogramMainDirection, polyline);
			gpa.SetGeoPointEvent += SetSizeGeoPoint;
			Frame.SetAction(gpa);
		}

		void SetSizeGeoPoint(GeneralGeoPointAction sender, GeoPoint newValue)
		{
			if (polyline.IsRectangle)
			{
				double w = Geometry.DistPL(newValue, polyline.RectangleLocation, polyline.ParallelogramSecondaryDirection);
				double h = Geometry.DistPL(newValue, polyline.RectangleLocation, polyline.ParallelogramMainDirection);
				if (Math.Abs(w) > Precision.eps && Math.Abs(h) > Precision.eps)
				{
					polyline.RectangleWidth = w;
					polyline.RectangleHeight = h;
				}
			}
			else if (polyline.IsParallelogram)
			{
				Plane pln = polyline.GetPlane();
				GeoPoint2D fix = pln.Project(polyline.ParallelogramLocation);
				GeoVector2D dirx = pln.Project(polyline.ParallelogramMainDirection);
				GeoVector2D diry = pln.Project(polyline.ParallelogramSecondaryDirection);
				GeoPoint2D np = pln.Project(newValue);
				GeoPoint2D xp, yp;
				if (Geometry.IntersectLL(fix, dirx, np, diry, out xp) && Geometry.IntersectLL(fix, diry, np, dirx, out yp))
				{
					polyline.ParallelogramWidth = fix | xp;
					polyline.ParallelogramHeight = fix | yp;
				}
			}
		}

		private void PolylineDidChange(IGeoObject sender, GeoObjectChange change)
		{
			if (rectangleProperty != null && !polyline.IsRectangle)
			{   // die Einträge für das Rechteck wegmachen
				rectangleProperty = null;
				subEntries = new IPropertyEntry[1];
				subEntries[0] = vertexProperty;
				base.resourceIdInternal = "Polyline.Object";
				base.propertyPage.Refresh(this);
				base.propertyPage.OpenSubEntries(vertexProperty, true);
			}
			if (parallelProperty != null && !polyline.IsParallelogram)
			{   // die Einträge für das Parallelogramm wegmachen
				parallelProperty = null;
				subEntries = new IPropertyEntry[1];
				subEntries[0] = vertexProperty;
				base.resourceIdInternal = "Polyline.Object";
				base.propertyPage.Refresh(this);
				base.propertyPage.OpenSubEntries(vertexProperty, true);
			}
			if (vertexProperty.SubEntries.Length != polyline.Vertices.Length) vertexProperty.Refresh();
			for (int i = 0; i < subEntries.Length; ++i)
			{
				propertyPage.Refresh(subEntries[i] as IPropertyEntry);
			}
			//if(Change.MethodOrPropertyName != "SetPoint")
			//    vertexProperty.ShowOpen(false); // warum? geht ja sonst immer zu
			if (polyline.IsRectangle && rectangleProperty != null)
			{
				widthHotSpot.Position = polyline.RectangleLocation + 0.5 * polyline.ParallelogramSecondaryDirection + polyline.ParallelogramMainDirection;
				heightHotSpot.Position = polyline.RectangleLocation + polyline.ParallelogramSecondaryDirection + 0.5 * polyline.ParallelogramMainDirection;
				directionHotSpot.Position = polyline.RectangleLocation + polyline.ParallelogramMainDirection;
				sizeHotSpot.Position = polyline.RectangleLocation + polyline.ParallelogramMainDirection + polyline.ParallelogramSecondaryDirection;
			}
			if (polyline.IsParallelogram && parallelProperty != null)
			{
				directionXParallelHotSpot.Position = polyline.ParallelogramLocation + polyline.ParallelogramMainDirection;
				directionYParallelHotSpot.Position = polyline.ParallelogramLocation + polyline.ParallelogramSecondaryDirection;
				sizeHotSpot.Position = polyline.ParallelogramLocation + polyline.ParallelogramMainDirection + polyline.ParallelogramSecondaryDirection;
			}
		}

		#region IShowPropertyImpl Overrides
		public override PropertyEntryType Flags => PropertyEntryType.ContextMenu | PropertyEntryType.Selectable | PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries;
		public override MenuWithHandler[] ContextMenu
		{
			get
			{
				List<MenuWithHandler> items = new List<MenuWithHandler>(MenuResource.LoadMenuDefinition("MenuId.Object.Polyline", false, this));
				CreateContextMenueEvent?.Invoke(this, items);
				polyline.GetAdditionalContextMenue(this, Frame, items);
				return items.ToArray();
			}
		}
		public override IPropertyEntry[] SubItems
		{
			get
			{
				return PropertyEntryImpl.Concat(subEntries, attributeProperties);
			}
		}
		public override void Added(IPropertyPage page)
		{
			base.Added(page);
			polyline.UserData.UserDataAddedEvent += OnUserDataAdded;
			polyline.UserData.UserDataRemovedEvent += OnUserDataAdded;
			polyline.DidChangeEvent += PolylineDidChange;
		}
		public override void Removed(IPropertyPage page)
		{
			polyline.DidChangeEvent -= PolylineDidChange;
			polyline.UserData.UserDataAddedEvent -= OnUserDataAdded;
			polyline.UserData.UserDataRemovedEvent -= OnUserDataAdded;
			base.Removed(page);
		}
		void OnUserDataAdded(string name, object value)
		{
			attributeProperties = polyline.GetAttributeProperties(Frame);
			propertyPage.Refresh(this);
		}

		#endregion
		#region IDisplayHotSpots Members

		public event CADability.HotspotChangedDelegate HotspotChangedEvent;
		public void ReloadProperties()
		{
		}

		#endregion


		private bool OnModifyVertexWithMouse(IPropertyEntry sender, int index)
		{
			GeneralGeoPointAction gpa = new GeneralGeoPointAction(polyline.GetPoint(index), polyline);
			gpa.UserData.Add("Mode", "Point");
			gpa.UserData.Add("Index", index);
			gpa.GeoPointProperty = sender as GeoPointProperty;
			gpa.SetGeoPointEvent += OnSetPoint;
			Frame.SetAction(gpa);
			return false;
		}

		private void OnSetPoint(GeneralGeoPointAction sender, GeoPoint newValue)
		{
			int index = (int)sender.UserData.GetData("Index");
			polyline.SetPoint(index, newValue);
		}

		private void OnPointsSelectionChanged(GeoPointProperty sender, bool isSelected)
		{
			if (HotspotChangedEvent != null)
			{
				HotspotChangedEvent(sender, isSelected ? HotspotChangeMode.Selected : HotspotChangeMode.Deselected);
			}
		}
		private void ModifyLocationWithMouse(IPropertyEntry sender, bool StartModifying)
		{
			GeneralGeoPointAction gpa = new GeneralGeoPointAction(sender as GeoPointProperty, polyline);
			Frame.SetAction(gpa);
		}

		private void ModifyWidthWithMouse(IPropertyEntry sender, bool StartModifying)
		{
			GeneralLengthAction gpa = new GeneralLengthAction(sender as LengthProperty, polyline.RectangleLocation, polyline.ParallelogramSecondaryDirection, polyline);
			Frame.SetAction(gpa);
		}

		private void ModifyHeightWithMouse(IPropertyEntry sender, bool StartModifying)
		{
			GeneralLengthAction gpa = new GeneralLengthAction(sender as LengthProperty, polyline.RectangleLocation, polyline.ParallelogramMainDirection, polyline);
			Frame.SetAction(gpa);
		}

		private void ModifyDirectionXWithMouse(IPropertyEntry sender, bool StartModifying)
		{
			GeneralGeoVectorAction gva = new GeneralGeoVectorAction(sender as GeoVectorProperty, polyline.RectangleLocation, polyline);
			Frame.SetAction(gva);
		}

		private void ModifyDirectionXParallelWithMouse(IPropertyEntry sender, bool StartModifying)
		{
			GeneralGeoVectorAction gva = new GeneralGeoVectorAction(sender as GeoVectorProperty, polyline.ParallelogramLocation, polyline);
			Frame.SetAction(gva);
		}

		private void ModifyDirectionYParallelWithMouse(IPropertyEntry sender, bool StartModifying)
		{
			GeneralGeoVectorAction gva = new GeneralGeoVectorAction(sender as GeoVectorProperty, polyline.ParallelogramLocation, polyline);
			Frame.SetAction(gva);
		}


		private void OnWidthPositionChanged(GeoPoint newPosition)
		{   // an dem Hotspot für die Breite wurde gezogen
			GeoPoint foot = Geometry.DropPL(newPosition, polyline.RectangleLocation, polyline.ParallelogramSecondaryDirection);
			GeoVector directionX = newPosition - foot;
			if (!Precision.IsNullVector(directionX))
			{
				polyline.SetRectangle(polyline.RectangleLocation, directionX, polyline.ParallelogramSecondaryDirection);
			}
		}

		private void OnHeightPositionChanged(GeoPoint newPosition)
		{
			GeoPoint foot = Geometry.DropPL(newPosition, polyline.RectangleLocation, polyline.ParallelogramMainDirection);
			GeoVector directionY = newPosition - foot;
			if (!Precision.IsNullVector(directionY))
			{
				polyline.SetRectangle(polyline.RectangleLocation, polyline.ParallelogramMainDirection, directionY);
			}
		}

		private void OnRectanglePropertyStateChanged(IPropertyEntry sender, StateChangedArgs args)
		{   // hier erfahren wir, ob die Rechteckeigenschaften aufgeklappt
			// oder zugeklappt werden
			if (args.EventState == StateChangedArgs.State.OpenSubEntries)
			{
				vertexProperty.ShowOpen(false);
				if (HotspotChangedEvent != null)
				{
					HotspotChangedEvent(locationProperty, HotspotChangeMode.Visible);
					HotspotChangedEvent(widthHotSpot, HotspotChangeMode.Visible);
					HotspotChangedEvent(heightHotSpot, HotspotChangeMode.Visible);
					HotspotChangedEvent(directionHotSpot, HotspotChangeMode.Visible);
					HotspotChangedEvent(sizeHotSpot, HotspotChangeMode.Visible);
				}
			}
			else if (args.EventState == StateChangedArgs.State.CollapseSubEntries)
			{
				if (HotspotChangedEvent != null)
				{
					HotspotChangedEvent(locationProperty, HotspotChangeMode.Invisible);
					HotspotChangedEvent(widthHotSpot, HotspotChangeMode.Invisible);
					HotspotChangedEvent(heightHotSpot, HotspotChangeMode.Invisible);
					HotspotChangedEvent(directionHotSpot, HotspotChangeMode.Invisible);
					HotspotChangedEvent(sizeHotSpot, HotspotChangeMode.Invisible);
				}
			}
		}
		private void OnParallelPropertyStateChanged(IPropertyEntry sender, StateChangedArgs args)
		{   // hier erfahren wir, ob die Rechteckeigenschaften aufgeklappt
			// oder zugeklappt werden
			if (args.EventState == StateChangedArgs.State.OpenSubEntries)
			{
				vertexProperty.ShowOpen(false);
				if (HotspotChangedEvent != null)
				{
					HotspotChangedEvent(locationParallelProperty, HotspotChangeMode.Visible);
					HotspotChangedEvent(directionXParallelHotSpot, HotspotChangeMode.Visible);
					HotspotChangedEvent(directionYParallelHotSpot, HotspotChangeMode.Visible);
					HotspotChangedEvent(sizeHotSpot, HotspotChangeMode.Visible);
				}
			}
			else if (args.EventState == StateChangedArgs.State.CollapseSubEntries)
			{
				if (HotspotChangedEvent != null)
				{
					HotspotChangedEvent(locationParallelProperty, HotspotChangeMode.Invisible);
					HotspotChangedEvent(directionXParallelHotSpot, HotspotChangeMode.Invisible);
					HotspotChangedEvent(directionYParallelHotSpot, HotspotChangeMode.Invisible);
					HotspotChangedEvent(sizeHotSpot, HotspotChangeMode.Invisible);
				}
			}
		}
		private void OnVertexPropertyStateChanged(IPropertyEntry sender, StateChangedArgs args)
		{
			if (args.EventState == StateChangedArgs.State.OpenSubEntries)
			{
				if (rectangleProperty != null && propertyPage != null) propertyPage.OpenSubEntries(rectangleProperty as IPropertyEntry, false);
				if (parallelProperty != null && propertyPage != null) propertyPage.OpenSubEntries(parallelProperty as IPropertyEntry, false);
			}
			if (HotspotChangedEvent != null)
			{
				if (args.EventState == StateChangedArgs.State.OpenSubEntries)
				{
					for (int i = 0; i < vertexProperty.SubEntries.Length; ++i)
					{
						HotspotChangedEvent(vertexProperty.SubEntries[i] as IHotSpot, HotspotChangeMode.Visible);
					}
				}
				else if (args.EventState == StateChangedArgs.State.CollapseSubEntries)
				{
					for (int i = 0; i < vertexProperty.SubEntries.Length; ++i)
					{
						HotspotChangedEvent(vertexProperty.SubEntries[i] as IHotSpot, HotspotChangeMode.Invisible);
					}
				}
			}
		}

		public override void Opened(bool IsOpen)
		{
			base.Opened(IsOpen);
			// das geht komischerweise nicht:
			if (rectangleProperty != null)
			{
				base.propertyPage.OpenSubEntries(rectangleProperty as IPropertyEntry, true);
			}
			else if (parallelProperty != null)
			{
				base.propertyPage.OpenSubEntries(parallelProperty as IPropertyEntry, true);
			}
			else
			{
				base.propertyPage.OpenSubEntries(vertexProperty, true);
			}
		}
		#region ICommandHandler Members

		virtual public bool OnCommand(string MenuId)
		{
			switch (MenuId)
			{
				case "MenuId.Reverse":
					polyline.Reverse();
					return true;
				case "MenuId.CurveSplit":
					Frame.SetAction(new ConstrSplitCurve(polyline));
					return true;
				case "MenuId.Explode":
					if (Frame.ActiveAction is SelectObjectsAction)
					{
						using (Frame.Project.Undo.UndoFrame)
						{
							IGeoObjectOwner addTo = polyline.Owner;
							if (addTo == null) addTo = Frame.ActiveView.Model;
							GeoObjectList toSelect = polyline.Decompose();
							addTo.Remove(polyline);
							for (int i = 0; i < toSelect.Count; ++i)
							{
								addTo.Add(toSelect[i]);
							}
							SelectObjectsAction soa = Frame.ActiveAction as SelectObjectsAction;
							soa.SetSelectedObjects(toSelect); // alle Teilobjekte markieren
						}
					}
					return true;
				case "MenuId.Aequidist":
					Frame.SetAction(new ConstructAequidist(polyline));
					return true;
				case "MenuId.Path.Vertex.StartWithMe":
					{
						if (polyline.IsClosed)
						{
							GeoPointProperty gpp = Frame.ContextMenuSource as GeoPointProperty;
							if (gpp != null)
							{
								if (gpp.UserData.ContainsData("Index"))
								{
									int index = (int)gpp.UserData.GetData("Index");
									polyline.CyclicalPermutation(index);
								}
							}
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
				case "MenuId.Reverse":
					return true;
				//				case "MenuId.Polyline.ToPath":
				case "MenuId.Explode":
					CommandState.Enabled = (Frame.ActiveAction is SelectObjectsAction);
					return true;
				case "MenuId.Path.Vertex.StartWithMe": // kommt nicht dran
					CommandState.Enabled = polyline.IsClosed;
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
			return polyline;
		}
		string IGeoObjectShowProperty.GetContextMenuId()
		{
			return "MenuId.Object.Polyline";
		}
		#endregion
	}
}
