using CADability.Actions;
using CADability.Attribute;
using CADability.GeoObject;
using CADability.Substitutes;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.IO;
using Wintellect.PowerCollections;
using MouseEventArgs = CADability.Substitutes.MouseEventArgs;
using DragEventArgs = CADability.Substitutes.DragEventArgs;
using MouseButtons = CADability.Substitutes.MouseButtons;
using DragDropEffects = CADability.Substitutes.DragDropEffects;
using Keys = CADability.Substitutes.Keys;
using PaintEventArgs = CADability.Substitutes.PaintEventArgs;
using CheckState = CADability.Substitutes.CheckState;
using static CADability.Projection;
#if WEBASSEMBLY
using CADability.WebDrawing;
using Point = CADability.WebDrawing.Point;
#else
using System.Drawing;
using Point = System.Drawing.Point;
#endif

namespace CADability
{
	public delegate void ScrollPositionChanged(double hRatio, double hPosition, double vRatio, double vPosition);
	public delegate void PaintView(Rectangle Extent, IView View, IPaintTo3D PaintTo3D);
	/// <summary>
	/// Filter mouse messages to the ModelView. return true, if you want to prevent further processing of the mouse message.
	/// </summary>
	/// <param name="sender">Object which issued the event</param>
	/// <param name="e">Event parameters forwarded</param>
	/// <returns>true: prevent further processing, false: let the sender process this event</returns>
	public delegate bool MouseFilterDelegate(Object sender, ref MouseEventArgs e);

	/// <summary>
	/// Interface must be implemented by a view, which is hosted in a CadCanvas
	/// </summary>

	public interface ICanvas
	{
		void Invalidate();
		Rectangle ClientRectangle { get; } // yes, rectangle is part of .NET Core 1.0
		IFrame Frame { get; }
		/// <summary>
		/// Sets the cursor defined by the provided id
		/// </summary>
		string Cursor { get; set; }
		/// <summary>
		/// Gets the paint interface to paint the canvas
		/// </summary>
		IPaintTo3D PaintTo3D { get; }

		event Action<ICanvas> OnPaintDone;
		/// <summary>
		/// Show the provided view in this canvas.
		/// </summary>
		/// <param name="toShow"></param>
		void ShowView(IView toShow);
		/// <summary>
		/// Returns the view, which is connected with this canvas
		/// </summary>
		/// <returns></returns>
		IView GetView();
		/// <summary>
		/// 
		/// </summary>
		/// <param name="mousePosition"></param>
		/// <returns></returns>
		Point PointToClient(Point mousePosition);
		/// <summary>
		/// Shows the provided <paramref name="contextMenu"/> at the provided <paramref name="viewPosition"/> on this canvas
		/// </summary>
		/// <param name="contextMenu"></param>
		/// <param name="viewPosition"></param>
		void ShowContextMenu(MenuWithHandler[] contextMenu, Point viewPosition, System.Action<int> collapsed = null);
		DragDropEffects DoDragDrop(GeoObjectList dragList, DragDropEffects all);
		/// <summary>
		/// Show a tooltip with the provided text.
		/// </summary>
		/// <param name="toDisplay">the text to show or null to hide the tooltip</param>
		void ShowToolTip(string toDisplay);
	}


	public interface IView
	{
		ProjectedModel ProjectedModel { get; }
		Model Model { get; }
		Projection Projection { get; set; }
		ICanvas Canvas { get; }
		void SetCursor(string cursor);
		DragDropEffects DoDragDrop(GeoObjectList dragList, Substitutes.DragDropEffects all);
		void Invalidate(PaintBuffer.DrawingAspect aspect, Rectangle ToInvalidate);
		void InvalidateAll();
		void SetPaintHandler(PaintBuffer.DrawingAspect aspect, PaintView PaintHandler);
		void RemovePaintHandler(PaintBuffer.DrawingAspect aspect, PaintView PaintHandler);
		Rectangle DisplayRectangle { get; }
		void ZoomToRect(BoundingRect World2D);
		SnapPointFinder.DidSnapModes AdjustPoint(Point MousePoint, out GeoPoint WorldPoint, GeoObjectList ToIgnore);
		SnapPointFinder.DidSnapModes AdjustPoint(GeoPoint BasePoint, Point MousePoint, out GeoPoint WorldPoint, GeoObjectList ToIgnore);
		GeoObjectList PickObjects(Point MousePoint, PickMode pickMode);
		IGeoObject LastSnapObject { get; }
		SnapPointFinder.DidSnapModes LastSnapMode { get; }

		IShowProperty GetShowProperties(IFrame Frame);
		string Name { get; }
		event ScrollPositionChanged ScrollPositionChangedEvent;
		void Connect(ICanvas canvas);
		void Disconnect(ICanvas canvas);
		void OnPaint(PaintEventArgs e);
		void OnSizeChanged(Rectangle oldClientRectangle);
		void HScroll(double Position);
		void VScroll(double Position);
		void ZoomDelta(double f);
		void ZoomTotal(double f);

		void OnMouseDown(MouseEventArgs e);
		void OnMouseEnter(System.EventArgs e);
		void OnMouseHover(System.EventArgs e);
		void OnMouseLeave(System.EventArgs e);
		void OnMouseMove(MouseEventArgs e);
		void OnMouseUp(MouseEventArgs e);
		void OnMouseWheel(MouseEventArgs e);
		void OnMouseDoubleClick(MouseEventArgs e);

		void OnDragDrop(DragEventArgs drgevent);
		void OnDragEnter(DragEventArgs drgevent);
		void OnDragLeave(EventArgs e);
		void OnDragOver(DragEventArgs drgevent);

		bool AllowDrop { get; }
		bool AllowDrag { get; }
		bool AllowContextMenu { get; }
		/// <summary>
		/// Currently we support two types "GDI" and "3D"
		/// </summary>
		string PaintType { get; }

		GeoObjectList GetDataPresent(object data);
	}

	internal class VisibleLayers : IShowPropertyImpl, ICommandHandler
	{
		LayerList layerList;
		public delegate void LayerVisibilityChangedDelegate(Layer l, bool isVisible);
		public event LayerVisibilityChangedDelegate LayerVisibilityChangedEvent;
		public delegate bool IsLayerVisibleDelegate(Layer l);
		public event IsLayerVisibleDelegate IsLayerVisibleEvent;
		public VisibleLayers(string resourceId, LayerList layerList)
		{
			base.resourceIdInternal = resourceId;
			this.layerList = layerList;
			layerList.LayerAddedEvent += OnLayerAdded;
			layerList.LayerRemovedEvent += OnLayerRemoved;
		}
		void OnLayerRemoved(LayerList sender, Layer removed)
		{
		}
		void OnLayerAdded(LayerList sender, Layer added)
		{
		}
		#region IShowProperty Members
		/// <summary>
		/// Overrides <see cref="IShowPropertyImpl.EntryType"/>, 
		/// returns <see cref="ShowPropertyEntryType.GroupTitle"/>.
		/// </summary>
		public override ShowPropertyEntryType EntryType
		{
			get
			{
				return ShowPropertyEntryType.GroupTitle;
			}
		}
		/// <summary>
		/// Overrides <see cref="IShowPropertyImpl.LabelType"/>
		/// </summary>
		public override ShowPropertyLabelFlags LabelType
		{
			get
			{
				return ShowPropertyLabelFlags.ContextMenu | ShowPropertyLabelFlags.ContextMenu | ShowPropertyLabelFlags.Selectable;
			}
		}
		/// <summary>
		/// Overrides <see cref="IShowPropertyImpl.SubEntriesCount"/>, 
		/// returns the number of subentries in this property view.
		/// </summary>
		public override int SubEntriesCount
		{
			get
			{
				return SubEntries.Length;
			}
		}
		IShowProperty[] subEntries;
		/// <summary>
		/// Overrides <see cref="IShowPropertyImpl.SubEntries"/>, 
		/// returns the subentries in this property view.
		/// </summary>
		public override IShowProperty[] SubEntries
		{
			get
			{
				if (subEntries == null)
				{
					subEntries = new IShowProperty[layerList.Count];
					for (int i = 0; i < layerList.Count; ++i)
					{
						CheckState checkState;
						if (IsLayerVisibleEvent != null && IsLayerVisibleEvent(layerList[i]))
							checkState = CheckState.Checked;
						else
							checkState = CheckState.Unchecked;
						CheckProperty cp = new CheckProperty("VisibleLayer.Entry", checkState);
						cp.CheckStateChangedEvent += OnCheckStateChanged;
						cp.LabelText = layerList[i].Name;
						subEntries[i] = cp;
					}
				}
				return subEntries;
			}
		}
		public override MenuWithHandler[] ContextMenu
		{
			get
			{
				Frame.ContextMenuSource = this;
				return MenuResource.LoadMenuDefinition("MenuId.VisibleLayer", false, this);

			}
		}
		#endregion
		private void OnCheckStateChanged(string label, CheckState state)
		{
			if (LayerVisibilityChangedEvent != null)
				LayerVisibilityChangedEvent(layerList.Find(label), state == CheckState.Checked);
		}
		#region ICommandHandler Members
		/// <summary>
		/// Implements <see cref="CADability.UserInterface.ICommandHandler.OnCommand (string)"/>
		/// </summary>
		/// <param name="MenuId"></param>
		/// <returns></returns>
		virtual public bool OnCommand(string MenuId)
		{
			switch (MenuId)
			{
				case "MenuId.VisibleLayer.SelectAll":
					if (LayerVisibilityChangedEvent != null)
					{
						for (int i = 0; i < layerList.Count; ++i)
						{
							LayerVisibilityChangedEvent(layerList[i], true);
						}
					}
					subEntries = null;
					if (propertyTreeView != null)
					{
						propertyTreeView.Refresh(this);
						propertyTreeView.OpenSubEntries(this, true);
					}
					return true;
				case "MenuId.VisibleLayer.SelectNone":
					if (LayerVisibilityChangedEvent != null)
					{
						for (int i = 0; i < layerList.Count; ++i)
						{
							LayerVisibilityChangedEvent(layerList[i], false);
						}
					}
					subEntries = null;
					if (propertyTreeView != null)
					{
						propertyTreeView.Refresh(this);
						propertyTreeView.OpenSubEntries(this, true);
					}
					return true;
			}
			return false;
		}

		/// <summary>
		/// Implements <see cref="CADability.UserInterface.ICommandHandler.OnUpdateCommand (string, CommandState)"/>
		/// </summary>
		/// <param name="MenuId"></param>
		/// <param name="CommandState"></param>
		/// <returns></returns>
		virtual public bool OnUpdateCommand(string MenuId, CommandState CommandState)
		{
			// TODO:  Add CheckedAttributes.OnUpdateCommand implementation
			return false;
		}
		void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }

		#endregion
		/// <summary>
		/// Overrides <see cref="CADability.UserInterface.IShowPropertyImpl.Refresh ()"/>
		/// </summary>
		public override void Refresh()
		{
			subEntries = null;
			if (propertyTreeView != null) propertyTreeView.Refresh(this);
		}
	}

	/// <summary>
	/// The ModelView is the three-dimensional presentation of a single <see cref="Model"/>.
	/// </summary>

	public class ModelView : IShowPropertyImpl, ICommandHandler, IView, IActionInputView
	{
		//public event RepaintView RepaintDrawingEvent;
		//public event RepaintView RepaintSelectEvent;
		//public event RepaintView RepaintActiveEvent;
		//public event RepaintView RepaintBackgroundEvent;

		//internal event PaintView PaintDrawingEvent;
		internal event PaintView PaintSelectEvent;
		internal event PaintView PaintActiveEvent;
		public event PaintView PaintBackgroundEvent;
		public event PaintView PaintClearEvent;
		/// <summary>
		/// Which background paint tasks were handled by the PrePaintBackground event (flags, combine with "|")
		/// </summary>
		public enum BackgroungTaskHandled
		{
			/// <summary>
			/// Nothing handled
			/// </summary>
			Nothing = 0x00,
			/// <summary>
			/// Grid was painted
			/// </summary>
			Grid = 0x01,
			/// <summary>
			/// Drawingplane was painted
			/// </summary>
			DrawingPlane = 0x02,
			/// <summary>
			/// Coordinate cross and arrows were painted
			/// </summary>
			CoordCross = 0x04
		}
		/// <summary>
		/// Delegate definition for background painting event. The painting of the coordinate cross and arrows, the grid and the DrawingPlane
		/// can be modified using this event
		/// </summary>
		/// <param name="paintToBackground">Painting engine</param>
		/// <param name="modelView">ModelView issuing the event</param>
		/// <param name="handled">Set flags to indicate which tasks were handled</param>
		public delegate void PaintBackgroundDelegate(IPaintTo3D paintToBackground, ModelView modelView, out BackgroungTaskHandled handled);
		/// <summary>
		/// Event to modify backgroung painting
		/// </summary>
		public event PaintBackgroundDelegate PrePaintBackground;
		/// <summary>
		/// Delegate definition for notification of changes of the view position or direction
		/// </summary>
		/// <param name="modelView">Issuing ModelView</param>
		/// <param name="projection">the new projection</param>
		public delegate void ProjectionChangedDelegate(ModelView modelView, Projection projection);
		/// <summary>
		/// Event for notification of view position and direction changes
		/// </summary>
		public event ProjectionChangedDelegate ProjectionChangedEvent;

		/// <summary>
		/// Provide an event handler for the mouse move message here if you want to manipulate the mouse input to this ModelView
		/// </summary>
		public event MouseFilterDelegate MouseMove;
		/// <summary>
		/// Provide an event handler for the mouse down message here if you want to manipulate the mouse input to this ModelView
		/// </summary>
		public event MouseFilterDelegate MouseDown;
		/// <summary>
		/// Provide an event handler for the mouse up message here if you want to manipulate the mouse input to this ModelView
		/// </summary>
		public event MouseFilterDelegate MouseUp;
		/// <summary>
		/// Provide an event handler for the mouse wheel message here if you want to manipulate the mouse input to this ModelView
		/// </summary>
		public event MouseFilterDelegate MouseWheel;
		/// <summary>
		/// Provide an event handler for the mouse double click message here if you want to manipulate the mouse input to this ModelView
		/// </summary>
		public event MouseFilterDelegate MouseDoubleClick;

		private ProjectedModel projectedModel;
		private Project project;
		private bool zAxisUp;
		internal bool projectedModelNeedsRecalc; // Ansicht wurde mit der Maus gedreht, Quadtree muss berechnet werden
		private static bool UseOpenGl = Settings.GlobalSettings.GetBoolValue("UseOpenGl", true);
		private static double mouseWheelZoomFactor = Settings.GlobalSettings.GetDoubleValue("MouseWheelZoomFactor", 1.1);
		private GeoPoint fixPoint;
		private bool fixPointValid;
		private double distance; // Entfernung vom fixPoint bei perspektivischer Ansicht
		private bool allowDrag;
		private bool allowDrop;
		private bool allowContextMenu;
		private Color? backgroundColor;
		private IGeoObject lastSnapObject;
		private SnapPointFinder.DidSnapModes lastSnapMode;
		private double displayPrecision;
		private BoundingCube additionalExtent;

		private BlockRef dragBlock; // ein Symbol wird aus der Bibliothek per DragDrop plaziert
		public delegate void DisplayChangedDelegate(object sender, DisplayChangeArg displayChangeArg);
		public event DisplayChangedDelegate DisplayChangedEvent;
		private Point lastPanPosition;
		static int dbg = 0; // nur debug!
		private int dbgcnt;
		private bool painting = false; //True during OnPaint. To avoid reentrancy.
		public ModelView(Project project)
		{
			this.project = project;
			base.resourceIdInternal = "ModelView";
			project.LayerList.LayerAddedEvent += OnLayerListLayerAdded;
			project.LayerList.LayerRemovedEvent += OnLayerListLayerRemoved;
			project.LayerList.DidModifyEvent += OnLayerListDidModify;
			// ist wohl fest mit diesem projekt verbunden, wird nie gelöst, also auch keine events abmelden
			// bzw. das loslösen noch implementieren
			projectedModelNeedsRecalc = false;
			dbgcnt = ++dbg;
			zAxisUp = true;
			allowDrag = Settings.GlobalSettings.GetBoolValue("AllowDrag", true);
			allowDrop = Settings.GlobalSettings.GetBoolValue("AllowDrop", true);
			allowContextMenu = Settings.GlobalSettings.GetBoolValue("AllowContextMenu", true);
			displayPrecision = -1.0; // automatisch
			additionalExtent = BoundingCube.EmptyBoundingCube;
		}

		/// <summary>
		/// Sets the precision for the display of non linear entities (e.g. arcs). Choose a small value for high precision.
		/// </summary>
		/// <param name="precision">Maximum deviation from exact position</param>
		public void SetDisplayPrecision(double precision)
		{
			displayPrecision = precision;
			if (precision == 0.0)
			{   // einmalig fest setzen
				double precisionFactor = Settings.GlobalSettings.GetDoubleValue("HighestDisplayPrecision", 1e5);
				displayPrecision = Math.Max(1.0 / Projection.WorldToDeviceFactor, Model.Extent.MaxSide / precisionFactor);
			}
			{
				Invalidate();
			}
		}
		~ModelView()
		{
		}
		void OnLayerListDidModify(object sender, EventArgs args)
		{   // Name eines Layer geändert (z.B.)
			subEntries = null; // alle Properties wegmachen
			if (propertyTreeView != null)
			{
				propertyTreeView.Refresh(this);
			}
		}
		void OnLayerListLayerRemoved(LayerList sender, Layer removed)
		{
			if (projectedModel != null) projectedModel.RemoveVisibleLayer(removed);
			subEntries = null; // alle Properties wegmachen
			if (propertyTreeView != null)
			{
				propertyTreeView.Refresh(this);
			}
		}
		void OnLayerListLayerAdded(LayerList sender, Layer added)
		{
			if (projectedModel != null) projectedModel.AddVisibleLayer(added);
			subEntries = null; // alle Properties wegmachen
			if (propertyTreeView != null)
			{
				propertyTreeView.Refresh(this);
			}
		}
		public ProjectedModel ProjectedModel
		{
			get { return projectedModel; }
			set
			{
				project.SetModelView(value);
				if (projectedModel != null)
				{
					if (canvas != null) projectedModel.Disconnect(canvas.PaintTo3D);
					projectedModel.NeedsRepaintEvent -= ProjectedModelNeedsRepaint;
					projectedModel.Projection.ProjectionChangedEvent -= OnProjectionChanged;
				}
				projectedModel = value;
				int griddisplaymode = Settings.GlobalSettings.GetIntValue("Grid.DisplayMode", 1);
				if (griddisplaymode > 0)
				{
					projectedModel.Projection.Grid.DisplayMode = (Grid.Appearance)(griddisplaymode - 1);
					projectedModel.Projection.Grid.Show = true;
				}
				else
				{
					projectedModel.Projection.Grid.Show = false;
				}
				projectedModel.Projection.Grid.XDistance = Settings.GlobalSettings.GetDoubleValue("Grid.XDistance", 10.0);
				projectedModel.Projection.Grid.YDistance = Settings.GlobalSettings.GetDoubleValue("Grid.YDistance", 10.0);
				if (canvas != null) projectedModel.Connect(canvas.PaintTo3D);
				projectedModel.NeedsRepaintEvent += ProjectedModelNeedsRepaint;
				projectedModel.Projection.ProjectionChangedEvent += OnProjectionChanged;
				if (projectedModel.GetVisibleLayers().Length == 0)
				{   // bei neuen ProjektedModels ist kein Layer sichtbar. Dann alle sichtbar machen
					foreach (Layer l in project.LayerList)
					{
						projectedModel.AddVisibleLayer(l);
					}
				}
				fixPoint = projectedModel.fixPoint;
				distance = projectedModel.distance;
				subEntries = null; // alle Properties wegmachen
				if (propertyTreeView != null)
				{
					propertyTreeView.Refresh(this);
				}
			}
		}

		void OnProjectionChanged(Projection sender, EventArgs args)
		{
			viewDirection = sender.Direction;
			if (projectionDirection != null) projectionDirection.Refresh();
			Model.InvalidateProjectionCache(Projection);
			if (ProjectionChangedEvent != null) ProjectionChangedEvent(this, Projection);
		}

		public string Name
		{
			get
			{
				return projectedModel.Name;
			}
			set
			{
				projectedModel.Name = value;
			}
		}
		public bool ZAxisUp
		{
			get
			{
				return zAxisUp;
			}
			set
			{
				zAxisUp = value;
				if (ZAxisUp && distance == 0.0) // bei Zentral ist Z-Axis immer senkrecht
				{
					ModOp pr = Projection.UnscaledProjection;
					// jetzt noch die Z-Achse einrichten. Die kann senkrecht nach oben oder unten
					// zeigen. Wenn die Z-Achse genau auf den Betrachter zeigt oder genau von ihm weg,
					// dann wird keine Anpassung vorgenommen, weils sonst zu sehr wackelt
					GeoVector haxis = Projection.InverseProjection * GeoVector.XAxis;
					GeoVector vaxis = Projection.InverseProjection * GeoVector.YAxis;
					GeoVector z = pr * GeoVector.ZAxis;
					if (z.x == 0.0 && z.y == 0.0) return;
					if (z.y < 0.0)
					{   // Z-Achse soll nach unten zeigen
						Angle a = new Angle(-GeoVector2D.YAxis, new GeoVector2D(z.x, z.y));
						if (z.x < 0)
						{
							pr = pr * ModOp.Rotate(vaxis ^ haxis, -a.Radian);
						}
						else
						{
							pr = pr * ModOp.Rotate(vaxis ^ haxis, a.Radian);
						}
					}
					else
					{
						Angle a = new Angle(GeoVector2D.YAxis, new GeoVector2D(z.x, z.y));
						if (z.x < 0)
						{
							pr = pr * ModOp.Rotate(vaxis ^ haxis, a.Radian);
						}
						else
						{
							pr = pr * ModOp.Rotate(vaxis ^ haxis, -a.Radian);
						}
					}
					// Fixpunkt bestimmen
					Rectangle clientRect = canvas.ClientRectangle;
					Point clcenter = clientRect.Location;
					clcenter.X += clientRect.Width / 2;
					clcenter.Y += clientRect.Height / 2;
					SetViewDirection(pr, Projection.UnProject(clcenter), false);
				}
			}
		}
		public Color BackgroundColor
		{
			get
			{
				if (!backgroundColor.HasValue)
				{
					backgroundColor = Color.AliceBlue;
					if (Frame != null)
					{
						backgroundColor = Frame.GetColorSetting("Colors.Background", Color.AliceBlue);
					}
					else
					{
						object s = Settings.GlobalSettings.GetValue("Colors.Background");
						if (s != null)
						{
							if (s.GetType() == typeof(Color)) backgroundColor = (Color)s;
							if (s is ColorSetting) backgroundColor = (s as ColorSetting).Color;
						}
					}
				}
				return backgroundColor.Value;
			}
			set
			{
				backgroundColor = value;
			}
		}
		public void RefreshBackground()
		{
			canvas?.Invalidate();
		}
		#region Scroll und Zoom
		public void RecalcScrollPosition()
		{
			Rectangle d = DisplayRectangle;
			BoundingRect e = projectedModel.GetDisplayExtent();
			if (e.IsEmpty() || d.IsEmpty) return;
			e = e * 1.1; // etwas Rand drum rum lassen, Konfigurierbar!!!
			double hPart = d.Width / e.Width; // dieser Anteil wird dargestellt: 1.0: Alles, >1.0 mehr als alles, <1.0 der Anteil
			double hPos;
			if (hPart >= 1.0) hPos = 0.0;
			else hPos = (d.Left - e.Left) / (e.Width - d.Width); // 0.0: ganz links, 1.0 ganz rechts
			double vPart = d.Height / e.Height; // dieser Anteil wird dargestellt: 1.0: Alles, >1.0 mehr als alles, <1.0 der Anteil
			double vPos;
			if (vPart >= 1.0) vPos = 0.0;
			else vPos = 1.0 - (d.Top - e.Bottom) / (e.Height - d.Height); // 0.0: ganz unten, , 1.0 ganz oben
			if (ScrollPositionChangedEvent != null) ScrollPositionChangedEvent(hPart, hPos, vPart, vPos);
		}
		/// <summary>
		/// Implements <see cref="CADability.IView.ZoomToRect (BoundingRect)"/>
		/// </summary>
		/// <param name="visibleRect"></param>
		public void ZoomToRect(BoundingRect visibleRect)
		{
			if (canvas != null)
			{
				Rectangle clientRect = canvas.ClientRectangle;
				projectedModel.ZoomToRect(clientRect, visibleRect);
				canvas.Invalidate();
			}
		}
		public void ForceInvalidateAll()
		{
			projectedModel.ForceRecalc();
		}
		public DragDropEffects DoDragDrop(GeoObjectList dragList, DragDropEffects all)
		{
			return canvas.DoDragDrop(dragList, all);
		}
		public void Invalidate()
		{
			canvas?.Invalidate();
		}
		/// <summary>
		/// DEPRECATED, use <see cref="ZoomTotal(double)"/> instead.
		/// </summary>
		/// <param name="ClientRect"></param>
		/// <param name="Factor"></param>
		public void ZoomToModelExtent(Rectangle ClientRect, double Factor)
		{
			Rectangle clientRect = canvas.ClientRectangle;
			projectedModel.ZoomToModelExtent(ClientRect, Factor);
			canvas?.Invalidate();
			RecalcScrollPosition();
		}
		/// <summary>
		/// DEPRECATED, use <see cref="ZoomTotal(double)"/> instead.
		/// </summary>
		/// <param name="ClientRect"></param>
		/// <param name="Factor"></param>
		public void ZoomTotal(Rectangle ClientRect, double Factor)
		{
			Rectangle clientRect = canvas.ClientRectangle;
			if (clientRect.Width == 0 && clientRect.Height == 0) return;
			projectedModel.ZoomToModelExtent(ClientRect, Factor);
			canvas.Invalidate();
			RecalcScrollPosition();
		}
		/// <summary>
		/// Zoom to the extent of the displayed model. Use 1.1 as a factor to leave some small amound of border
		/// area blank. Use 1.0 to exactely fit into the window
		/// </summary>
		/// <param name="Factor">Additional scaling factor</param>
		public void ZoomTotal(double Factor)
		{
			ZoomToRect(Model.GetExtentForZoomTotal(Projection) * Factor);
		}
		public BoundingRect GetVisibleBoundingRect()
		{
			Rectangle clr = canvas.ClientRectangle;
			return this.Projection.BoundingRectWorld2d(clr.Left, clr.Right, clr.Bottom, clr.Top);
		}
		#endregion
		#region ICommandHandler Members
		bool ICommandHandler.OnCommand(string MenuId)
		{
			switch (MenuId)
			{
				case "MenuId.Zoom.Total":
					{
						ZoomTotal(1.1);
						return true;
					}
				case "MenuId.Repaint":
					{
						projectedModel.ForceRecalc();
						ForceInvalidateAll();
						canvas?.Invalidate();
						return true;
					}
				case "MenuId.ModelView.Show":
					if (Frame.ActiveView != this) base.Frame.ActiveView = this;
					return true;
				case "MenuId.ModelView.Rename":
					propertyTreeView.StartEditLabel(this);
					return true;
				case "MenuId.ModelView.Remove":
					project.RemoveModelView(this);
					if (propertyTreeView != null)
					{
						propertyTreeView.Refresh(propertyTreeView.GetParent(this));
					}
					return true;
			}
			return false;
		}
		bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
		{
			switch (MenuId)
			{
				case "MenuId.ModelView.Show":
					{
						CommandState.Checked = Frame.ActiveView == this;
					}
					return true;
			}
			return false;
		}
		void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
		#endregion
		#region IView Members
		public event ScrollPositionChanged ScrollPositionChangedEvent; // ist explizit schwierig
		void IView.OnPaint(PaintEventArgs e)
		{
			if (!painting)
			{
				painting = true;
				Paint(e);
				painting = false;
			}
		}
		private void Paint(PaintEventArgs e)
		{
			if (projectedModel == null || Projection == null) return; // should never happen
			IPaintTo3D paintTo3D = canvas.PaintTo3D;
			if (paintTo3D != null)
			{
				paintTo3D.MakeCurrent();
				paintTo3D.Clear(BackgroundColor);
				PaintClearEvent?.Invoke(e.ClipRectangle, this, paintTo3D);
				paintTo3D.UseZBuffer(true);
				BoundingCube bc = Model.Extent;
				bc.MinMax(Model.MinExtend);
				// sicherstellen, dass die komplette Rasterebene auch mit angezeigt wird
				BoundingRect ext = BoundingRect.EmptyBoundingRect;
				ext.MinMax(Projection.DrawingPlane.Project(new GeoPoint(bc.Xmin, bc.Ymin, bc.Zmin)));
				ext.MinMax(Projection.DrawingPlane.Project(new GeoPoint(bc.Xmin, bc.Ymin, bc.Zmax)));
				ext.MinMax(Projection.DrawingPlane.Project(new GeoPoint(bc.Xmin, bc.Ymax, bc.Zmin)));
				ext.MinMax(Projection.DrawingPlane.Project(new GeoPoint(bc.Xmin, bc.Ymax, bc.Zmax)));
				ext.MinMax(Projection.DrawingPlane.Project(new GeoPoint(bc.Xmax, bc.Ymin, bc.Zmin)));
				ext.MinMax(Projection.DrawingPlane.Project(new GeoPoint(bc.Xmax, bc.Ymin, bc.Zmax)));
				ext.MinMax(Projection.DrawingPlane.Project(new GeoPoint(bc.Xmax, bc.Ymax, bc.Zmin)));
				ext.MinMax(Projection.DrawingPlane.Project(new GeoPoint(bc.Xmax, bc.Ymax, bc.Zmax)));
				ext.Inflate(ext.Width, ext.Height);
				bc.MinMax(Projection.DrawingPlane.ToGlobal(new GeoPoint2D(ext.Left, ext.Bottom)));
				bc.MinMax(Projection.DrawingPlane.ToGlobal(new GeoPoint2D(ext.Left, ext.Top)));
				bc.MinMax(Projection.DrawingPlane.ToGlobal(new GeoPoint2D(ext.Right, ext.Bottom)));
				bc.MinMax(Projection.DrawingPlane.ToGlobal(new GeoPoint2D(ext.Right, ext.Top)));

				if (!additionalExtent.IsEmpty) bc.MinMax(additionalExtent);

				paintTo3D.SetProjection(Projection, bc);

				double precisionFactor = Settings.GlobalSettings.GetDoubleValue("HighestDisplayPrecision", 1e5);
				// HighestDisplayPrecision ist nicht interaktiv einstellbar, nur mit Programmcode
				// Wenn man weiter hineinzoomt, wird keine weitere Displayliste mehr berechnet
				// Sollte man dokumentieren
				if (displayPrecision > 0) paintTo3D.Precision = Math.Min(displayPrecision, Math.Max(1.0 / Projection.WorldToDeviceFactor, Model.Extent.MaxSide / precisionFactor));
				else paintTo3D.Precision = Math.Max(1.0 / Projection.WorldToDeviceFactor, Model.Extent.MaxSide / precisionFactor);

				try
				{
					if (projectedModel.supressAutoRegeneration) (paintTo3D).DontRecalcTriangulation = true;
					projectedModel.Paint(paintTo3D);
					displayPrecision = paintTo3D.Precision; // keep this precision, even when zooming out
					PaintBackground(paintTo3D);
					paintTo3D.UseZBuffer(false);
					PaintBackgroundEvent?.Invoke(e.ClipRectangle, this, paintTo3D);
					paintTo3D.PaintFaces(PaintTo3D.PaintMode.All);
					if (dragBlock != null)
					{
						dragBlock.PrePaintTo3D(paintTo3D);
						dragBlock.PaintTo3D(paintTo3D);
					}
					if (Settings.GlobalSettings.GetBoolValue("ActionActiveObject.UseZBuffer", true)) paintTo3D.UseZBuffer(true);
					if (PaintActiveEvent != null) PaintActiveEvent(e.ClipRectangle, this, paintTo3D);
					if (PaintSelectEvent != null) PaintSelectEvent(e.ClipRectangle, this, paintTo3D);
					paintTo3D.DontRecalcTriangulation = false;
				}
				catch (PaintTo3DOutOfMemory)
				{
					// hier wirklich Collect aufrufen, damit die OpenGL Listen freigegeben werden
					System.GC.Collect();
					System.GC.WaitForPendingFinalizers(); // nicht entfernen! kein Debug
					paintTo3D.FreeUnusedLists();
				}
				paintTo3D.FinishPaint();
			}
		}
		private void PaintBackground(IPaintTo3D PaintToBackground)
		{
			BackgroungTaskHandled bth = BackgroungTaskHandled.Nothing;
			if (PrePaintBackground != null) PrePaintBackground(PaintToBackground, this, out bth);
			Projection pr = this.Projection;
			bool displayGrid = (bth & BackgroungTaskHandled.Grid) == 0;
			bool displayDrawingPlane = (bth & BackgroungTaskHandled.DrawingPlane) == 0;
			bool displayCoordCross = (bth & BackgroungTaskHandled.CoordCross) == 0;
			ShowGrid(PaintToBackground, displayGrid, displayDrawingPlane);
			Color bckgnd = Color.AliceBlue;
			if (Frame != null)
				bckgnd = Frame.GetColorSetting("Colors.Background", Color.AliceBlue);
			Color infocolor;
			if (bckgnd.GetBrightness() > 0.5) infocolor = Color.Black;
			else infocolor = Color.White;
			if (displayCoordCross)
			{
				PaintCoordCross(PaintToBackground, pr, infocolor, Plane.XYPlane, false);
				if (!Precision.IsEqual(pr.DrawingPlane.Location, GeoPoint.Origin))
				{
					if (bckgnd.GetBrightness() > 0.5) infocolor = Color.DarkGray;
					else infocolor = Color.LightGray;
					PaintCoordCross(PaintToBackground, pr, infocolor, pr.DrawingPlane, true);
				}
			}
		}
		void IView.OnSizeChanged(Rectangle oldRectangle)
		{
			Size newSize = Size.Empty;
			newSize = canvas.ClientRectangle.Size;
			if (newSize.Width > 0 && newSize.Height > 0)
			{
				if (canvas.PaintTo3D != null) canvas.PaintTo3D.Resize(newSize.Width, newSize.Height);
				Rectangle clr = canvas.ClientRectangle;
				BoundingRect oldVisibleRect = this.Projection.BoundingRectWorld2d(oldRectangle.Left, oldRectangle.Right, oldRectangle.Bottom, oldRectangle.Top); // aus GetVisibleBoundingRect()
				if (newSize.Height > 0 && oldVisibleRect.Height > 0.0)
				{
					if (oldVisibleRect.Width / oldVisibleRect.Height != newSize.Width / (double)newSize.Height)
					{
						double w = newSize.Width / (double)newSize.Height * oldVisibleRect.Height;
						double center = oldVisibleRect.GetCenter().x;
						oldVisibleRect.Left = center - w / 2.0;
						oldVisibleRect.Right = center + w / 2.0;
					}
				}
				ZoomToRect(oldVisibleRect);
			}

		}
		void IView.HScroll(double Position)
		{
			Rectangle d = DisplayRectangle;
			BoundingRect e = projectedModel.GetDisplayExtent();
			if (e.IsEmpty() || d.IsEmpty) return;
			e = e * 1.1; // kommt mehrfach vor, konfigurierbar machen!
			double hPart = d.Width / e.Width; // dieser Anteil wird dargestellt: 1.0: Alles, >1.0 mehr als alles, <1.0 der Anteil
			if (hPart >= 1.0) return; // kein Scrollbereich frei, alles abgedeckt
			double NewLeftPos = d.Left - (e.Width - d.Width) * Position;
			int HScrollOffset = (int)(NewLeftPos - e.Left); // Betrag in Pixel
			if (HScrollOffset == 0) return;
			canvas?.Invalidate();
			projectedModel.Scroll(HScrollOffset, 0.0);
			if (HScrollOffset > 0)
			{
				if (DisplayChangedEvent != null) DisplayChangedEvent(this, new DisplayChangeArg(DisplayChangeArg.Reasons.ScrollLeft));
			}
			else
			{
				if (DisplayChangedEvent != null) DisplayChangedEvent(this, new DisplayChangeArg(DisplayChangeArg.Reasons.ScrollRight));
			}
		}
		void IView.VScroll(double Position)
		{
			Rectangle d = DisplayRectangle;
			BoundingRect e = projectedModel.GetDisplayExtent();
			if (e.IsEmpty() || d.IsEmpty) return;
			e = e * 1.1; // kommt mehrfach vor, konfigurierbar machen!
			double vPart = d.Height / e.Height;
			if (vPart >= 1.0) return; // kein Scrollbereich frei, alles abgedeckt
			double NewBottomPos = d.Bottom - (d.Height - e.Height) * Position;
			int VScrollOffset = (int)(NewBottomPos - e.Top); // Betrag in Pixel
			if (VScrollOffset == 0) return;
			canvas?.Invalidate();
			projectedModel.Scroll(0.0, VScrollOffset);
			if (VScrollOffset > 0)
			{
				if (DisplayChangedEvent != null) DisplayChangedEvent(this, new DisplayChangeArg(DisplayChangeArg.Reasons.ScrollUp));
			}
			else
			{
				if (DisplayChangedEvent != null) DisplayChangedEvent(this, new DisplayChangeArg(DisplayChangeArg.Reasons.ScrollDown));
			}
		}
		void IView.OnMouseDown(MouseEventArgs eIn)
		{
			MouseEventArgs e = eIn;
			if (MouseDown != null) if (MouseDown(this, ref e)) return;
			bool doScroll = e.Button == MouseButtons.Middle;
			if (e.Button == MouseButtons.Left && (Frame.UIService.ModifierKeys & Keys.Control) != 0)
				doScroll = true;
			if (doScroll)
			{
				canvas.Cursor = "SizeAll";
				lastPanPosition = new Point(e.X, e.Y);
			}
			canvas.Frame.ActiveView = this;
			canvas.Frame.ActionStack.OnMouseDown(e, this);
		}
		void IView.OnMouseEnter(System.EventArgs e)
		{
			canvas.Frame.ActiveView = this;
			canvas.Frame.ActionStack.OnMouseEnter(e, this);
		}
		void IView.OnMouseHover(System.EventArgs e)
		{
			canvas.Frame.ActiveView = this;
			canvas.Frame.ActionStack.OnMouseHover(e, this);
		}
		void IView.OnMouseLeave(System.EventArgs e)
		{
			canvas.Frame.ActiveView = this;
			canvas.Frame.ActionStack.OnMouseLeave(e, this);
		}
		void IView.OnMouseMove(MouseEventArgs eIn)
		{
			MouseEventArgs e = eIn;
			if (MouseMove != null) if (MouseMove(this, ref e)) return;
			bool doScroll = e.Button == MouseButtons.Middle;
			bool doDirection = false;
			if (e.Button == MouseButtons.Middle && (Frame.UIService.ModifierKeys & Keys.Control) != 0)
			{
				doScroll = false;
				doDirection = true;
			}
			if (doScroll) // && paintToOpenGl != null)
			{
				int HScrollOffset = e.X - lastPanPosition.X;
				int VScrollOffset = e.Y - lastPanPosition.Y;
				canvas?.Invalidate();
				projectedModel.Scroll(HScrollOffset, VScrollOffset);
				if (HScrollOffset > 0)
				{
					if (DisplayChangedEvent != null) DisplayChangedEvent(this, new DisplayChangeArg(DisplayChangeArg.Reasons.ScrollLeft));
				}
				else
				{
					if (DisplayChangedEvent != null) DisplayChangedEvent(this, new DisplayChangeArg(DisplayChangeArg.Reasons.ScrollRight));
				}
				if (VScrollOffset > 0)
				{
					if (DisplayChangedEvent != null) DisplayChangedEvent(this, new DisplayChangeArg(DisplayChangeArg.Reasons.ScrollUp));
				}
				else
				{
					if (DisplayChangedEvent != null) DisplayChangedEvent(this, new DisplayChangeArg(DisplayChangeArg.Reasons.ScrollDown));
				}
				lastPanPosition = new Point(e.X, e.Y);
			}
			if (doDirection) // && paintToOpenGl != null)
			{
				int HOffset = e.X - lastPanPosition.X;
				int VOffset = e.Y - lastPanPosition.Y;
				if (VOffset != 0 || HOffset != 0)
				{
					//if (Math.Abs(VOffset) > Math.Abs(HOffset)) HOffset = 0;
					//else VOffset = 0;
					// bringt keine Vorteile
					lastPanPosition = new Point(e.X, e.Y);
					if (distance == 0.0)
					{
						GeoVector haxis = Projection.InverseProjection * GeoVector.XAxis;
						GeoVector vaxis = Projection.InverseProjection * GeoVector.YAxis;
						ModOp mh = ModOp.Rotate(vaxis, SweepAngle.Deg(HOffset / 5.0));
						ModOp mv = ModOp.Rotate(haxis, SweepAngle.Deg(VOffset / 5.0));

						ModOp project = Projection.UnscaledProjection * mv * mh;
						// jetzt noch die Z-Achse einrichten. Die kann senkrecht nach oben oder unten
						// zeigen. Wenn die Z-Achse genau auf den Betrachter zeigt oder genau von ihm weg,
						// dann wird keine Anpassung vorgenommen, weils sonst zu sehr wackelt
						GeoVector z = project * GeoVector.ZAxis;
						if (ZAxisUp)
						{
							const double mindeg = 0.05; // nur etwas aufrichten, aber in jedem Durchlauf
							if (z.y < -0.1)
							{   // Z-Achse soll nach unten zeigen
								Angle a = new Angle(-GeoVector2D.YAxis, new GeoVector2D(z.x, z.y));
								if (a.Radian > mindeg) a.Radian = mindeg;
								if (a.Radian < -mindeg) a.Radian = -mindeg;
								if (z.x < 0)
								{
									project = project * ModOp.Rotate(vaxis ^ haxis, -a.Radian);
								}
								else
								{
									project = project * ModOp.Rotate(vaxis ^ haxis, a.Radian);
								}
								z = project * GeoVector.ZAxis;
							}
							else if (z.y > 0.1)
							{
								Angle a = new Angle(GeoVector2D.YAxis, new GeoVector2D(z.x, z.y));
								if (a.Radian > mindeg) a.Radian = mindeg;
								if (a.Radian < -mindeg) a.Radian = -mindeg;
								if (z.x < 0)
								{
									project = project * ModOp.Rotate(vaxis ^ haxis, a.Radian);
								}
								else
								{
									project = project * ModOp.Rotate(vaxis ^ haxis, -a.Radian);
								}
								z = project * GeoVector.ZAxis;
							}
						}
						// Fixpunkt bestimmen
						Point clcenter = canvas.ClientRectangle.Location;
						clcenter.X += canvas.ClientRectangle.Width / 2;
						clcenter.Y += canvas.ClientRectangle.Height / 2;
						// ium Folgenden ist Temporary auf true zu setzen und ein Mechanismus zu finden
						// wie der QuadTree von ProjektedModel berechnet werden soll
						//GeoVector newDirection = mv * mh * Projection.Direction;
						//GeoVector oldDirection = Projection.Direction;
						//GeoVector perp = oldDirection ^ newDirection;
						if (fixPointValid)
							SetViewDirection(project, fixPoint, true);
						else
						{
							// find a good fixpoint for the rotation of the view:
							GeoPoint fp = Projection.UnProject(clcenter);
							PickArea pa = Projection.GetPickSpace(new Rectangle(clcenter.Y - 5, clcenter.Y - 5, 10, 10));
							double pd = Model.GetPickDistance(pa, null);
							if (pd != double.MaxValue) fp = pa.FrontCenter + pd * pa.Direction; // the point in the center of the screen, when there is something
							else
							{   // when there is nothing in the center, use the point on the drawing plane
								Axis pb = Projection.PointBeam(clcenter);
								try
								{
									GeoPoint2D dpp = Projection.DrawingPlane.Intersect(pb);
									fp = Projection.DrawingPlane.ToGlobal(dpp);
								}
								catch (PlaneException) { }
							}
							SetViewDirection(project, fp, true);
						}
					}
					else
					{
						GeoVector haxis = Projection.horizontalAxis;
						GeoVector vaxis = Projection.verticalAxis;
						ModOp mh = ModOp.Rotate(vaxis, SweepAngle.Deg(-HOffset / 5.0));
						ModOp mv = ModOp.Rotate(haxis, SweepAngle.Deg(-VOffset / 5.0));
						if (Precision.SameDirection(Projection.Direction, GeoVector.ZAxis, false))
						{
							mh = ModOp.Identity;
							mv = ModOp.Rotate(GeoVector.XAxis, SweepAngle.Deg(-VOffset / 5.0));
						}
						GeoVector viewDirection = mv * mh * Projection.Direction;
						//System.Diagnostics.Trace.WriteLine("viewDirection: " + viewDirection.ToString());
						GeoPoint fromHere;
						if (fixPointValid) fromHere = fixPoint - distance * viewDirection;
						else fromHere = GeoPoint.Origin - distance * viewDirection;
						ProjectedModel.SetViewDirection(fromHere, viewDirection, fixPoint, false);

						ForceInvalidateAll();
						canvas?.Invalidate();
						//if (!mouseIsDown)
						//{
						//    RecalcScrollPosition();
						//    projectedModel.RecalcAll(false);
						//}
					}
					if (Math.Abs(HOffset) > Math.Abs(VOffset))
					{
						if (HOffset > 0) canvas.Cursor = "PanEast";
						else canvas.Cursor = "PanWest";
					}
					else
					{
						if (VOffset > 0) canvas.Cursor = "PanSouth";
						else canvas.Cursor = "PanNorth";
					}
					projectedModelNeedsRecalc = true;
				}
				if (DisplayChangedEvent != null) DisplayChangedEvent(this, new DisplayChangeArg(DisplayChangeArg.Reasons.Unknown));

			}
			else
			{
				if (projectedModelNeedsRecalc)
				{
					projectedModelNeedsRecalc = false;
					projectedModel.RecalcAll(false);
					RecalcScrollPosition();
					if (DisplayChangedEvent != null) DisplayChangedEvent(this, new DisplayChangeArg(DisplayChangeArg.Reasons.Unknown));
				}
			}
			if (!doScroll && !doDirection)
			{
				// condorCtrl.Frame.ActiveView = this; // activeview soll sich nur bei Klick ändern
				canvas.Frame.ActionStack.OnMouseMove(e, this);
			}
		}
		void IView.OnMouseUp(MouseEventArgs eIn)
		{
			MouseEventArgs e = eIn;
			if (MouseUp != null) if (MouseUp(this, ref e)) return;
			IFrame frame = canvas.Frame;
			if (e.Button == MouseButtons.Middle && (Frame.UIService.ModifierKeys & Keys.Control) != 0 && (Frame.UIService.ModifierKeys & Keys.Shift) != 0)
			{
				GeoPoint wp;
				SnapPointFinder.SnapModes oldSnapMode = frame.SnapMode;
				frame.SnapMode = SnapPointFinder.SnapModes.SnapToObjectPoint;
				SnapPointFinder.DidSnapModes dsm = (this as IView).AdjustPoint(e.Location, out wp, null);
				frame.SnapMode = oldSnapMode;
				if (dsm != SnapPointFinder.DidSnapModes.DidNotSnap)
				{
					fixPoint = wp;
					fixPointValid = true;
				}
				else
				{
					fixPointValid = false;
				}
				projectedModel.fixPoint = fixPoint;
			}
			frame.ActiveView = this;
			Frame.ActionStack.OnMouseUp(e, this);

			// folgenden Block habe ich hinten angestellt wg. ViewPointAction
			if (projectedModelNeedsRecalc)
			{
				projectedModelNeedsRecalc = false;
				projectedModel.RecalcAll(false);
				RecalcScrollPosition();
			}
		}
		void IView.OnMouseWheel(MouseEventArgs eIn)
		{
			MouseEventArgs e = eIn;
			if (MouseWheel != null) if (MouseWheel(this, ref e)) return;

			double Factor;
			if (e.Delta > 0) Factor = 1.0 / mouseWheelZoomFactor;
			else if (e.Delta < 0) Factor = mouseWheelZoomFactor;
			else return;
			BoundingRect rct = GetVisibleBoundingRect();
			Point p = new Point(e.X, e.Y);
			p = canvas.PointToClient(Frame.UIService.CurrentMousePosition);
			GeoPoint2D p2 = this.Projection.PointWorld2D(p);
			rct.Right = p2.x + (rct.Right - p2.x) * Factor;
			rct.Left = p2.x + (rct.Left - p2.x) * Factor;
			rct.Bottom = p2.y + (rct.Bottom - p2.y) * Factor;
			rct.Top = p2.y + (rct.Top - p2.y) * Factor;
			this.ZoomToRect(rct);
			if (DisplayChangedEvent != null) DisplayChangedEvent(this, new DisplayChangeArg(DisplayChangeArg.Reasons.ZoomIn));
		}
		void IView.OnMouseDoubleClick(MouseEventArgs eIn)
		{
			MouseEventArgs e = eIn;
			if (MouseDoubleClick != null) if (MouseDoubleClick(this, ref e)) return;
		}
		void IView.ZoomDelta(double f)
		{
			double Factor = f;
			BoundingRect rct = GetVisibleBoundingRect();
			Rectangle clr = canvas.ClientRectangle;
			Point p = new Point((clr.Left + clr.Right) / 2, (clr.Bottom + clr.Top) / 2);
			GeoPoint2D p2 = this.Projection.PointWorld2D(p);
			rct.Right = p2.x + (rct.Right - p2.x) * Factor;
			rct.Left = p2.x + (rct.Left - p2.x) * Factor;
			rct.Bottom = p2.y + (rct.Bottom - p2.y) * Factor;
			rct.Top = p2.y + (rct.Top - p2.y) * Factor;
			this.ZoomToRect(rct);
		}
		void IView.OnDragDrop(DragEventArgs drgevent)
		{
			IFrame Frame = canvas.Frame;
			//IFrameInternal FrameInternal = canvas.Frame as IFrameInternal;
			//if (FrameInternal.FilterDragDrop(drgevent)) return;
			if (dragBlock != null)
			{
				if ((drgevent.AllowedEffect & DragDropEffects.Move) > 0 && (drgevent.KeyState & 8) == 0)
					drgevent.Effect = DragDropEffects.Move;
				else
					drgevent.Effect = DragDropEffects.Copy;
				Point p = canvas.PointToClient(new Point(drgevent.X, drgevent.Y));
				GeoPoint newRefGP = projectedModel.Projection.DrawingPlanePoint(p);
				Block toDrop = dragBlock.ReferencedBlock;
				ModOp mop = ModOp.Translate(newRefGP - toDrop.RefPoint);
				toDrop.Modify(mop);
				for (int i = toDrop.Count - 1; i >= 0; i--)
				{
					IGeoObject go = toDrop.Child(i);
					if (go.Style != null && go.Style.Name == "CADability.EdgeStyle")
					{
						Layer l = go.Layer;
						go.Style = Frame.Project.StyleList.Current;
						// Layer wieder setzen? Ich denke nicht!
					}
					AttributeListContainer.UpdateObjectAttrinutes(Frame.Project, go);
				}
				bool move = false;
				if (drgevent.Effect == DragDropEffects.Move)
				{
					if (toDrop.UserData.Contains("DragGuid") && Model.UserData.Contains("DragGuid"))
					{
						Guid g1 = (Guid)toDrop.UserData.GetData("DragGuid");
						Guid g2 = (Guid)Model.UserData.GetData("DragGuid");
						if (g1 == g2)
						{
							move = true;
							Model.UserData.Add("DragGuid:" + g1.ToString(), mop); // modop an SelectObjectsAction weitergeben
						}
					}
				}
				if (!move)
				{

					Model.Add(toDrop.Children);
				}
				dragBlock = null;
				canvas?.Invalidate();

			}

		}
		void IView.OnDragLeave(EventArgs e)
		{
			IFrame Frame = canvas.Frame;
			//IFrameInternal FrameInternal = canvas.Frame as IFrameInternal;
			//if (FrameInternal.FilterDragLeave(null)) return;
			dragBlock = null;
			canvas?.Invalidate();


		}
		public void OnDragEnter(DragEventArgs drgevent)
		{
			IFrame Frame = canvas.Frame;
			//IFrameInternal FrameInternal = canvas.Frame as IFrameInternal;
			//FrameInternal.FilterDragEnter(drgevent); // das bolsche Ergebnis wird nicht verwendet. Ist das OK?
			//GeoObjectList importedData = FrameInternal.FilterDragGetData(drgevent);
			//if (importedData == null)
			//{
			GeoObjectList importedData = Frame.UIService.GetDataPresent(drgevent.Data);
			//}
			if (importedData != null)
			{
				Block bl = Block.Construct();

				for (int i = 0; i < importedData.Count; i++)
					bl.Add(importedData[i]);
				if (importedData.UserData.Contains("DragDownPoint"))
					bl.RefPoint = (GeoPoint)importedData.UserData.GetData("DragDownPoint");
				else
				{
					BoundingRect br = importedData.GetExtent(Projection, false, false);
					GeoPoint2D gp = br.GetCenter();
					bl.RefPoint = new GeoPoint(gp.x, gp.y);
				}
				if (importedData.UserData.Contains("DragGuid"))
				{
					bl.UserData.Add("DragGuid", importedData.UserData.GetData("DragGuid"));
				}
				dragBlock = BlockRef.Construct(bl);
				// neue Position ausrechnen
				Point p = canvas.PointToClient(new Point(drgevent.X, drgevent.Y));
				GeoPoint newRefGP = projectedModel.Projection.DrawingPlanePoint(p);

				ModOp mop = ModOp.Translate(newRefGP - dragBlock.RefPoint);
				dragBlock.Modify(mop);
				canvas?.Invalidate();
				if ((drgevent.AllowedEffect & DragDropEffects.Move) > 0 && (drgevent.KeyState & 8) == 0)
					drgevent.Effect = DragDropEffects.Move;
				else
					drgevent.Effect = DragDropEffects.Copy;
			}
		}
		public void OnDragOver(DragEventArgs drgevent)
		{
			IFrame Frame = canvas.Frame;
			//IFrameInternal FrameInternal = canvas.Frame as IFrameInternal;
			//if (FrameInternal.FilterDragOver(drgevent)) return;
			if (dragBlock != null)
			{
				Point p = canvas.PointToClient(new Point(drgevent.X, drgevent.Y));
				GeoPoint newRefGP = projectedModel.Projection.DrawingPlanePoint(p);
				ModOp mop = ModOp.Translate(newRefGP - dragBlock.RefPoint);
				dragBlock.Modify(mop);
				canvas?.Invalidate();
				if ((drgevent.AllowedEffect & DragDropEffects.Move) > 0 && (drgevent.KeyState & 8) == 0)
					drgevent.Effect = DragDropEffects.Move;
				else
					drgevent.Effect = DragDropEffects.Copy;
			}
		}
		private ICanvas canvas;
		void IView.Connect(ICanvas canvas)
		{
			this.canvas = canvas;
			projectedModel?.Connect(canvas.PaintTo3D);
		}
		void IView.Disconnect(ICanvas canvas)
		{
			projectedModel?.Disconnect(canvas.PaintTo3D);
		}
		GeoObjectList IView.GetDataPresent(object data)
		{
			return Frame.UIService.GetDataPresent(data);
		}
		DragDropEffects IView.DoDragDrop(GeoObjectList dragList, DragDropEffects all)
		{
			return canvas.DoDragDrop(dragList, all);
		}
		ProjectedModel IView.ProjectedModel
		{
			get
			{
				return projectedModel;
			}
		}
		ICanvas IView.Canvas
		{
			get { return canvas; }
		}
		string IView.PaintType => "3D";
		void IView.SetCursor(string cursor)
		{

			canvas.Cursor = cursor;
		}
		void IView.Invalidate(PaintBuffer.DrawingAspect aspect, Rectangle ToInvalidate)
		{
			canvas?.Invalidate();
		}
		void IView.InvalidateAll()
		{
			// ForceInvalidateAll(); // das ist definitiv zuviel, bei FeedbackObjekten wird das aufgerufen und es bräuchte nur ein 
			// neuzeichnen der Feedback Objekte
			canvas?.Invalidate();
		}
		void IView.SetPaintHandler(PaintBuffer.DrawingAspect aspect, PaintView paintHandler)
		{
			switch (aspect)
			{
				case PaintBuffer.DrawingAspect.Background: PaintBackgroundEvent += paintHandler; break;
				//case PaintBuffer.DrawingAspect.Drawing: PaintDrawingEvent += paintHandler; break;
				case PaintBuffer.DrawingAspect.Select: PaintSelectEvent += paintHandler; break;
				case PaintBuffer.DrawingAspect.Active: PaintActiveEvent += paintHandler; break;
			}
		}
		void IView.RemovePaintHandler(PaintBuffer.DrawingAspect aspect, PaintView paintHandler)
		{
			switch (aspect)
			{
				case PaintBuffer.DrawingAspect.Background: PaintBackgroundEvent -= paintHandler; break;
				//case PaintBuffer.DrawingAspect.Drawing: PaintDrawingEvent -= paintHandler; break;
				case PaintBuffer.DrawingAspect.Select: PaintSelectEvent -= paintHandler; break;
				case PaintBuffer.DrawingAspect.Active: PaintActiveEvent -= paintHandler; break;
			}
		}
		public Rectangle DisplayRectangle
		{
			get
			{
				return canvas.ClientRectangle;
			}
		}
		public Model Model
		{
			get
			{
				return projectedModel.Model;
			}
			set
			{
				Model m = value;
				Projection pr = null;
				string name = "";
				if (projectedModel != null)
				{
					if (projectedModel.Model == m) return; // nix zu tun
					pr = projectedModel.Projection;
					name = projectedModel.Name;
				}
				else
				{
					pr = new Projection(Projection.StandardProjection.FromTop);
				}
				this.ProjectedModel = new ProjectedModel(m, pr); // setzen der Property regelt den Rest
				this.ProjectedModel.Name = name;

				ForceInvalidateAll();
				canvas?.Invalidate();
			}
		}
		public Projection Projection
		{
			get
			{
				if (projectedModel == null) return null;
				return projectedModel.Projection;
			}
			set
			{
				if (projectedModel != null) projectedModel.Projection = value;
			}
		}
		void IView.ZoomToRect(BoundingRect World2D)
		{
			if (World2D.Width + World2D.Height < 1e-6) return;
			Projection.SetPlacement(DisplayRectangle, World2D);
			(this as IView).Invalidate(PaintBuffer.DrawingAspect.All, DisplayRectangle);
			RecalcScrollPosition();
		}
		SnapPointFinder.DidSnapModes IView.AdjustPoint(Point MousePoint, out GeoPoint WorldPoint, GeoObjectList ToIgnore)
		{
			SnapPointFinder spf = new SnapPointFinder();
			spf.FilterList = this.Frame.Project.FilterList;
			IFrame frame = canvas.Frame;
			spf.Init(MousePoint, projectedModel.Projection, frame.SnapMode, 5);
			spf.SnapMode = frame.SnapMode;
			spf.Snap30 = frame.GetBooleanSetting("Snap.Circle30", false);
			spf.Snap45 = frame.GetBooleanSetting("Snap.Circle45", false);
			spf.SnapLocalOrigin = frame.GetBooleanSetting("Snap.SnapLocalOrigin", false);
			spf.SnapGlobalOrigin = frame.GetBooleanSetting("Snap.SnapGlobalOrigin", false);
			spf.IgnoreList = ToIgnore;
			projectedModel.AdjustPoint(spf);
			WorldPoint = spf.SnapPoint; // ist auch gesetzt, wenn nicht gefangen (gemäß DrawingPlane)
			lastSnapObject = spf.BestObject;
			lastSnapMode = spf.DidSnap;
			return spf.DidSnap;
		}
		SnapPointFinder.DidSnapModes IView.AdjustPoint(GeoPoint BasePoint, Point MousePoint, out GeoPoint WorldPoint, GeoObjectList ToIgnore)
		{
			SnapPointFinder spf = new SnapPointFinder();
			spf.FilterList = this.Frame.Project.FilterList;
			IFrame frame = canvas.Frame;
			spf.Init(MousePoint, projectedModel.Projection, frame.SnapMode, 5, BasePoint);
			spf.SnapMode = frame.SnapMode;
			spf.Snap30 = frame.GetBooleanSetting("Snap.Circle30", false);
			spf.Snap45 = frame.GetBooleanSetting("Snap.Circle45", false);
			spf.SnapLocalOrigin = frame.GetBooleanSetting("Snap.SnapLocalOrigin", false);
			spf.SnapGlobalOrigin = frame.GetBooleanSetting("Snap.SnapGlobalOrigin", false);
			spf.IgnoreList = ToIgnore;
			projectedModel.AdjustPoint(spf);
			WorldPoint = spf.SnapPoint;
			lastSnapObject = spf.BestObject;
			lastSnapMode = spf.DidSnap;
			return spf.DidSnap;
		}
		GeoObjectList IView.PickObjects(Point MousePoint, PickMode pickMode)
		{
			Projection.PickArea pa = Projection.GetPickSpace(new Rectangle(MousePoint.X - 5, MousePoint.Y - 5, 10, 10));
			return Model.GetObjectsFromRect(pa, new Set<Layer>(projectedModel.GetVisibleLayers()), pickMode, null);
		}
		IShowProperty IView.GetShowProperties(IFrame Frame)
		{
			return this;
		}
		IGeoObject IView.LastSnapObject
		{
			get
			{
				return lastSnapObject;
			}
		}
		SnapPointFinder.DidSnapModes IView.LastSnapMode
		{
			get
			{
				return lastSnapMode;
			}
		}
		#endregion
		#region IShowProperty
		private bool viewDirectionModified;
		private GeoVector viewDirection;
		private GeoVectorProperty projectionDirection;
		private LengthProperty projectionDistance;
		public override string LabelText
		{
			get
			{
				// return StringTable.GetFormattedString("ModelView",name);
				return projectedModel.Name;
			}
		}
		/// <summary>
		/// Overrides <see cref="IShowPropertyImpl.EntryType"/>, 
		/// returns <see cref="ShowPropertyEntryType.GroupTitle"/>.
		/// </summary>
		public override ShowPropertyEntryType EntryType
		{
			get
			{
				return ShowPropertyEntryType.GroupTitle;
			}
		}
		/// <summary>
		/// Overrides <see cref="IShowPropertyImpl.LabelType"/>
		/// </summary>
		public override ShowPropertyLabelFlags LabelType
		{
			get
			{
				ShowPropertyLabelFlags res = ShowPropertyLabelFlags.Selectable | ShowPropertyLabelFlags.Editable | ShowPropertyLabelFlags.ContextMenu;
				if (Frame != null && Frame.ActiveView == this)
				{
					res |= ShowPropertyLabelFlags.Bold;
				}
				return res;
			}
		}
		/// <summary>
		/// Overrides <see cref="CADability.UserInterface.IShowPropertyImpl.LabelChanged (string)"/>
		/// </summary>
		/// <param name="NewText"></param>
		public override void LabelChanged(string NewText)
		{
			if (NewText == this.Name) return;
			if (!Frame.Project.RenameModelView(this, NewText))
			{
				// Messagebox oder nicht?
			}
		}
		public override void EndEdit(bool aborted, bool modified, string newValue)
		{
			if (newValue == this.Name) return;
			if (!Frame.Project.RenameModelView(this, newValue))
			{
				// display a Message-box?
			}
		}
		/// <summary>
		/// Overrides <see cref="PropertyEntryImpl.ContextMenu"/>, 
		/// returns the context menu with the id "MenuId.ModelView".
		/// (see <see cref="MenuResource.LoadContextMenu"/>)
		/// </summary>
		public override MenuWithHandler[] ContextMenu
		{
			get
			{
				Frame.ContextMenuSource = this;
				return MenuResource.LoadMenuDefinition("MenuId.ModelView", false, this);

			}
		}

		IShowProperty[] subEntries;
		/// <summary>
		/// Overrides <see cref="IShowPropertyImpl.SubEntriesCount"/>, 
		/// returns the number of subentries in this property view.
		/// </summary>
		public override int SubEntriesCount
		{
			get
			{
				return SubEntries.Length;
			}
		}
		/// <summary>
		/// Overrides <see cref="IShowPropertyImpl.SubEntries"/>, 
		/// returns the subentries in this property view.
		/// </summary>
		public override IShowProperty[] SubEntries
		{
			get
			{
				if (subEntries == null)
				{   // man könnte auch die Events bei Added setzen und bei Removed wieder entfernen ...
					List<IShowProperty> se = new List<IShowProperty>();
					string[] modelNames = new string[Frame.Project.GetModelCount()];
					for (int i = 0; i < Frame.Project.GetModelCount(); ++i)
					{
						modelNames[i] = Frame.Project.GetModel(i).Name;
					}
					string modelName;
					if (Model != null) modelName = Model.Name;
					else modelName = Frame.Project.GetActiveModel().Name;
					MultipleChoiceProperty modelSelection = new MultipleChoiceProperty("ModelSelection", modelNames, modelName);
					modelSelection.ValueChangedEvent += ModelSelectionChanged;
					se.Add(modelSelection);
					projectionDirection = new GeoVectorProperty(Frame, "ModelView.Projection.Direction");
					projectionDirection.OnGetValue = () => viewDirection;
					projectionDirection.OnSetValue = (v) => 
					{
						viewDirection = v;  // wird nur aufgehoben
						viewDirectionModified = true;
					};
					projectionDirection.IsAngle = false;
					projectionDirection.IsNormedVector = false;
					projectionDirection.FilterCommandEvent += FilterProjectionDirectionCommand;
					projectionDirection.ContextMenuId = "MenuId.Projection.Direction";
					se.Add(projectionDirection);
					projectionDistance = new LengthProperty(this, "Distance", "ModelView.Projection.Distance", Frame, false);
					se.Add(projectionDistance);
					se.Add(new DrawingPlaneProperty(Projection, Frame));
					BooleanProperty lineWidthMode = new BooleanProperty("ModelView.LineWidthMode", "ModelView.LineWidthMode.Values");
					lineWidthMode.GetBooleanEvent += GetLineWidthMode;
					lineWidthMode.SetBooleanEvent += SetLineWidthMode;
					lineWidthMode.BooleanValue = !projectedModel.UseLineWidth;
					se.Add(lineWidthMode);
					se.Add(Projection.Grid); // Achtung, projection kann sich ändern, dann stimmt diese Property nicht mehr
					VisibleLayers vl = new VisibleLayers("ModelView.VisibleLayers", Frame.Project.LayerList);
					vl.IsLayerVisibleEvent += IsLayerVisible;
					vl.LayerVisibilityChangedEvent += OnLayerVisibilityChanged;
					se.Add(vl);
					BooleanProperty showHiddenLines = new BooleanProperty("ModelView.ShowHiddenLines", "ModelView.ShowHiddenLines.Values");
					showHiddenLines.BooleanValue = Projection.ShowFaces;
					showHiddenLines.BooleanChangedEvent += OnShowHiddenLinesChanged;
					se.Add(showHiddenLines);
					subEntries = se.ToArray();
				}
				return subEntries;
			}
		}
		void OnShowHiddenLinesChanged(object sender, bool newValue)
		{
			Projection.ShowFaces = newValue;
			propertyTreeView.Refresh(this);
			projectedModel.ForceRecalc();
			canvas?.Invalidate();
		}
		/// <summary>
		/// Overrides <see cref="IShowPropertyImpl.Added"/>
		/// </summary>
		/// <param name="page"></param>
		public override void Added(IPropertyPage page)
		{
			base.Added(page);
			//propertyTreeView.FocusChangedEvent += new FocusChangedDelegate(OnFocusChanged);
			viewDirectionModified = false;
			viewDirection = Projection.Direction;
			Projection.Grid.GridChangedEvent += OnGridChanged;
			Frame.SettingChangedEvent += OnSettingChanged;
			project.ModelsChangedEvent += OnModelsChanged;
		}
		/// <summary>
		/// Overrides <see cref="IShowPropertyImpl.Removed"/>
		/// </summary>
		/// <param name="page">the IPropertyTreeView from which it was removed</param>
		public override void Removed(IPropertyPage page)
		{
			//propertyTreeView.FocusChangedEvent -= new FocusChangedDelegate(OnFocusChanged);
			Projection.Grid.GridChangedEvent -= OnGridChanged;
			Frame.SettingChangedEvent -= OnSettingChanged;
			project.ModelsChangedEvent -= OnModelsChanged;
			base.Removed(page);
			subEntries = null;
		}

		private bool GetLineWidthMode()
		{
			return !this.Projection.UseLineWidth;
		}
		private void SetLineWidthMode(bool val)
		{
			projectedModel.UseLineWidth = !val;
			this.Projection.UseLineWidth = !val;
			projectedModel.ForceRecalc(); // setzt das dirty Flag
			this.Model.ClearDisplayLists();
			canvas?.Invalidate();
		}
		public bool LineWidthMode
		{
			get
			{
				return !projectedModel.UseLineWidth;
			}
			set
			{
				projectedModel.UseLineWidth = !value;
				this.Projection.UseLineWidth = !value;
				projectedModel.ForceRecalc(); // setzt das dirty Flag
				this.Model.ClearDisplayLists();
				if (base.propertyTreeView != null && subEntries != null)
				{
					for (int i = 0; i < subEntries.Length; ++i)
					{
						if (subEntries[i].HelpLink == "ModelView.LineWidthMode")
						{
							BooleanProperty lwprop = subEntries[i] as BooleanProperty;
							if (lwprop != null) lwprop.Refresh();
							break;
						}
					}
					propertyTreeView.Refresh(this);
				}
			}
		}

		public bool IsShading
		{
			get
			{
				return Projection.ShowFaces;
			}
			set
			{
				Projection.ShowFaces = value;
				projectedModel.ForceRecalc();
				canvas?.Invalidate();
			}
		}

		private void OnFocusChanged(IPropertyTreeView sender, IShowProperty NewFocus, IShowProperty OldFocus)
		{
			foreach (IShowProperty sp in projectionDirection.SubEntries)
			{
				if (sp == NewFocus) return;
			}
			if (viewDirectionModified && NewFocus != projectionDirection)
			{
				if (distance > 0)
				{
					GeoPoint fromHere;
					if (fixPointValid) fromHere = fixPoint - distance * viewDirection;
					else fromHere = GeoPoint.Origin - distance * viewDirection;
					ProjectedModel.SetViewDirection(fromHere, viewDirection, fixPoint, false);
				}
				else
				{
					ProjectedModel.SetViewDirection(viewDirection, false);
				}
				viewDirectionModified = false;
				// Repaint, wenn Projektionsrichtung sich ändert
				ForceInvalidateAll();
				canvas?.Invalidate();
				RecalcScrollPosition();
			}
		}
		#endregion
		public bool FixPointValid
		{
			get
			{
				return fixPointValid;
			}
			set
			{
				fixPointValid = value;
			}
		}
		public GeoPoint FixPoint
		{
			get
			{
				return fixPoint;
			}
			set
			{
				fixPoint = value;
				fixPointValid = true;
				projectedModel.fixPoint = fixPoint; // weitergeben
			}
		}
		public double Distance
		{
			get
			{
				return distance;
			}
			set
			{
				distance = value;
				// hier muss man reagieren mit einer Änderung des Views
				if (distance > 0)
				{   // beim Focuswechsel ist das ausschlaggebend
					viewDirectionModified = true;
				}
				projectedModel.distance = distance;
			}
		}
		/// <summary>
		/// Gets or sets a flag which controls the dragging from this view, whether this view may be a source to DragAndDrop.
		/// </summary>
		public bool AllowDrag
		{
			get
			{
				return allowDrag;
			}
			set
			{
				allowDrag = value;
			}
		}
		/// <summary>
		/// Gets or sets a flag which controls whether a dragged object may be dropped on this view.
		/// </summary>
		public bool AllowDrop
		{
			get
			{
				return allowDrop;
			}
			set
			{
				allowDrop = value;
			}
		}
		/// <summary>
		/// Gets or sets a flag which controls the context menu when a right mouse click in this view happens.
		/// </summary>
		public bool AllowContextMenu
		{
			get
			{
				return allowContextMenu;
			}
			set
			{
				allowContextMenu = value;
			}
		}

		public void SetViewDirection(ModOp project, GeoPoint fixPoint, bool mouseIsDown)
		{
			PointF before = projectedModel.Projection.ProjectF(fixPoint);
			projectedModel.SetViewDirection(project, mouseIsDown);
			PointF after = projectedModel.Projection.ProjectF(fixPoint);
			projectedModel.Scroll(before.X - after.X, before.Y - after.Y);

			ForceInvalidateAll();
			canvas?.Invalidate();
			if (!mouseIsDown)
			{
				RecalcScrollPosition();
				projectedModel.RecalcAll(false);
			}
#if DEBUG
			//BoundingRect br = GetVisibleBoundingRect();
			//GeoVector dir = this.Projection.Direction;

			//this.Projection.Direction = dir;
			//this.ZoomToRect(br);
#endif
		}
		/// <summary>
		/// Set the direction of the view, the center and the scaling factor
		/// </summary>
		/// <param name="direction">The direction</param>
		/// <param name="center">The center in worldcoordinates</param>
		/// <param name="scalingFactor">World units to pixel</param>
		public void SetProjection(GeoVector direction, GeoPoint center, double scalingFactor)
		{
			projectedModel.SetViewDirection(direction, false);
			if (scalingFactor == 0.0)
			{
				ZoomToRect(Model.GetExtentForZoomTotal(Projection) * 1.1);
			}
			else
			{
				Rectangle clr = canvas.ClientRectangle;
				double w = clr.Width * scalingFactor / 2.0;
				double h = clr.Height * scalingFactor / 2.0;
				projectedModel.ZoomToRect(clr, new BoundingRect(GeoPoint2D.Origin, w, h));
				PointF after = projectedModel.Projection.ProjectF(center);
				projectedModel.Scroll(clr.Width / 2 - after.X, clr.Height / 2 - after.Y);

				ForceInvalidateAll();
				canvas?.Invalidate();
				RecalcScrollPosition();
			}
		}
		public bool SupressAutoRegeneration
		{
			get
			{
				return projectedModel.supressAutoRegeneration;
			}
			set
			{
				projectedModel.supressAutoRegeneration = value;
				canvas?.Invalidate();
			}
		}

		public void SetViewPosition(Point lastMousePosition, Point currentMousePosition)
		{
			int HOffset = currentMousePosition.X - lastMousePosition.X;
			int VOffset = currentMousePosition.Y - lastMousePosition.Y;
			if (VOffset != 0 || HOffset != 0)
			{

				lastMousePosition = new Point(currentMousePosition.X, currentMousePosition.Y);
				GeoVector haxis = Projection.InverseProjection * GeoVector.XAxis;
				GeoVector vaxis = Projection.InverseProjection * GeoVector.YAxis;
				ModOp mh = ModOp.Rotate(vaxis, SweepAngle.Deg(HOffset / 5.0));
				ModOp mv = ModOp.Rotate(haxis, SweepAngle.Deg(VOffset / 5.0));

				ModOp project = Projection.UnscaledProjection * mv * mh;
				// jetzt noch die Z-Achse einrichten. Die kann senkrecht nach oben oder unten
				// zeigen. Wenn die Z-Achse genau auf den Betrachter zeigt oder genau von ihm weg,
				// dann wird keine Anpassung vorgenommen, weils sonst zu sehr wackelt
				GeoVector z = project * GeoVector.ZAxis;
				if (ZAxisUp)
				{
					const double mindeg = 0.05; // nur etwas aufrichten, aber in jedem Durchlauf
					if (z.y < -0.1)
					{   // Z-Achse soll nach unten zeigen
						Angle a = new Angle(-GeoVector2D.YAxis, new GeoVector2D(z.x, z.y));
						if (a.Radian > mindeg) a.Radian = mindeg;
						if (a.Radian < -mindeg) a.Radian = -mindeg;
						if (z.x < 0)
						{
							project = project * ModOp.Rotate(vaxis ^ haxis, -a.Radian);
						}
						else
						{
							project = project * ModOp.Rotate(vaxis ^ haxis, a.Radian);
						}
						z = project * GeoVector.ZAxis;
					}
					else if (z.y > 0.1)
					{
						Angle a = new Angle(GeoVector2D.YAxis, new GeoVector2D(z.x, z.y));
						if (a.Radian > mindeg) a.Radian = mindeg;
						if (a.Radian < -mindeg) a.Radian = -mindeg;
						if (z.x < 0)
						{
							project = project * ModOp.Rotate(vaxis ^ haxis, a.Radian);
						}
						else
						{
							project = project * ModOp.Rotate(vaxis ^ haxis, -a.Radian);
						}
						z = project * GeoVector.ZAxis;
					}
				}
				// Fixpunkt bestimmen
				Point clcenter = canvas.ClientRectangle.Location;
				clcenter.X += canvas.ClientRectangle.Width / 2;
				clcenter.Y += canvas.ClientRectangle.Height / 2;
				// ium Folgenden ist Temporary auf true zu setzen und ein Mechanismus zu finden
				// wie der QuadTree von ProjektedModel berechnet werden soll
				//GeoVector newDirection = mv * mh * Projection.Direction;
				//GeoVector oldDirection = Projection.Direction;
				//GeoVector perp = oldDirection ^ newDirection;

				GeoPoint fp;
				if (fixPointValid)
				{
					fp = fixPoint;
				}
				else
				{
					fp = Projection.UnProject(clcenter);
				}
				SetViewDirection(project, fp, true);
				if (Math.Abs(HOffset) > Math.Abs(VOffset))
				{
					if (HOffset > 0) canvas.Cursor = "PanEast";
					else canvas.Cursor = "PanWest";
				}
				else
				{
					if (VOffset > 0) canvas.Cursor = "PanSouth";
					else canvas.Cursor = "PanNorth";
				}
				projectedModelNeedsRecalc = false;
			}
		}
		public void SetLayerVisibility(Layer l, bool visible)
		{
			if (visible)
				projectedModel.AddVisibleLayer(l);
			else
				projectedModel.RemoveVisibleLayer(l);
		}
		public void Recalc()
		{
			projectedModel.RecalcAll(false);
			RecalcScrollPosition();
		}
		private void RepaintSynchronized()
		{   // wird aufgerufen wenn ein backgroundthread in projectedmodel ein besseres Bild
			// berechnet hat
			canvas?.Invalidate();
		}
		private void ProjectedModelNeedsRepaint(object Sender, NeedsRepaintEventArg Arg)
		{
			canvas?.Invalidate();
		}
		private void ShowGrid(IPaintTo3D PaintToBackground, bool displayGrid, bool displayDrawingPlane)
		{
			if (Frame == null) return;
			Color bckgnd = Frame.GetColorSetting("Colors.Background", Color.AliceBlue);
			Color clrgrd = Frame.GetColorSetting("Colors.Grid", Color.LightGoldenrodYellow);
			clrgrd = Color.FromArgb(128, clrgrd.R, clrgrd.G, clrgrd.B);
			// Raster darstellen
			Projection pr = this.Projection;
			double factor, ddx, ddy;
			pr.GetPlacement(out factor, out ddx, out ddy);
			BoundingCube bc = Model.Extent;
			bc.MinMax(Model.MinExtend);
			BoundingRect ext = BoundingRect.EmptyBoundingRect;
			ext.MinMax(pr.DrawingPlane.Project(new GeoPoint(bc.Xmin, bc.Ymin, bc.Zmin)));
			ext.MinMax(pr.DrawingPlane.Project(new GeoPoint(bc.Xmin, bc.Ymin, bc.Zmax)));
			ext.MinMax(pr.DrawingPlane.Project(new GeoPoint(bc.Xmin, bc.Ymax, bc.Zmin)));
			ext.MinMax(pr.DrawingPlane.Project(new GeoPoint(bc.Xmin, bc.Ymax, bc.Zmax)));
			ext.MinMax(pr.DrawingPlane.Project(new GeoPoint(bc.Xmax, bc.Ymin, bc.Zmin)));
			ext.MinMax(pr.DrawingPlane.Project(new GeoPoint(bc.Xmax, bc.Ymin, bc.Zmax)));
			ext.MinMax(pr.DrawingPlane.Project(new GeoPoint(bc.Xmax, bc.Ymax, bc.Zmin)));
			ext.MinMax(pr.DrawingPlane.Project(new GeoPoint(bc.Xmax, bc.Ymax, bc.Zmax)));
			ext.Inflate(ext.Width, ext.Height);

			// höchstens 200 Einheiten in der jeweiligen Richtung
			double dx = pr.Grid.XDistance;
			double dy = pr.Grid.YDistance;
			while (ext.Width / dx > 200) dx = dx * 2.0;
			while (ext.Height / dy > 200) dy = dy * 2.0;
			int xstart = (int)(ext.Left / dx - 1);
			int xend = (int)(ext.Right / dx + 1);
			int ystart = (int)(ext.Bottom / dy - 1);
			int yend = (int)(ext.Top / dy + 1);
			if (xend - xstart < 250 && yend - ystart < 250 && pr.Grid.Show && displayGrid)
			{
				switch (pr.Grid.DisplayMode)
				{
					case Grid.Appearance.marks:
						PaintToBackground.SetColor(clrgrd);
						PaintToBackground.SetLineWidth(null); // 1.0 würde mit dem faktor multipliziert
						PaintToBackground.SetLinePattern(null);
						GeoPoint[] line1 = new GeoPoint[2];
						GeoPoint[] line2 = new GeoPoint[2];
						for (int x = xstart; x <= xend; ++x)
						{
							for (int y = ystart; y <= yend; ++y)
							{
								line1[0] = pr.DrawingPlane.ToGlobal(new GeoPoint2D(x * dx - 3.0 / factor, y * dy));
								line1[1] = pr.DrawingPlane.ToGlobal(new GeoPoint2D(x * dx + 3.0 / factor, y * dy));
								line2[0] = pr.DrawingPlane.ToGlobal(new GeoPoint2D(x * dx, y * dy - 3.0 / factor));
								line2[1] = pr.DrawingPlane.ToGlobal(new GeoPoint2D(x * dx, y * dy + 3.0 / factor));
								PaintToBackground.Polyline(line1);
								PaintToBackground.Polyline(line2);
							}
						}
						break;
					case Grid.Appearance.dots:
						// das ist nicht sehr optimal, man könnte zunächst auf das Pixelsystem
						// runterrechnen und dann zeichnen
						GeoPoint[] points = new GeoPoint[(xend - xstart + 1) * (yend - ystart + 1)];
						for (int x = xstart; x <= xend; ++x)
						{
							for (int y = ystart; y <= yend; ++y)
							{
								GeoPoint2D p = new GeoPoint2D(x * dx, y * dy);
								points[(x - xstart) * (yend - ystart + 1) + y - ystart] = pr.DrawingPlane.ToGlobal(p);
							}
						}
						PaintToBackground.SetColor(clrgrd);
						if (pr.Grid.DisplayMode == Grid.Appearance.dots)
						{
							PaintToBackground.Points(points, 1.0f, PointSymbol.Dot);
						}
						else
						{
							PaintToBackground.Points(points, 2.0f, PointSymbol.Dot);
						}
						break;
					case Grid.Appearance.lines:
						PaintToBackground.SetColor(clrgrd);
						PaintToBackground.SetLineWidth(null); // 1.0 würde mit dem faktor multipliziert
						PaintToBackground.SetLinePattern(null);
						GeoPoint[] line = new GeoPoint[2];
						for (int i = xstart; i <= xend; ++i)
						{
							line[0] = pr.DrawingPlane.ToGlobal(new GeoPoint2D(i * dx, ystart * dy));
							line[1] = pr.DrawingPlane.ToGlobal(new GeoPoint2D(i * dx, yend * dy));
							PaintToBackground.Polyline(line);
						}

						for (int i = ystart; i <= yend; ++i)
						{
							line[0] = pr.DrawingPlane.ToGlobal(new GeoPoint2D(xstart * dx, i * dy));
							line[1] = pr.DrawingPlane.ToGlobal(new GeoPoint2D(xend * dx, i * dy));
							PaintToBackground.Polyline(line);
						}
						break;
					case Grid.Appearance.fields:
						// der Hintergrund ist ja bereits gefüllt, die komische
						// Anfangsbedingung bei y dient dem Versatz der ungeraden Reihen
						// und dass es in allen Zoomstufen gleich bleibt.
						PaintToBackground.PushState();
						PaintToBackground.Blending(true);
						PaintToBackground.SetColor(clrgrd);
						for (int x = xstart; x <= xend; ++x)
						{
							for (int y = ystart + ((ystart + x) % 2); y <= yend; y += 2)
							{
								GeoPoint2D pp1 = new GeoPoint2D(x * dx, y * dy);
								GeoPoint2D pp2 = new GeoPoint2D((x + 1) * dx, y * dy);
								GeoPoint2D pp3 = new GeoPoint2D((x + 1) * dx, (y + 1) * dy);
								GeoPoint2D pp4 = new GeoPoint2D(x * dx, (y + 1) * dy);
								GeoPoint[] pf = new GeoPoint[4];
								pf[0] = pr.DrawingPlane.ToGlobal(pp1);
								pf[1] = pr.DrawingPlane.ToGlobal(pp2);
								pf[2] = pr.DrawingPlane.ToGlobal(pp3);
								pf[3] = pr.DrawingPlane.ToGlobal(pp4);
								GeoVector[] normals = new GeoVector[4];
								normals[0] = pr.DrawingPlane.Normal;
								normals[1] = pr.DrawingPlane.Normal;
								normals[2] = pr.DrawingPlane.Normal;
								normals[3] = pr.DrawingPlane.Normal;
								PaintToBackground.Triangle(pf, normals, new int[] { 0, 1, 2, 0, 2, 3 });
							}
						}
						PaintToBackground.PopState();
						break;
				}
			}
			else
			{   // raster in dieser Ansicht zu fein
			}
			if (pr.ShowDrawingPlane && displayDrawingPlane)
			{
				Color clrdrwpln = Frame.GetColorSetting("Colors.Drawingplane", Color.LightSkyBlue);
				PaintToBackground.PushState();
				PaintToBackground.Blending(true);
				PaintToBackground.SetColor(clrdrwpln);
				GeoPoint2D pp1 = new GeoPoint2D(xstart * dx, ystart * dy);
				GeoPoint2D pp2 = new GeoPoint2D(xend * dx, ystart * dy);
				GeoPoint2D pp3 = new GeoPoint2D(xend * dx, yend * dy);
				GeoPoint2D pp4 = new GeoPoint2D(xstart * dx, yend * dy);
				GeoPoint[] pf = new GeoPoint[4];
				pf[0] = pr.DrawingPlane.ToGlobal(pp1);
				pf[1] = pr.DrawingPlane.ToGlobal(pp2);
				pf[2] = pr.DrawingPlane.ToGlobal(pp3);
				pf[3] = pr.DrawingPlane.ToGlobal(pp4);
				GeoVector[] normals = new GeoVector[4];
				normals[0] = pr.DrawingPlane.Normal;
				normals[1] = pr.DrawingPlane.Normal;
				normals[2] = pr.DrawingPlane.Normal;
				normals[3] = pr.DrawingPlane.Normal;
				PaintToBackground.Triangle(pf, normals, new int[] { 0, 1, 2, 0, 2, 3 });
				PaintToBackground.PopState();
			}
		}
		private void PaintCoordCross(IPaintTo3D PaintToBackground, Projection pr, Color infocolor, Plane plane, bool local)
		{

			GeoPoint2D scr0 = pr.WorldToWindow(GeoPoint.Origin);
			GeoPoint2D scrx = pr.WorldToWindow(GeoPoint.Origin + GeoVector.XAxis);
			GeoPoint2D scry = pr.WorldToWindow(GeoPoint.Origin + GeoVector.YAxis);
			GeoPoint2D scrz = pr.WorldToWindow(GeoPoint.Origin + GeoVector.ZAxis);
			double scale = Math.Max(Math.Max(scrx | scr0, scry | scr0), scrz | scr0);
			double size;
			if (local) size = 13 / scale;
			else size = 20 / scale;
			GeoPoint org = plane.Location;

			PaintToBackground.UseZBuffer(false);
			PaintToBackground.SetColor(infocolor);
			PaintToBackground.SetLineWidth(null);
			PaintToBackground.SetLinePattern(null);
			PaintToBackground.Polyline(new GeoPoint[] { org, org + size * plane.DirectionX });
			PaintToBackground.Polyline(new GeoPoint[] { org, org + size * plane.DirectionY });
			PaintToBackground.Polyline(new GeoPoint[] { org, org + size * plane.Normal });
			GeoPoint p = org + size * plane.DirectionX;
			GeoVector v = plane.DirectionX ^ pr.Direction;
			if (!v.IsNullVector())
			{
				v.Norm();
				PaintTo3D.Arrow1(PaintToBackground, p + size / 4 * v, p - size / 4 * v, p + size / 2 * plane.DirectionX);
			}
			p = org + size * plane.DirectionY;
			v = plane.DirectionY ^ pr.Direction;
			if (!v.IsNullVector())
			{
				v.Norm();
				PaintTo3D.Arrow1(PaintToBackground, p + size / 4 * v, p - size / 4 * v, p + size / 2 * plane.DirectionY);
			}
			p = org + size * plane.Normal;
			v = plane.Normal ^ pr.Direction;
			if (!v.IsNullVector())
			{
				v.Norm();
				PaintTo3D.Arrow1(PaintToBackground, p + size / 4 * v, p - size / 4 * v, p + size / 2 * plane.Normal);
			}
			// if (!local && !pr.IsPerspective)
			{
				double d = size;
				PaintToBackground.PrepareText("Arial", "xyz0123456789+-°.,", FontStyle.Regular); // wg. einem komischen Fehler in PFOCAD:
																								 // dort kommen manchmal die Sortiernummern nicht, da wglUseFontOutlines nicht funktioniert. Wenn hier gleich alle Ziffern
																								 // erzeugt werden, dann passiert das nicht, denn hier geht es immer
				PaintToBackground.SetColor(infocolor);
				// die Buchstaben x,y,z am Ende der Achsen in Richtung der ProjectionPlane
				if (!Precision.SameDirection(pr.ProjectionPlane.Normal, plane.DirectionX, true))
					PaintToBackground.Text(d * pr.ProjectionPlane.DirectionX, d * pr.ProjectionPlane.DirectionY, org + 2 * size * plane.DirectionX, "Arial", "x", FontStyle.Regular, Text.AlignMode.Center, Text.LineAlignMode.Center);
				if (!Precision.SameDirection(pr.ProjectionPlane.Normal, plane.DirectionY, true))
					PaintToBackground.Text(d * pr.ProjectionPlane.DirectionX, d * pr.ProjectionPlane.DirectionY, org + 2 * size * plane.DirectionY, "Arial", "y", FontStyle.Regular, Text.AlignMode.Center, Text.LineAlignMode.Center);
				if (!Precision.SameDirection(pr.ProjectionPlane.Normal, plane.Normal, true))
					PaintToBackground.Text(d * pr.ProjectionPlane.DirectionX, d * pr.ProjectionPlane.DirectionY, org + 2 * size * plane.Normal, "Arial", "z", FontStyle.Regular, Text.AlignMode.Center, Text.LineAlignMode.Center);
			}
			return;

			// alter Text:
			//double size;
			//if (local) size = 13;
			//else size = 20;
			//double factor, dx, dy;
			//pr.GetPlacement(out factor, out dx, out dy);
			//double d = size / factor;
			//// 9 Punkte für die beiden Pfeile, zunächst im DrawingPlane System, dann
			//// ins projizierte 2D System gewandelt
			//GeoPoint2D p1 = new GeoPoint2D(0.0, 0.0); // Ursprung
			//GeoPoint2D p2 = new GeoPoint2D(d, 0.0); // X-Pfeil Basis
			//GeoPoint2D p3 = new GeoPoint2D(d, -d / 4.0);
			//GeoPoint2D p4 = new GeoPoint2D(1.5 * d, 0.0);
			//GeoPoint2D p5 = new GeoPoint2D(d, d / 4.0);
			//GeoPoint2D p6 = new GeoPoint2D(0.0, d);
			//GeoPoint2D p7 = new GeoPoint2D(-d / 4.0, d);
			//GeoPoint2D p8 = new GeoPoint2D(0.0, 1.5 * d);
			//GeoPoint2D p9 = new GeoPoint2D(d / 4.0, d);
			//GeoPoint pz3 = new GeoPoint(0.0, 0.0, d);
			//PointF pp1 = pr.Project(plane.ToGlobal(p1));
			//PointF pp2 = pr.Project(plane.ToGlobal(p2));
			//PointF pp3 = pr.Project(plane.ToGlobal(p3));
			//PointF pp4 = pr.Project(plane.ToGlobal(p4));
			//PointF pp5 = pr.Project(plane.ToGlobal(p5));
			//PointF pp6 = pr.Project(plane.ToGlobal(p6));
			//PointF pp7 = pr.Project(plane.ToGlobal(p7));
			//PointF pp8 = pr.Project(plane.ToGlobal(p8));
			//PointF pp9 = pr.Project(plane.ToGlobal(p9));
			//PointF pz2 = pr.Project(plane.ToGlobal(pz3));
			//// 1. Pfeil in X-Richtung (geschlossener Pfeil)
			//PaintToBackground.UseZBuffer(false);
			//PaintToBackground.SetColor(infocolor);
			//PaintToBackground.SetLineWidth(1.0);
			//PaintToBackground.SetLinePattern(null);
			//PaintToBackground.Line2D(pp1, pp2); // die Achse selbst
			//if (local)
			//{   // der geschlossene Pfeil
			//    PaintToBackground.Line2D(pp3, pp4);
			//    PaintToBackground.Line2D(pp4, pp5);
			//    PaintToBackground.Line2D(pp5, pp3);
			//}
			//else
			//{
			//    PaintToBackground.Blending(true);
			//    PaintToBackground.SetColor(Color.FromArgb(128, infocolor));
			//    GeoPoint[] vertex = new GeoPoint[6];
			//    vertex[0] = new GeoPoint(1.5 * d, 0, 0); // Spitze
			//    vertex[1] = new GeoPoint(d, d / 4, 0);
			//    vertex[2] = new GeoPoint(d, -d / 4, 0);
			//    vertex[3] = new GeoPoint(1.5 * d, 0, 0); // Spitze
			//    vertex[4] = new GeoPoint(d, 0, d / 4);
			//    vertex[5] = new GeoPoint(d, 0, -d / 4);
			//    GeoVector[] normal = new GeoVector[6];
			//    normal[0] = GeoVector.ZAxis;
			//    normal[1] = GeoVector.ZAxis;
			//    normal[2] = GeoVector.ZAxis;
			//    normal[3] = GeoVector.YAxis;
			//    normal[4] = GeoVector.YAxis;
			//    normal[5] = GeoVector.YAxis;
			//    int[] ind = new int[6];
			//    ind[0] = 0;
			//    ind[1] = 1;
			//    ind[2] = 2;
			//    ind[3] = 3;
			//    ind[4] = 4;
			//    ind[5] = 5;
			//    PaintToBackground.Triangle(vertex, normal, ind);
			//}
			//// 2. Pfeil in Y-Richtung (offener Pfeil)
			//PaintToBackground.Blending(false);
			//PaintToBackground.SetColor(infocolor);
			//PaintToBackground.Line2D(pp1, pp6);
			//if (local)
			//{   // der offene Pfeil
			//    PaintToBackground.Line2D(pp7, pp8);
			//    PaintToBackground.Line2D(pp8, pp9);
			//}
			//else
			//{
			//    PaintToBackground.Blending(true);
			//    PaintToBackground.SetColor(Color.FromArgb(128, infocolor));
			//    GeoPoint[] vertex = new GeoPoint[6];
			//    vertex[0] = new GeoPoint(0, 1.5 * d, 0); // Spitze
			//    vertex[1] = new GeoPoint(0, d, d / 4);
			//    vertex[2] = new GeoPoint(0, d, -d / 4);
			//    vertex[3] = new GeoPoint(0, 1.5 * d, 0); // Spitze
			//    vertex[4] = new GeoPoint(d / 4, d, 0);
			//    vertex[5] = new GeoPoint(-d / 4, d, 0);
			//    GeoVector[] normal = new GeoVector[6];
			//    normal[0] = GeoVector.XAxis;
			//    normal[1] = GeoVector.XAxis;
			//    normal[2] = GeoVector.XAxis;
			//    normal[3] = GeoVector.ZAxis;
			//    normal[4] = GeoVector.ZAxis;
			//    normal[5] = GeoVector.ZAxis;
			//    int[] ind = new int[6];
			//    ind[0] = 0;
			//    ind[1] = 1;
			//    ind[2] = 2;
			//    ind[3] = 3;
			//    ind[4] = 4;
			//    ind[5] = 5;
			//    PaintToBackground.Triangle(vertex, normal, ind);
			//}
			//// 3. Linie in Z-Richtung (nur Linie)
			//PaintToBackground.Blending(false);
			//PaintToBackground.SetColor(infocolor);
			//PaintToBackground.Line2D(pp1, pz2);
			//if (!local)
			//{
			//    PaintToBackground.Blending(true);
			//    PaintToBackground.SetColor(Color.FromArgb(128, infocolor));
			//    GeoPoint[] vertex = new GeoPoint[6];
			//    vertex[0] = new GeoPoint(0, 0, 1.5 * d); // Spitze
			//    vertex[1] = new GeoPoint(d / 4, 0, d);
			//    vertex[2] = new GeoPoint(-d / 4, 0, d);
			//    vertex[3] = new GeoPoint(0, 0, 1.5 * d); // Spitze
			//    vertex[4] = new GeoPoint(0, d / 4, d);
			//    vertex[5] = new GeoPoint(0, -d / 4, d);
			//    GeoVector[] normal = new GeoVector[6];
			//    normal[0] = GeoVector.YAxis;
			//    normal[1] = GeoVector.YAxis;
			//    normal[2] = GeoVector.YAxis;
			//    normal[3] = GeoVector.XAxis;
			//    normal[4] = GeoVector.XAxis;
			//    normal[5] = GeoVector.XAxis;
			//    int[] ind = new int[6];
			//    ind[0] = 0;
			//    ind[1] = 1;
			//    ind[2] = 2;
			//    ind[3] = 3;
			//    ind[4] = 4;
			//    ind[5] = 5;
			//    PaintToBackground.Triangle(vertex, normal, ind);
			//}
			//if (!local)
			//{
			//    PaintToBackground.PrepareText("Arial", "xyz", FontStyle.Regular);
			//    PaintToBackground.SetColor(infocolor);
			//    // die Buchstaben x,y,z am Ende der Achsen in Richtung der ProjectionPlane
			//    if (!Precision.SameDirection(pr.ProjectionPlane.Normal, GeoVector.XAxis, true))
			//        PaintToBackground.Text(d * pr.ProjectionPlane.DirectionX, d * pr.ProjectionPlane.DirectionY, new GeoPoint(2 * d, 0, 0), "Arial", "x", FontStyle.Regular, Text.AlignMode.Center, Text.LineAlignMode.Center);
			//    if (!Precision.SameDirection(pr.ProjectionPlane.Normal, GeoVector.YAxis, true))
			//        PaintToBackground.Text(d * pr.ProjectionPlane.DirectionX, d * pr.ProjectionPlane.DirectionY, new GeoPoint(0, 2 * d, 0), "Arial", "y", FontStyle.Regular, Text.AlignMode.Center, Text.LineAlignMode.Center);
			//    if (!Precision.SameDirection(pr.ProjectionPlane.Normal, GeoVector.ZAxis, true))
			//        PaintToBackground.Text(d * pr.ProjectionPlane.DirectionX, d * pr.ProjectionPlane.DirectionY, new GeoPoint(0, 0, 2 * d), "Arial", "z", FontStyle.Regular, Text.AlignMode.Center, Text.LineAlignMode.Center);
			//}
		}
		private void OnGridChanged(Grid sender)
		{
			RefreshBackground();
		}
		private void FilterProjectionDirectionCommand(GeoVectorProperty sender, string menuId, CommandState commandState, ref bool handled)
		{
			bool zoomTotal = Settings.GlobalSettings.GetBoolValue("ModelView.AutoZoomTotal", true);
			if (commandState != null)
			{
				switch (menuId)
				{
					case "MenuId.Projection.Direction.FromTop":
						commandState.Checked = Precision.SameNotOppositeDirection(Projection.Direction, -GeoVector.ZAxis);
						break;
					case "MenuId.Projection.Direction.FromFront":
						commandState.Checked = Precision.SameNotOppositeDirection(Projection.Direction, GeoVector.YAxis);
						break;
					case "MenuId.Projection.Direction.FromBack":
						commandState.Checked = Precision.SameNotOppositeDirection(Projection.Direction, -GeoVector.YAxis);
						break;
					case "MenuId.Projection.Direction.FromLeft":
						commandState.Checked = Precision.SameNotOppositeDirection(Projection.Direction, GeoVector.XAxis);
						break;
					case "MenuId.Projection.Direction.FromRight":
						commandState.Checked = Precision.SameNotOppositeDirection(Projection.Direction, -GeoVector.XAxis);
						break;
					case "MenuId.Projection.Direction.FromBottom":
						commandState.Checked = Precision.SameNotOppositeDirection(Projection.Direction, GeoVector.ZAxis);
						break;
					case "MenuId.Projection.Direction.Isometric":
						{
							GeoVector iso = new GeoVector(-1, -1, -1);
							iso.Norm();
							commandState.Checked = Precision.SameNotOppositeDirection(Projection.Direction, iso);
						}
						break;
					case "MenuId.Projection.Direction.Perspective":
						{
							commandState.Checked = distance > 0;
						}
						break;
				}
			}
			else
			{
				switch (menuId)
				{
					case "MenuId.Projection.Direction.FromTop":
						viewDirection = -GeoVector.ZAxis;
						viewDirectionModified = true;
						handled = true;
						break;
					case "MenuId.Projection.Direction.FromFront":
						viewDirection = GeoVector.YAxis;
						viewDirectionModified = true;
						handled = true;
						break;
					case "MenuId.Projection.Direction.FromBack":
						viewDirection = -GeoVector.YAxis;
						viewDirectionModified = true;
						handled = true;
						break;
					case "MenuId.Projection.Direction.FromLeft":
						viewDirection = GeoVector.XAxis;
						viewDirectionModified = true;
						handled = true;
						break;
					case "MenuId.Projection.Direction.FromRight":
						viewDirection = -GeoVector.XAxis;
						viewDirectionModified = true;
						handled = true;
						break;
					case "MenuId.Projection.Direction.FromBottom":
						viewDirection = GeoVector.ZAxis;
						viewDirectionModified = true;
						handled = true;
						break;
					case "MenuId.Projection.Direction.Isometric":
						viewDirection = new GeoVector(-1, -1, -1);
						viewDirection.Norm();
						viewDirectionModified = true;
						handled = true;
						break;
					case "MenuId.Projection.Direction.Orthogonal":
						viewDirection = -Projection.DrawingPlane.Normal;
						viewDirection.Norm();
						viewDirectionModified = true;
						handled = true;
						break;
					case "MenuId.Projection.Direction.Perspective":
						{   // Umschalten zentral<>parallel
							if (distance == 0.0)
							{   // wir behalten die Richtung bei, stellen nur perspektivisch ein
								distance = Model.Extent.Size / 2.0; // mal sehen, ob alle mit dem Wert zufrieden sind...
								zoomTotal = true; // bei Änderung des Modus ZoomTotal, wo sollte sonst der Fixpunkt sein?
							}
							else
							{
								distance = 0.0;
								zoomTotal = true; // bei Änderung des Modus ZoomTotal, wo sollte sonst der Fixpunkt sein?
							}
							if (projectionDistance != null) projectionDistance.Refresh();
							projectedModel.distance = distance;
							viewDirectionModified = true;
							handled = true;
						}
						break;
				}
				if (handled)
				{
					projectionDirection.Refresh();
					if (viewDirectionModified) // sofort updaten bei Menueauswahl
					{
						if (false) // hier nicht, und wenn, dann auf Projection.SetDirectionAnimated beziehen (Settings.GlobalSettings.GetBoolValue("AnimateViewChange", true) && condorCtrl != null)
						{
							// ... Projection.SetDirectionAnimated  ...
						}
						else
						{
							if (distance == 0.0)
							{
								ProjectedModel.SetViewDirection(viewDirection, false);
							}
							else
							{
								GeoPoint fromHere;
								if (fixPointValid) fromHere = fixPoint - distance * viewDirection;
								else fromHere = GeoPoint.Origin - distance * viewDirection;
								ProjectedModel.SetViewDirection(fromHere, viewDirection, fixPoint, false);
							}

							viewDirectionModified = false;
							if (zoomTotal)
							{
								ZoomToRect(Model.GetExtentForZoomTotal(Projection) * 1.1);
							}
							// Repaint, wenn Projektionsrichtung sich ändert
							ForceInvalidateAll();
							canvas?.Invalidate();
							RecalcScrollPosition();
						}
					}
				}
			}
		}

		private void OnSettingChanged(string Name, object NewValue)
		{
			if (Name == "Colors.Background" || Name == "Colors.Grid")
			{
				if (Name == "Colors.Background")
				{
					if (NewValue is ColorSetting)
					{
						BackgroundColor = (NewValue as ColorSetting).Color;
					}
					if (NewValue is Color)
					{
						BackgroundColor = (Color)NewValue;
					}
				}
				ForceInvalidateAll();
				canvas?.Invalidate();
			}
		}
		public bool IsLayerVisible(Layer l)
		{
			return projectedModel.IsLayerVisible(l);
		}
		public Layer[] GetVisibleLayers()
		{
			return projectedModel.GetVisibleLayers();
		}
		private void OnLayerVisibilityChanged(Layer l, bool isVisible)
		{
			if (isVisible) projectedModel.AddVisibleLayer(l);
			else projectedModel.RemoveVisibleLayer(l);
		}
		private void ModelSelectionChanged(object sender, object NewValue)
		{
			Model m = project.FindModel(NewValue as String);
			if (m == null) return;
			Projection pr = null;
			string viewname = "";
			if (projectedModel != null)
			{
				if (projectedModel.Model == m) return; // nix zu tun
				pr = projectedModel.Projection;
				viewname = projectedModel.Name;
			}
			else
			{
				pr = new Projection(Projection.StandardProjection.FromTop);
			}
			ProjectedModel pm = new ProjectedModel(m, pr);
			pm.Name = viewname;
			this.ProjectedModel = pm; // setzen der Property regelt den Rest

			ForceInvalidateAll();
			canvas?.Invalidate();
		}
		private void OnModelsChanged(Project sender, Model model, bool added)
		{
			subEntries = null;
			if (propertyTreeView != null)
			{
				propertyTreeView.Refresh(this);
			}
		}
		public void Scroll(int HScrollOffset, int VScrollOffset)
		{
			canvas.Cursor = "SizeAll";
			canvas?.Invalidate();
			projectedModel.Scroll(HScrollOffset, VScrollOffset);
			if (HScrollOffset > 0)
			{
				if (DisplayChangedEvent != null) DisplayChangedEvent(this, new DisplayChangeArg(DisplayChangeArg.Reasons.ScrollLeft));
			}
			else
			{
				if (DisplayChangedEvent != null) DisplayChangedEvent(this, new DisplayChangeArg(DisplayChangeArg.Reasons.ScrollRight));
			}
			if (VScrollOffset > 0)
			{
				if (DisplayChangedEvent != null) DisplayChangedEvent(this, new DisplayChangeArg(DisplayChangeArg.Reasons.ScrollUp));
			}
			else
			{
				if (DisplayChangedEvent != null) DisplayChangedEvent(this, new DisplayChangeArg(DisplayChangeArg.Reasons.ScrollDown));
			}
		}
		public Bitmap GetContentsAsBitmap()
		{
			{
				return null;
			}
		}

		public void SetAdditionalExtent(BoundingCube bc)
		{
			if (bc.IsEmpty) additionalExtent = bc; // this is the way to clear the additional extent
			else additionalExtent.MinMax(bc);
		}
		public void MakeEverythingTranparent(bool transparent)
		{
			Invalidate();
		}
	}
}
