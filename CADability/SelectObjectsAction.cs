using CADability.GeoObject;
using CADability.Shapes;
using System;
using System.Collections;
#if WEBASSEMBLY
using CADability.WebDrawing;
using Point = CADability.WebDrawing.Point;
#else
using System.Drawing;
using Point = System.Drawing.Point;
#endif
using System.Runtime.Serialization;

using MouseEventArgs = CADability.Substitutes.MouseEventArgs;
using DragEventArgs = CADability.Substitutes.DragEventArgs;
using MouseButtons = CADability.Substitutes.MouseButtons;
using DragDropEffects = CADability.Substitutes.DragDropEffects;
using Keys = CADability.Substitutes.Keys;

namespace CADability.Actions
{
	using CADability.Attribute;
	using CADability.Substitutes;
	using System.Collections.Generic;
	using UserInterface;
	using Wintellect.PowerCollections;

	/// <summary>
	/// Diese Klasse fasst die Settings für das Markieren zusammen. Im einzelnen sind das:
	/// Select.HandleSize, 
	/// Select.FrameColor, 
	/// Select.HandleColor, 
	/// Select.UseFrame
	/// </summary>
	[Serializable]
	class SelectObjectsSettings : Settings, IDeserializationCallback, IJsonSerialize, IJsonSerializeDone
	{
		public static SelectObjectsSettings GetDefault()
		{   // erzeuge eine Default Version des Objektes
			SelectObjectsSettings res = new SelectObjectsSettings();
			res.resourceIdInternal = "SelectObjects.Settings";

			IntegerProperty HandleSizeIntegerProperty = new IntegerProperty("Select.HandleSize", "HandleSize");
			HandleSizeIntegerProperty.IntegerValue = 3;
			res.AddSetting("HandleSize", HandleSizeIntegerProperty); // soll der Markierrahmen mit den Handles verwendet werden

			ColorSetting FrameColor = new ColorSetting("FrameColor", "Select.FrameColor");
			FrameColor.Color = Color.DarkBlue;
			res.AddSetting("FrameColor", FrameColor);

			ColorSetting HandleColor = new ColorSetting("HandleColor", "Select.HandleColor");
			HandleColor.Color = Color.DarkBlue;
			res.AddSetting("HandleColor", HandleColor);

			ColorSetting SelectColor = new ColorSetting("SelectColor", "Select.SelectColor");
			SelectColor.Color = Color.Yellow;
			res.AddSetting("SelectColor", SelectColor);

			ColorSetting FocusColor = new ColorSetting("FocusColor", "Select.FocusColor");
			FocusColor.Color = Color.LightBlue;
			res.AddSetting("FocusColor", FocusColor);

			IntegerProperty SelectWidth = new IntegerProperty("Select.SelectWidth", "SelectWidth");
			SelectWidth.IntegerValue = 2;
			res.AddSetting("SelectWidth", SelectWidth);

			IntegerProperty SelectTransparency = new IntegerProperty("Select.SelectTransparency", "SelectTransparency");
			SelectTransparency.IntegerValue = 200;
			SelectTransparency.SetMinMax(1, 255, true);
			res.AddSetting("SelectTransparency", SelectTransparency);

			BooleanProperty SelectAbove = new BooleanProperty("Select.SelectAbove", "Select.SelectAbove.Values", "SelectAbove");
			SelectAbove.BooleanValue = false;
			res.AddSetting("SelectAbove", SelectAbove); // soll der Markierrahmen mit den Handles verwendet werden

			IntegerProperty DragWidth = new IntegerProperty("Select.DragWidth", "DragWidth");
			DragWidth.IntegerValue = 2;
			res.AddSetting("DragWidth", DragWidth);

			BooleanProperty UseFrameBooleanProperty = new BooleanProperty("Select.UseFrame", "Select.UseFrame.Values", "UseFrame");
			UseFrameBooleanProperty.BooleanValue = false;
			res.AddSetting("UseFrame", UseFrameBooleanProperty); // soll der Markierrahmen mit den Handles verwendet werden

			BooleanProperty FastFeedbackProperty = new BooleanProperty("Select.FastFeedback", "Select.FastFeedback.Values", "FastFeedback");
			FastFeedbackProperty.BooleanValue = false;
			res.AddSetting("FastFeedback", FastFeedbackProperty); // soll der Markierrahmen mit den Handles verwendet werden

			ColorSetting FeedbackColor = new ColorSetting("FeedbackColor", "Select.FeedbackColor");
			FeedbackColor.Color = Color.LightBlue;
			res.AddSetting("FeedbackColor", FeedbackColor);

			return res;
		}
		public SelectObjectsSettings()
		{
			this.myName = "Select"; // damit wir z.B. zu "Select.HandleSize" kommen
		}
		/// <summary>
		/// Constructor required by deserialization
		/// </summary>
		/// <param name="info">SerializationInfo</param>
		/// <param name="context">StreamingContext</param>
		protected SelectObjectsSettings(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
			this.myName = "Select"; // damit wir z.B. zu "Select.HandleSize" kommen
		}
		#region IDeserializationCallback Members
		void IDeserializationCallback.OnDeserialization(object sender)
		{
			base.OnDeserialization();
			if (!ContainsSetting("FrameColor"))
			{
				ColorSetting FrameColor = new ColorSetting("FrameColor", "Select.FrameColor");
				FrameColor.Color = Color.DarkGray;
				AddSetting("FrameColor", FrameColor);
			}
			if (!ContainsSetting("HandleColor"))
			{
				ColorSetting HandleColor = new ColorSetting("HandleColor", "Select.HandleColor");
				HandleColor.Color = Color.DarkGray;
				AddSetting("HandleColor", HandleColor);
			}
			if (!ContainsSetting("SelectColor"))
			{
				ColorSetting SelectColor = new ColorSetting("SelectColor", "Select.SelectColor");
				SelectColor.Color = Color.Yellow;
				AddSetting("SelectColor", SelectColor);
			}

			if (!ContainsSetting("FocusColor"))
			{
				ColorSetting FocusColor = new ColorSetting("FocusColor", "Select.FocusColor");
				FocusColor.Color = Color.LightBlue;
				AddSetting("FocusColor", FocusColor);
			}
			if (!ContainsSetting("SelectWidth"))
			{
				IntegerProperty SelectWidth = new IntegerProperty("Select.SelectWidth", "SelectWidth");
				SelectWidth.IntegerValue = 2;
				AddSetting("SelectWidth", SelectWidth);
			}
			if (!ContainsSetting("SelectTransparency"))
			{
				IntegerProperty SelectTransparency = new IntegerProperty("Select.SelectTransparency", "SelectTransparency");
				SelectTransparency.IntegerValue = 200;
				SelectTransparency.SetMinMax(1, 255, true);
				AddSetting("SelectTransparency", SelectTransparency);
			}
			if (!ContainsSetting("SelectAbove"))
			{
				BooleanProperty SelectAbove = new BooleanProperty("Select.SelectAbove", "Select.SelectAbove.Values", "SelectAbove");
				SelectAbove.BooleanValue = false;
				AddSetting("SelectAbove", SelectAbove); // soll der Markierrahmen mit den Handles verwendet werden
			}
			if (!ContainsSetting("DragWidth"))
			{
				IntegerProperty DragWidth = new IntegerProperty("Select.DragWidth", "DragWidth");
				DragWidth.IntegerValue = 5;
				AddSetting("DragWidth", DragWidth);
			}
			if (!ContainsSetting("PickRadius"))
			{
				IntegerProperty PickRadius = new IntegerProperty("Select.PickRadius", "PickRadius");
				PickRadius.IntegerValue = 5;
				AddSetting("PickRadius", PickRadius);
			}
			if (!ContainsSetting("FeedbackColor"))
			{
				ColorSetting FeedbackColor = new ColorSetting("FeedbackColor", "Select.FeedbackColor");
				FeedbackColor.Color = Color.LightBlue;
				AddSetting("FeedbackColor", FeedbackColor);
			}
			if (!ContainsSetting("FastFeedback"))
			{
				BooleanProperty FastFeedbackProperty = new BooleanProperty("Select.FastFeedback", "Select.FastFeedback.Values", "FastFeedback");
				FastFeedbackProperty.BooleanValue = false;
				AddSetting("FastFeedback", FastFeedbackProperty); // soll der Markierrahmen mit den Handles verwendet werden
			}
		}
		public override void GetObjectData(IJsonWriteData data)
		{
			base.GetObjectData(data);
		}

		public override void SetObjectData(IJsonReadData data)
		{
			base.SetObjectData(data);
			data.RegisterForSerializationDoneCallback(this);
		}
		public override void SerializationDone(JsonSerialize jsonSerialize)
		{
			base.SerializationDone(jsonSerialize);
			OnDeserialization();
		}
		#endregion
	}

	internal class SetSelection : ICommandHandler
	{
		IGeoObject toSelect;
		SelectObjectsAction selectObjectsAction;
		public SetSelection(IGeoObject toSelect, SelectObjectsAction selectObjectsAction)
		{
			this.toSelect = toSelect;
			this.selectObjectsAction = selectObjectsAction;
		}

		virtual public bool OnCommand(string MenuId)
		{
			switch (MenuId)
			{
				case "MenuId.Selection.Set":
					selectObjectsAction.SetSelectedObjects(new GeoObjectList(toSelect));
					return true;
				case "MenuId.Selection.Add":
					selectObjectsAction.AddSelectedObject(toSelect);
					return true;
			}
			return false;
		}

		virtual public void OnSelected(MenuWithHandler selectedMenuItem, bool selected)
		{
		}

		virtual public bool OnUpdateCommand(string MenuId, CommandState CommandState)
		{
			switch (MenuId)
			{
				case "MenuId.Selection.Set":
					return true;
				case "MenuId.Selection.Add":
					return true;
			}
			return false;
		}
	}

	/// <summary>
	/// The <see cref="Action"/> used to select GeoObjects of a (visible) <see cref="Model"/>.
	/// Mouse-clicks in the view are used to determine which objects should be selected. The user
	/// can select a rectangular area by dragging the mouse while the left button is pushed. Dragging
	/// from left to right selects all objects completely inside the rectangle, dragging from right to left
	/// selects all objects which are touched by the rectangle. The <see cref="PickMode"/> determines on which
	/// level the selection is performed, the <see cref="Project.FilterList"/> adds additional filtering
	/// to the selection process.
	/// </summary>

	public class SelectObjectsAction : Action
	{
		private enum Mode { NoAction, TestDragRect, DragRect, DragWidth, DragHeight, DragSize, DragMove, DragRotate, DragObjectProperty, TestHotSpot }
		public enum CursorPosition { EmptySpace, OverObject, OverSelectedObject, OverHandleLB, OverHandleLM, OverHandleLT, OverHandleMB, OverHandleMM, OverHandleMT, OverHandleRB, OverHandleRM, OverHandleRT, OverFixPoint, OverHotSpot }
		private Mode mode; // der Zustand, in dem sich die Aktion gerade befindet
		private CursorPosition lastCursorPosition; // die letzte gemessene Cursorposition, wird in den DragXxx Modi nicht berechnet
		private Point downPoint; // Position des letzten MouseDown
		private Point FirstPoint; // erster Punkt des Aufziehrechteck
		private Point SecondPoint; // zweiter Punkt des Aufziehrechteck
		private bool canStartDrag;
		// private IView lastView; // der CondorView, in dem die letzte Mausbewegung registriert wurde: wird nicht verwendet
		private bool useFrame; // Markierrahmen verwenden
		private bool directFeedback; // direktes Feedback verwenden
		private int handleSize; // Größe der Handles
		private Color frameColor; // die Farbe für den Rahmen
		private Color handleColor; // die Farbe für die Handles
		private Color selectColor;
		private int selectTransparency;
		private bool selectAbove;
		private Color focusColor;
		private Color feedbackColor;
		private int selectWidth;
		private int dragWidth;
		private int pickRadius;
		private IPaintTo3DList displayList;

		private GeoPoint2D fixPoint; // der Fixpunkt beim Ziehen an den Handles
		private BoundingRect orgExtend; // Ausdehunug der original Objekte beim Ziehen
		private GeoPoint lastPoint; // der letzte Punkt beim Ziehen an den Handles
		private ModOp cumulatedDrag;
		private GeoObjectList selectedObjects; // die gerade markierten Objekte
		private GeoObjectList activeObjects; // die aktiven Objekte, wenn manipuliert wird
		private GeoObjectList objectsUnderCursorFeedback; // die unter dem Cursor liegenden Objekte
		private SelectedObjectsProperty selectedObjectsProperty;
		private ArrayList hotspots; // Liste von IHotSpot, die gerade angezeigt werden
		private IHotSpot selectedHotspot;
		private IHotSpot lastCursorHotspot;
		private bool PropertyIsSleeping;
		private bool pickParent; // z.Z. unveränderlich: nur Parent picken
								 // private bool pickEdges; // lieber Kanten als Flächen Picken, offensichtlich nirgends verwendet
		private PickMode pickMode;
		private PickMode oldPickMode; // fallback value when overwritten by context menu
		private bool accumulateObjects; // if set, no need to hold ctrl key to accumulate objects
		internal bool dragDrop;
		private Projection.PickArea pickArea; // the last pickArea, which caused a change in the list of selected objects
		private bool showSelectedObjects = true;
		/// <summary>
		/// Constructs a new SelectObjectsAction. This is automatically done when a <see cref="IFrame"/> derived object
		/// is created and the instance of this class can be retrieved from <see cref="IFrame.ActiveAction"/>.
		/// </summary>
		/// <param name="Frame">The <see cref="IFrame">Frame</see> on which this action operates.</param>
		public SelectObjectsAction(IFrame Frame)
		{
			selectedObjects = new GeoObjectList();
			activeObjects = new GeoObjectList();
			objectsUnderCursorFeedback = new GeoObjectList();
			selectedObjectsProperty = SelectedObjectsProperty.Construct(Frame);
			selectedObjectsProperty.FocusedObjectChangedEvent += new SelectedObjectsProperty.FocusedObjectChangedDelegate(OnFocusedObjectChanged);
			selectedObjectsProperty.HotspotChangedEvent += new HotspotChangedDelegate(OnHotspotChanged);
			mode = Mode.NoAction;
			lastCursorPosition = CursorPosition.EmptySpace;
			hotspots = new ArrayList();
			PropertyIsSleeping = false;
			pickParent = true;
			dragDrop = true;
			base.ViewType = typeof(IActionInputView); // arbeitet nur auf ModelView Basis
			oldPickMode = pickMode = PickMode.normal;
		}

		void OnFocusedObjectChanged(SelectedObjectsProperty sender, IGeoObject focused)
		{
			displayList = null;
		}
		/// <summary>
		/// Method definition for the <see cref="SelectedObjectListChangedEvent"/>.
		/// </summary>
		/// <param name="sender">The action that raised the event.</param>
		/// <param name="selectedObjects">The list of the now selected objects</param>
		public delegate void SelectedObjectListChanged(SelectObjectsAction sender, GeoObjectList selectedObjects);
		/// <summary>
		/// Event raised when the list of selected objects changed. 
		/// </summary>
		public event SelectedObjectListChanged SelectedObjectListChangedEvent;
		/// <summary>
		/// Method definition of the <see cref="ClickOnSelectedObjectEvent"/>
		/// </summary>
		/// <param name="selected">The object on which the click occurred.</param>
		/// <param name="vw">The <see cref="IView">view</see> in which the click happened</param>
		/// <param name="e">The original MouseEventArgs propagated from the mouse event</param>
		/// <param name="handled">If handled set to true by the handler of the event, no further action will be performed.</param>
		public delegate void ClickOnSelectedObjectDelegate(IGeoObject selected, IView vw, MouseEventArgs e, ref bool handled);
		/// <summary>
		/// Event raised when clicking on a already selected object.
		/// </summary>
		public event ClickOnSelectedObjectDelegate ClickOnSelectedObjectEvent;

		/// <summary>
		/// Method definition of the <see cref="BeforeShowContextMenuEvent"/>
		/// </summary>
		/// <param name="selected">The object on which the click occurred.</param>
		/// <param name="vw">The <see cref="IView">view</see> in which the click happend</param>
		public delegate void BeforeShowContextMenuDelegate(GeoObjectList selectedObjects, MenuWithHandler[] cm);
		/// <summary>
		/// Event raised before context menue is displayed, you can modify it here.
		/// </summary>
		public event BeforeShowContextMenuDelegate BeforeShowContextMenuEvent;


		/// <summary>
		/// Enumeration used in <see cref="FilterMouseMessagesDelegate"/>.
		/// </summary>
		public enum MouseAction
		{
			/// <summary>
			/// Mouse button pushed
			/// </summary>
			MouseDown,
			/// <summary>
			/// Mouse button released
			/// </summary>
			MouseUp,
			/// <summary>
			/// Mouse is moving
			/// </summary>
			MouseMove
		}
		/// <summary>
		/// Method declaration for the <see cref="FilterMouseMessagesEvent"/>.
		/// </summary>
		/// <param name="mouseAction">The mouse action causing the event</param>
		/// <param name="e">Original MouseEventArgs</param>
		/// <param name="vw">The <see cref="IView">view</see> in which the mouse action took place.</param>
		/// <param name="handled">If handled set to true by the handler of the event, no further action will be performed.</param>
		public delegate void FilterMouseMessagesDelegate(MouseAction mouseAction, MouseEventArgs e, IView vw, ref bool handled);
		/// <summary>
		/// Event raised before the according mouse message is processed by this action. The user may
		/// prevent the handling of this mouse message by this action and/or do some other work instead.
		/// </summary>
		public event FilterMouseMessagesDelegate FilterMouseMessagesEvent;
		/// <summary>
		/// Delegate definition for the <see cref="FilterDragListEvent"/>.
		/// </summary>
		/// <param name="objectsAboutToBeDragged">The objects that are going to be dragged</param>
		public delegate void FilterDragListDelegate(GeoObjectList objectsAboutToBeDragged);
		/// <summary>
		/// Event to filter objects that are beeing dragged. This event is called just before the objects are dragged.
		/// You may modify the list of objects. If you clear the list, no objects will be dragged.
		/// </summary>
		public event FilterDragListDelegate FilterDragListEvent;
		private BoundingRect GetDisplayExtent(Projection projection, IGeoObject geoObject)
		{
			return geoObject.GetExtent(projection, ExtentPrecision.Raw);
		}
		private BoundingRect GetDisplayExtent(Projection projection, GeoObjectList list)
		{
			BoundingRect res = BoundingRect.EmptyBoundingRect;
			for (int i = 0; i < list.Count; ++i)
			{
				res.MinMax(GetDisplayExtent(projection, list[i]));
			}
			return res;
		}
		private void RecalcFrameSize(IView vw, out int frameLeft, out int frameRight, out int frameBottom, out int frameTop)
		{
			if (vw.Model == null)
			{
				frameLeft = 0;
				frameRight = 0;
				frameBottom = 0;
				frameTop = 0;
				return; // Zeichen für leer
			}
			BoundingRect ext = GetDisplayExtent(vw.Projection, selectedObjects);
			if (ext.IsEmpty())
			{
				frameLeft = 0;
				frameRight = 0;
				frameBottom = 0;
				frameTop = 0;
				return; // Zeichen für leer
			}
			Rectangle rext = vw.Projection.DeviceRect(ext);
			frameLeft = rext.Left;
			frameRight = rext.Right;
			frameBottom = rext.Bottom;
			frameTop = rext.Top;
			// anpassen, wenn zu klein
			if (frameBottom - frameTop < 3 * (2 * handleSize + 1))
			{
				int c = (frameTop + frameBottom) / 2; // die Mitte
				int d = (3 * (2 * handleSize + 1) + 1) / 2; // die Hälfte der 3 Handles
				frameTop = c - d;
				frameBottom = c + d;
			}
			if (frameRight - frameLeft < 3 * (2 * handleSize + 1))
			{
				int c = (frameRight + frameLeft) / 2; // die Mitte
				int d = (3 * (2 * handleSize + 1) + 1) / 2; // die Hälfte der 3 Handles
				frameRight = c + d;
				frameLeft = c - d;
			}
		}
		private Rectangle GetFrameInvalidateRect(IView vw)
		{   // liefert das View abhängige umgebende Rechteck der markierten Objekte
			// welches um handleSize erweitert wurde
			int frameLeft, frameRight, frameBottom, frameTop;
			RecalcFrameSize(vw, out frameLeft, out frameRight, out frameBottom, out frameTop);
			foreach (IHotSpot hsp in hotspots)
			{
				if (vw.Projection != null)
				{
					GeoPoint p = hsp.GetHotspotPosition();
					PointF pf = vw.Projection.ProjectF(p);
					if (pf.X - 0.5 < frameLeft) frameLeft = (int)(pf.X - 0.5);
					if (pf.X + 0.5 > frameRight) frameRight = (int)(pf.X + 0.5);
					if (pf.Y - 0.5 < frameTop) frameTop = (int)(pf.Y - 0.5);
					if (pf.Y + 0.5 > frameBottom) frameBottom = (int)(pf.Y + 0.5);
				}
			}
			Rectangle res = new Rectangle(frameLeft, frameTop, frameRight - frameLeft, frameBottom - frameTop);
			if (res.Width + res.Height > 0) res.Inflate(handleSize + 1, handleSize + 1);
			return res;
		}

		private void OnRepaintSelect(Rectangle IsInvalid, IView View, IPaintTo3D PaintToSelect)
		{
			// stellt das umgebende Rechteck, die Handles und die markierten Objekte in dem gegebenen View dar
			if (mode == Mode.DragRect)
			{
				Color bckgnd = Frame.GetColorSetting("Colors.Background", Color.AliceBlue);
				Color infocolor;
				if (bckgnd.GetBrightness() > 0.5) infocolor = Color.Black;
				else infocolor = Color.White;

				PaintToSelect.SetColor(infocolor);
				PaintToSelect.Line2D(FirstPoint.X, FirstPoint.Y, SecondPoint.X, FirstPoint.Y);
				PaintToSelect.Line2D(SecondPoint.X, FirstPoint.Y, SecondPoint.X, SecondPoint.Y);
				PaintToSelect.Line2D(SecondPoint.X, SecondPoint.Y, FirstPoint.X, SecondPoint.Y);
				PaintToSelect.Line2D(FirstPoint.X, SecondPoint.Y, FirstPoint.X, FirstPoint.Y);
			}
			if ((selectedObjects.Count > 0 || (directFeedback && objectsUnderCursorFeedback.Count > 0)) && (mode == Mode.NoAction || mode == Mode.TestDragRect) && showSelectedObjects)
			{
				PaintToSelect.SelectMode = true;
				PaintToSelect.SelectColor = Color.FromArgb(selectTransparency, selectColor);
				bool visible = false;
				int wobbleWIdth = selectWidth;
				if (selectWidth == 0 && selectAbove) wobbleWIdth = -1;

				// die sichtbarkeit von Objekten hängt von View ab. Man müsste also wieder Layerindizierte
				// displaylisten benutzen, aber das wäre zu aufwendig. Axsys erwartet, dass in Ansichten, in denen 
				// die Objekte unsichtbar sind, diese auch nicht als markiert angezeigt werden. Das funktioniert somit
				// wenn auch nur bei einzelobjekten, oder wenn alle die gleiche Sichtbarkeit haben
				IActionInputView mv = (View as IActionInputView);
				if (mv == null) visible = true;
				foreach (IGeoObject go in selectedObjects)
				{
					if (go.IsVisible && mv != null && mv.IsLayerVisible(go.Layer)) visible = true;
				}
				if ((PaintToSelect.Capabilities & PaintCapabilities.ZoomIndependentDisplayList) != 0)
				{
					if (displayList == null)
					{
						PaintToSelect.OpenList("selected");
						foreach (IGeoObject go in selectedObjects)
						{
							if (go.IsVisible && mv != null && mv.IsLayerVisible(go.Layer)) visible = true;
							if (selectedObjectsProperty.focusedSelectedObject != go && go.IsVisible)
								go.PaintTo3D(PaintToSelect);
						}
						displayList = PaintToSelect.CloseList();
					}
					if (selectedObjects.Count > 0 && GetGeoObjectModel(selectedObjects[0]) != View.Model) visible = false;
					if (visible && displayList != null) PaintToSelect.SelectedList(displayList, wobbleWIdth);
					if (selectedObjectsProperty.focusedSelectedObject != null)
					{
						PaintToSelect.SelectColor = Color.FromArgb(selectTransparency, focusColor);
						IPaintTo3DList list = null;
						PaintToSelect.OpenList("focus-selected");
						if (selectedObjectsProperty.focusedSelectedObject.IsVisible) selectedObjectsProperty.focusedSelectedObject.PaintTo3D(PaintToSelect);
						list = PaintToSelect.CloseList();
						if (list != null && visible) PaintToSelect.SelectedList(list, wobbleWIdth);
					}
					if (directFeedback && objectsUnderCursorFeedback.Count > 0)
					{
						PaintToSelect.SelectColor = Color.FromArgb(selectTransparency, feedbackColor); // transparent machen
						PaintToSelect.OpenList("underCursor");
						foreach (IGeoObject go in objectsUnderCursorFeedback)
						{
							go.PaintTo3D(PaintToSelect);
							visible = true;
						}
						IPaintTo3DList feedbackDisplayList = PaintToSelect.CloseList();
						if (feedbackDisplayList != null) PaintToSelect.SelectedList(feedbackDisplayList, wobbleWIdth);
					}
				}
				else
				{   // keine Displayliste verwenden, z.B. GDI2DView
					if (selectedObjects.Count > 0 && GetGeoObjectModel(selectedObjects[0]) != View.Model) visible = false;
					if (visible)
					{
						foreach (IGeoObject go in selectedObjects)
						{
							if (go.IsVisible && mv != null && mv.IsLayerVisible(go.Layer))
							{
								if (selectedObjectsProperty.focusedSelectedObject != go)
									go.PaintTo3D(PaintToSelect);
							}
						}
						if (selectedObjectsProperty.focusedSelectedObject != null)
						{
							PaintToSelect.SelectColor = Color.FromArgb(selectTransparency, focusColor);
							selectedObjectsProperty.focusedSelectedObject.PaintTo3D(PaintToSelect);
						}
					}
				}
				PaintToSelect.SelectMode = false;
				if (useFrame && visible)
				{
					int frameLeft, frameRight, frameBottom, frameTop;
					RecalcFrameSize(View, out frameLeft, out frameRight, out frameBottom, out frameTop);
					PaintToSelect.SetColor(frameColor);
					PaintToSelect.Line2D(frameLeft, frameBottom, frameRight, frameBottom);
					PaintToSelect.Line2D(frameRight, frameBottom, frameRight, frameTop);
					PaintToSelect.Line2D(frameRight, frameTop, frameLeft, frameTop);
					PaintToSelect.Line2D(frameLeft, frameTop, frameLeft, frameBottom);

					PaintTo3D.PaintHandle(PaintToSelect, frameLeft, frameBottom, handleSize, handleColor);
					PaintTo3D.PaintHandle(PaintToSelect, frameRight, frameBottom, handleSize, handleColor);
					PaintTo3D.PaintHandle(PaintToSelect, frameLeft, frameTop, handleSize, handleColor);
					PaintTo3D.PaintHandle(PaintToSelect, frameRight, frameTop, handleSize, handleColor);
					int cx = (frameLeft + frameRight) / 2;
					int cy = (frameBottom + frameTop) / 2;
					PaintTo3D.PaintHandle(PaintToSelect, cx, frameBottom, handleSize, handleColor);
					PaintTo3D.PaintHandle(PaintToSelect, cx, frameTop, handleSize, handleColor);
					PaintTo3D.PaintHandle(PaintToSelect, frameLeft, cy, handleSize, handleColor);
					PaintTo3D.PaintHandle(PaintToSelect, frameRight, cy, handleSize, handleColor);
					PaintTo3D.PaintHandle(PaintToSelect, cx, cy, handleSize, handleColor);
				}
				if (visible)
				{
					foreach (IHotSpot hsp in hotspots)
					{
						GeoPoint p = hsp.GetHotspotPosition();
						PointF pf = View.Projection.ProjectF(p);
						//Color bckgnd = Frame.GetColorSetting("Colors.Background", Color.AliceBlue);
						//Color infocolor;
						//if (bckgnd.GetBrightness() > 0.5) infocolor = System.Drawing.Color.Black;
						//else infocolor = System.Drawing.Color.White;
						PaintTo3D.PaintHandle(PaintToSelect, pf, handleSize, handleColor);
						// TODO: man bräuchte einen Algorithmus, der alle hotspots bei Überlappung
						// neu platziert, so eine Art Gedränge von innen nach außen
					}
					if (selectedHotspot != null)
					{
						GeoPoint p = selectedHotspot.GetHotspotPosition();
						PointF pf = View.Projection.ProjectF(p);
						//Color bckgnd = Frame.GetColorSetting("Colors.Background", Color.AliceBlue);
						//Color infocolor;
						//if (bckgnd.GetBrightness() > 0.5) infocolor = System.Drawing.Color.Black;
						//else infocolor = System.Drawing.Color.White;
						if (handleSize > 1) PaintTo3D.PaintHandle(PaintToSelect, pf, handleSize - 1, handleColor);
						if (handleSize > 2) PaintTo3D.PaintHandle(PaintToSelect, pf, handleSize - 2, handleColor);
					}
				}
			}
		}

		private Model GetGeoObjectModel(IGeoObject go)
		{
			IGeoObjectOwner Owner = go.Owner;
			while (Owner != null)
			{
				if (Owner is Model) return Owner as Model;
				if (Owner is IGeoObject) Owner = (Owner as IGeoObject).Owner;
				else if (Owner is Edge) Owner = (Owner as Edge).Owner.Owner;
				else return null;
			}
			return null;
			//while (go.Owner is IGeoObject) go = go.Owner as IGeoObject;
			//return go.Owner as Model;
		}
		/// <summary>
		/// Returns the selected object which currently has the focus. This is only meaningful if several objects
		/// are selected and the user navigates in the control center
		/// </summary>
		/// <returns>The selected object which currently has the focus</returns>
		public IGeoObject GetFocusedSelectedObject()
		{
			return selectedObjectsProperty.focusedSelectedObject;
		}
		/// <summary>
		/// Set the selected object which should get focus. This is only meaningful if several objects
		/// are selected and the user navigates in the control center
		/// </summary>
		/// <param name="geoObject">The GeoObject which should get the focus</param>
		public void SetFocusedSelectedObject(IGeoObject geoObject)
		{
			selectedObjectsProperty.Focus(geoObject);
		}
		/// <summary>
		/// Gets or sets the pick-mode or selection mode for this SelectObjectsAction.
		/// </summary>
		public PickMode PickMode
		{
			get
			{
				return pickMode;
			}
			set
			{
				oldPickMode = pickMode = value;
				accumulateObjects = false;
			}
		}
		/// <summary>
		/// Starts the accumulation mode: each click adds to the selection, regardless whether the ctrl key is pressed or not.
		/// Used by feature definition and maybe in other situations.
		/// </summary>
		/// <param name="mode"></param>
		public void Accumulate(PickMode mode)
		{
			oldPickMode = this.pickMode;
			this.pickMode = mode;
			accumulateObjects = true;
		}
		/// <summary>
		/// Stops the accumulation mode (<seealso cref="Accumulate(PickMode)"/>
		/// </summary>
		public void StopAccumulate()
		{
			accumulateObjects = false;
			pickMode = oldPickMode;
		}
		/// <summary>
		/// Detects whether the accumulate mode is on (<seealso cref="Accumulate(PickMode)"/>
		/// </summary>
		public bool IsAccumulating
		{
			get { return accumulateObjects; }
		}
		/// <summary>
		/// Returns the pick area of the last mouse button up event, which caused the list of selected objects to change
		/// </summary>
		/// <returns></returns>
		public Projection.PickArea GetPickArea()
		{
			return pickArea;
		}
		/// <summary>
		/// Shows the selection. This is normally true, but can be used (e.g. by the modeller of ShapeIt) to turn off the display and implement is in a differnt way
		/// </summary>
		public bool ShowSelectedObjects { get { return showSelectedObjects; } set { showSelectedObjects = value; } }
		/// <summary>
		/// Adds the provided GeoObject to the list of selected objects.
		/// The <see cref="SelectedObjectListChangedEvent"/> will be raised.
		/// </summary>
		/// <param name="selObj">The object to add</param>
		public void AddSelectedObject(IGeoObject selObj)
		{
			AddSelectedObjects(new GeoObjectList(selObj));
		}
		/// <summary>
		/// Adds the provided list of geoObjects to the list of selected objects.
		/// The <see cref="SelectedObjectListChangedEvent"/> will be raised.
		/// </summary>
		/// <param name="selObj">List of objects to add</param>
		public void AddSelectedObjects(GeoObjectList selObj)
		{   // zentrale Stelle, an der neue Objekte markiert werden. Das Invalidate für das wegnehmen
			// der Markierung ist hier schon gelaufen. Wird auch mit leerer Liste aufgerufen
#if DEBUG

			if (selObj.Count == 1)
			{
				bool ok = true;
				if (selObj[0] is Solid)
				{
					foreach (Edge edge in (selObj[0] as Solid).Shells[0].Edges)
					{
						if (edge.Curve3D != null && edge.Curve3D.GetExtent().Zmin < -0.1)
						{ }
					}
					ok = (selObj[0] as Solid).Shells[0].CheckConsistency();
					//Shell sh = (selObj[0] as Solid).Shells[0].Clone() as Shell;
					//sh.RecalcVertices();
					//sh.CombineConnectedFaces();
					//ok = sh.CheckConsistency();
					//string serialized = JsonSerialize.ToString(selObj[0] as Solid);
					//Solid dbg = JsonSerialize.FromString(serialized) as Solid;
					//ok = dbg.Shells[0].CheckConsistency();
					//foreach (Edge edg in (selObj[0] as Solid).Shells[0].Edges)
					//{

					//    if (edg.Curve3D is InterpolatedDualSurfaceCurve)
					//    {
					//        (edg.Curve3D as InterpolatedDualSurfaceCurve).CheckSurfaceParameters();
					//        //edg.Curve3D = new InterpolatedDualSurfaceCurve(edg.PrimaryFace.Surface, edg.PrimaryFace.Area.GetExtent(), edg.SecondaryFace.Surface, edg.SecondaryFace.Area.GetExtent(), new List<GeoPoint>((edg.Curve3D as InterpolatedDualSurfaceCurve).BasePoints));
					//        //edg.PrimaryCurve2D = (edg.Curve3D as InterpolatedDualSurfaceCurve).CurveOnSurface1;
					//        //edg.SecondaryCurve2D = (edg.Curve3D as InterpolatedDualSurfaceCurve).CurveOnSurface2;
					//        //if (!edg.Forward(edg.PrimaryFace)) edg.PrimaryCurve2D.Reverse();
					//        //if (!edg.Forward(edg.SecondaryFace)) edg.SecondaryCurve2D.Reverse();
					//    }
					//}
				}
				if (selObj[0] is Shell)
				{
					ok = (selObj[0] as Shell).CheckConsistency();
				}
				if (selObj[0] is Face)
				{
					ok = (selObj[0] as Face).CheckConsistency();
				}
				if (!ok)
				{
					//try
					//{
					//    if (selObj[0] is Solid)
					//    {
					//        (selObj[0] as Solid).Shells[0].RecalcVertices();
					//        (selObj[0] as Solid).Shells[0].Repair();
					//        ok = (selObj[0] as Solid).Shells[0].CheckConsistency();
					//        (selObj[0] as Solid).Shells[0].AssertOutwardOrientation();
					//        ok = (selObj[0] as Solid).Shells[0].CheckConsistency();
					//        Shell sdbg = Project.SerializeDeserialize((selObj[0] as Solid).Shells[0]) as Shell;
					//        ok = sdbg.CheckConsistency();
					//        sdbg.ReverseOrientation();
					//        ok = sdbg.CheckConsistency();
					//    }
					//    if (selObj[0] is Shell)
					//    {
					//        (selObj[0] as Shell).RecalcVertices();
					//        (selObj[0] as Shell).Repair();
					//        ok = (selObj[0] as Shell).CheckConsistency();
					//    }
					//}
					//catch
					//{ }
				}
			}
#endif
			displayList = null; // gilt nicht mehr, wird beim repaint neu generiert
			if (selectedObjects.Count > 0)
			{   // es wird in ein nicht leeres Array zugefügt, da können auch welche drin sein, die 
				// neu zugefügt werden, deshalb mit set mischen
				Set<IGeoObject> set = new Set<IGeoObject>((IGeoObject[])selectedObjects, new GeoObjectComparer());
				for (int i = 0; i < selObj.Count; ++i)
				{
					if (!set.Contains(selObj[i]))
					{   // der set ist viel schneller als die GeoObjectlist
						// deshalb hier Umweg über set
						set.Add(selObj[i]);
						selObj[i].WillChangeEvent += new ChangeDelegate(OnWillChange);
						selObj[i].DidChangeEvent += new ChangeDelegate(OnDidChange);
					}
				}
				selectedObjects = new GeoObjectList(set.ToArray());
			}
			else
			{   // hier nicht mit sen set arbeiten, da die reihenfolge erhalten bleiben soll
				for (int i = 0; i < selObj.Count; ++i)
				{
					selObj[i].WillChangeEvent += new ChangeDelegate(OnWillChange);
					selObj[i].DidChangeEvent += new ChangeDelegate(OnDidChange);
				}
				selectedObjects = new GeoObjectList(selObj);
			}
			selectedObjectsProperty.SetGeoObjectList(selectedObjects);
			// ist diese Aktion überhaupt aktiv, d.h. wird selectedObjectsProperty überhaupt
			// z.Z. dargestellt?
			if (Frame.ControlCenter != null)
			{
				IPropertyPage pp = Frame.ControlCenter.GetPropertyPage("Action");
				if (pp != null)
				{
					pp.Remove(selectedObjectsProperty);
					pp.Add(selectedObjectsProperty, true);
					if (Frame.GetBooleanSetting("Action.PopProperties", true))
					{
						pp.BringToFront();
						Frame.SetControlCenterFocus("Action", null, false, false);
					}
				}
			}
			hotspots.Clear();
			selectedHotspot = null;

			selectedObjectsProperty.OpenSubEntries();
			// selectedObjectsExtent = selectedObjects.GetExtent(); // Empty, wenn Liste leer
			foreach (IView vw in base.Frame.AllViews)
			{
				if (vw.Projection.IsPerspective)
				{   // GetFrameInvalidateRect liefert hier falschen Wert
					vw.InvalidateAll();
				}
				else
				{
					Rectangle extnew = GetFrameInvalidateRect(vw);
					if (extnew.Width + extnew.Height > 0) vw.Invalidate(PaintBuffer.DrawingAspect.Select, extnew);
				}
			}
			if (SelectedObjectListChangedEvent != null) SelectedObjectListChangedEvent(this, selectedObjects);
		}
		/// <summary>
		/// Sets the provided object as the only selected object
		/// </summary>
		/// <param name="toSelect"></param>
		public void SetSelectedObject(IGeoObject toSelect)
		{
			SetSelectedObjects(new GeoObjectList(toSelect));
		}
		/// <summary>
		/// Replaces the contents of the list of selected objects by the contents of the provided list.
		/// The <see cref="SelectedObjectListChangedEvent"/> will be raised.
		/// </summary>
		/// <param name="selObj">List of GeoObjects to be selected</param>
		public void SetSelectedObjects(GeoObjectList selObj)
		{
			if (selObj.HasSameContent(selectedObjects)) return;
			for (int i = 0; i < selectedObjects.Count; ++i)
			{
				selectedObjects[i].WillChangeEvent -= new ChangeDelegate(OnWillChange);
				selectedObjects[i].DidChangeEvent -= new ChangeDelegate(OnDidChange);
				// selectedObjects[i].UserData.RemoveUserData("CADability.SelectList");
			}
			foreach (IView vw in base.Frame.AllViews)
			{
				if (vw.Projection.IsPerspective)
				{   // GetFrameInvalidateRect liefert hier falschen Wert
					vw.InvalidateAll();
				}
				else
				{
					Rectangle oldext = GetFrameInvalidateRect(vw);
					vw.Invalidate(PaintBuffer.DrawingAspect.Select, oldext);
				}
			}
			selectedObjects.Clear();
			AddSelectedObjects(selObj);
		}
		/// <summary>
		/// Replaces the contents of the list of selected objects by those objects of the provided list
		/// that are accepted by the according <see cref="Project.FilterList"/>.
		/// The <see cref="SelectedObjectListChangedEvent"/> will be raised.
		/// </summary>
		/// <param name="selObj">List of objects to select.</param>
		/// <param name="useFilter">If true, applies all active Filters of the <see cref="Project.FilterList"/>.</param>
		public void SetSelectedObjects(GeoObjectList selObj, bool useFilter)
		{
			if (!useFilter) SetSelectedObjects(selObj);
			else
			{
				FilterList filterList = Frame.Project.FilterList;
				GeoObjectList layerchecked = new GeoObjectList();
				for (int i = 0; i < selObj.Count; ++i)
				{
					if (filterList.Accept(selObj[i]) && selObj[i].IsVisible) layerchecked.Add(selObj[i]);
				}
				SetSelectedObjects(layerchecked);
			}
		}
		/// <summary>
		/// Removes the provided GeoObject from the list of selected objects.
		/// The <see cref="SelectedObjectListChangedEvent"/> will be raised.
		/// </summary>
		/// <param name="toDeselect">The object to remove</param>
		public void RemoveSelectedObject(IGeoObject toDeselect)
		{
			GeoObjectList newList = new GeoObjectList(selectedObjects);
			newList.Remove(toDeselect);
			SetSelectedObjects(newList);
		}
		/// <summary>
		/// Clears the list of selected objects. No more objects are selected after this call.
		/// The <see cref="SelectedObjectListChangedEvent"/> will be raised.
		/// </summary>
		public void ClearSelectedObjects()
		{
			SetSelectedObjects(new GeoObjectList()); // einfach leere Liste
		}
		/// <summary>
		/// Returns the list of selected GeoObjects.
		/// </summary>
		/// <returns>The list of selected GeoObjects</returns>
		public GeoObjectList GetSelectedObjects()
		{
			return selectedObjects;
		}
		/// <summary>
		/// Implements <see cref="Action.OnActivate"/>. If you override this method
		/// don't forget to call the bas implementation.
		/// </summary>
		public override void OnActivate(Action OldActiveAction, bool SettingAction)
		{
			if (PropertyIsSleeping)
			{
				selectedObjectsProperty.ReloadProperties();
			}
			else
			{
				IPropertyPage ptv = base.Frame.GetPropertyDisplay("Action");
				if (ptv != null) ptv.Add(selectedObjectsProperty, true);
				// das mehrfache Einfügen macht nichts
				foreach (IView vw in base.Frame.AllViews)
				{
					//vw.SetPaintHandler(PaintBuffer.DrawingAspect.Select, new RepaintView(OnRepaintSelect));
					vw.RemovePaintHandler(PaintBuffer.DrawingAspect.Select, new PaintView(OnRepaintSelect));
					vw.SetPaintHandler(PaintBuffer.DrawingAspect.Select, new PaintView(OnRepaintSelect));
				}
			}
			if (OldActiveAction is IIntermediateConstruction)
			{
				IIntermediateConstruction ic = OldActiveAction as IIntermediateConstruction;
				IPropertyPage ptv = base.Frame.GetPropertyDisplay("Action");
				if (ptv != null)
				{
					ptv.MakeVisible(ic.GetHandledProperty());
					ptv.SelectEntry(ic.GetHandledProperty());
				}
			}
			base.Frame.SettingChangedEvent += new SettingChangedDelegate(OnSettingChanged);
			Model m = base.Frame.Project.GetActiveModel();
			// man müsste reagieren, wenn das aktive Modell wechselt, d.h. es müsste einen
			// entsprechenden event für die Aktionen geben, und zwar für alle,
			// nicht nur für die aktive
			m.GeoObjectRemovedEvent += new CADability.Model.GeoObjectRemoved(OnGeoObjectRemoved);
			// da wir die Änderungen nicht mitbekommen haben hier die Werte setzen
			handleSize = base.Frame.GetIntSetting("Select.HandleSize", 3); // Größe der Handles
			frameColor = base.Frame.GetColorSetting("Select.FrameColor", Color.Gray); // die Farbe für den Rahmen
			handleColor = base.Frame.GetColorSetting("Select.HandleColor", Color.DarkBlue); // die Farbe für die Handles
			selectColor = base.Frame.GetColorSetting("Select.SelectColor", Color.Yellow); // die Farbe für die selektierten Objekte
			selectTransparency = base.Frame.GetIntSetting("Select.SelectTransparency", 128); // die transparenz beim Selektieren
			selectAbove = base.Frame.GetBooleanSetting("Select.SelectAbove", false); // die transparenz beim Selektieren
			focusColor = base.Frame.GetColorSetting("Select.FocusColor", Color.LightBlue); // die Farbe für das Objekt mit dem Focus
			feedbackColor = base.Frame.GetColorSetting("Select.FeedbackColor", Color.LightBlue); // die Farbe für das Objekt mit dem Focus
			selectWidth = base.Frame.GetIntSetting("Select.SelectWidth", 2);
			dragWidth = base.Frame.GetIntSetting("Select.DragWidth", 5);
			pickRadius = base.Frame.GetIntSetting("Select.PickRadius", 5);
			useFrame = base.Frame.GetBooleanSetting("Select.UseFrame", false); // soll der Markierrahmen mit den Handles verwendet werden
			directFeedback = base.Frame.GetBooleanSetting("Select.FastFeedback", true); // soll direktes Feedback angezeigt werden?
			base.OnActivate(OldActiveAction, SettingAction);
		}
		/// <summary>
		/// Implements <see cref="Action.OnInactivate"/>. If you override this method
		/// don't forget to call the bas implementation.
		/// </summary>
		/// <param name="NewActiveAction"><paramref name="Action.OnInactivate.NewActiveAction"/></param>
		/// <param name="RemovingAction"><paramref name="Action.OnInactivate.RemovingAction"/></param>
		public override void OnInactivate(Action NewActiveAction, bool RemovingAction)
		{
			bool RemoveProperty = true;
			PropertyIsSleeping = false;
			if (NewActiveAction != null)
			{
				string NewActionId = NewActiveAction.GetID();
				// das Zauberwort "[LeaveSelectProperties]" in der ID der Aktion verhindert das 
				// Entfernen der Properties
				if (NewActionId != null && NewActionId.IndexOf("[LeaveSelectProperties]") > 0)
				{
					RemoveProperty = false;
					PropertyIsSleeping = true;
				}
			}
			if (RemoveProperty)
			{
				ClearSelectedObjects();
				IPropertyPage ptv = base.Frame.GetPropertyDisplay("Action");
				if (ptv != null) ptv.Remove(selectedObjectsProperty);
				foreach (IView vw in base.Frame.AllViews)
				{
					//vw.RemovePaintHandler(PaintBuffer.DrawingAspect.Select, new RepaintView(OnRepaintSelect));
					vw.RemovePaintHandler(PaintBuffer.DrawingAspect.Select, new PaintView(OnRepaintSelect));
				}
				StopAccumulate(); // when the context menu did switch to a collecting mode, we must stop it here
			}
			base.Frame.SettingChangedEvent -= new SettingChangedDelegate(OnSettingChanged);
			Model m = base.Frame.Project.GetActiveModel();
			m.GeoObjectRemovedEvent -= new CADability.Model.GeoObjectRemoved(OnGeoObjectRemoved);
			base.OnInactivate(NewActiveAction, RemovingAction);
		}

		private GeoObjectList FeedbackObjectsUnderCursor(MouseEventArgs e, IView vw)
		{
			Projection.PickArea pa = vw.Projection.GetPickSpace(new Rectangle(e.X - pickRadius, e.Y - pickRadius, pickRadius * 2, pickRadius * 2));

			IActionInputView pm = vw as IActionInputView;
			FilterList filterList = Frame.Project.FilterList;
			// folgendes liefert keine Kanten, dazu müsste man einen extra PickMode machen
			return vw.Model.GetObjectsFromRect(pa, new Set<Layer>(pm.GetVisibleLayers()), PickMode.singleChild, filterList);
		}

		private GeoObjectList ObjectsUnderCursor(MouseEventArgs e, IView vw, bool single)
		{
			pickArea = vw.Projection.GetPickSpace(new Rectangle(e.X - pickRadius, e.Y - pickRadius, pickRadius * 2, pickRadius * 2));

			GeoObjectList result = new GeoObjectList();
			IActionInputView pm = vw as IActionInputView;

			// BoundingRect pickrect = vw.Projection.BoundingRectWorld2d(e.X - pickRadius, e.X + pickRadius, e.Y + pickRadius, e.Y - pickRadius);
			GeoObjectList fromquadtree = null;
			FilterList filterList = Frame.Project.FilterList;

			switch (pickMode)
			{   // der Pickmode bezieht sich auf mehrere Objete, hier wird allerdings nur ein Objekt gesucht
				case PickMode.singleChild:
				case PickMode.blockchildren:
				case PickMode.children:
					if (single)
					{
						fromquadtree = vw.Model.GetObjectsFromRect(pickArea, new Set<Layer>(pm.GetVisibleLayers()), PickMode.singleChild, filterList, selectedObjects);
					}
					else
					{
						fromquadtree = vw.Model.GetObjectsFromRect(pickArea, new Set<Layer>(pm.GetVisibleLayers()), PickMode.children, filterList);
					}
					break;
				case PickMode.normal:
					if (single)
					{
						fromquadtree = vw.Model.GetObjectsFromRect(pickArea, new Set<Layer>(pm.GetVisibleLayers()), PickMode.single, filterList, selectedObjects);
					}
					else
					{
						fromquadtree = vw.Model.GetObjectsFromRect(pickArea, new Set<Layer>(pm.GetVisibleLayers()), PickMode.normal, filterList);
					}
					// fromquadtree = pm.GetObjectsFromRect(pickrect, PickMode.single, filterList);
					break;
				case PickMode.onlyFaces:
					if (single)
						fromquadtree = vw.Model.GetObjectsFromRect(pickArea, new Set<Layer>(pm.GetVisibleLayers()), PickMode.singleFace, filterList, selectedObjects);
					else
						fromquadtree = vw.Model.GetObjectsFromRect(pickArea, new Set<Layer>(pm.GetVisibleLayers()), PickMode.onlyFaces, filterList);
					// fromquadtree = pm.GetObjectsFromRect(pickrect, PickMode.singleFace, filterList);
					break;
				case PickMode.onlyEdges:
					if (single)
						fromquadtree = vw.Model.GetObjectsFromRect(pickArea, new Set<Layer>(pm.GetVisibleLayers()), PickMode.singleEdge, filterList, selectedObjects);
					else
						fromquadtree = vw.Model.GetObjectsFromRect(pickArea, new Set<Layer>(pm.GetVisibleLayers()), PickMode.onlyEdges, filterList);
					// fromquadtree = pm.GetObjectsFromRect(pickrect, PickMode.singleEdge, filterList);
					break;
			}
			// die Objekte sind alle schon mit HitTest getestet, es werden nur die echt getroffenen geliefert
			bool addParent = pickParent && (pickMode == PickMode.normal);
			bool childOfBlock = (pickMode == CADability.PickMode.singleChild) || (pickMode == CADability.PickMode.blockchildren); // blockchildren wg. Bazzi
			foreach (IGeoObject go in fromquadtree)
			{
				if (childOfBlock && go.Owner is IGeoObject)
				{
					IGeoObject par = go.Owner as IGeoObject;
					// Im folgenden werden aus Edges oder Faces -> Shells oder Solids
					while (par.Owner is IGeoObject && !(par.Owner is Block)) par = par.Owner as IGeoObject;
					result.AddUnique(par);
				}
				else if (addParent && go.Owner is IGeoObject && !childOfBlock)
				{
					IGeoObject par = go.Owner as IGeoObject;
					while (par.Owner is IGeoObject) par = par.Owner as IGeoObject;
					// pickEdges ist immer false, wird offensichtlich nicht mehr verwendet
					//if (((par is Face) || (par is Solid)) && pickEdges)
					//{
					//    if (filterList.Accept(go) && go.IsVisible) result.AddUnique(go);
					//}
					//else
					//{
					result.AddUnique(par);
					//}
				}
				else
				{   // hier braucht man nicht testen
					// if (filterList.Accept(go) && go.IsVisible) result.AddUnique(go);
					// Filter ist schon getestet und das Problem mit Blöcken (ERSACAD) würde hier stören
					// stört sich mit Mauell, deshalb Unterscheidung nach go.Layer!=null
					if (go.Layer != null)
					{
						if (filterList.Accept(go) && go.IsVisible) result.AddUnique(go);
					}
					else
					{
						if (go.IsVisible) result.AddUnique(go);
					}
				}
			}
			return result;
		}
		private GeoObjectList ObjectsInsideDragRect(IView vw)
		{
			GeoObjectList result = new GeoObjectList();
			IActionInputView pm = vw as IActionInputView;
			BoundingRect pickrect = vw.Projection.BoundingRectWorld2d(
				Math.Min(FirstPoint.X, SecondPoint.X), Math.Max(FirstPoint.X, SecondPoint.X),
				Math.Max(FirstPoint.Y, SecondPoint.Y), Math.Min(FirstPoint.Y, SecondPoint.Y)
				);
			Rectangle winrect = new Rectangle(Math.Min(FirstPoint.X, SecondPoint.X), Math.Min(FirstPoint.Y, SecondPoint.Y),
				Math.Abs(FirstPoint.X - SecondPoint.X), Math.Abs(FirstPoint.Y - SecondPoint.Y));
			// GeoObjectList fromocttree = pm.GetObjectsFromRect(pickrect, pickMode, null);
			FilterList filterList = Frame.Project.FilterList;
			if (winrect.Width == 0) winrect.Inflate(1, 0);
			if (winrect.Height == 0) winrect.Inflate(0, 1);
			pickArea = vw.Projection.GetPickSpace(winrect);
			GeoObjectList fromocttree = vw.Model.GetObjectsFromRect(pickArea, new Set<Layer>(pm.GetVisibleLayers()), pickMode, filterList);
			bool onlyInside = (FirstPoint.X < SecondPoint.X);
			foreach (IGeoObject go in fromocttree)
			{
				if (pickMode == CADability.PickMode.blockchildren && onlyInside)
				{   // fromocttree enthält hier alles auf der untersten Ebene.
					// Bei blockchildren gehen wir nach oben, solange der Block noch ganz im Pickbereich liegt
					// Das gilt nur für onlyinside, sonst macht es vermutlich keinen Sinn, bis wohin sollte man sonst gehen, Hittest passt ja dann immer
					if ((go.Owner is Block || go.Owner is Face || go.Owner is Shell || go.Owner is Solid) && go.HitTest(pickArea, onlyInside))
					{   // beim Block die Kinder liefern, aber so weit nach oben gehen, bis es nicht mehr enthalten ist
						IGeoObject prnt = go;
						while (prnt.Owner is IGeoObject && (prnt.Owner as IGeoObject).HitTest(pickArea, onlyInside)) prnt = prnt.Owner as IGeoObject;
						result.AddUnique(prnt);
					}
				}
				else
				{
					if (go.HitTest(vw.Projection, pickrect, onlyInside))
					{
						if (go.IsVisible) result.Add(go);
					}
				}
			}
			return result;
		}
		private void GetCursorPosition(MouseEventArgs e, IView vw)
		{
			lastCursorPosition = CursorPosition.EmptySpace;
			if (selectedObjects.Count > 0)
			{   // nur dann gibt es die Handles
				if (useFrame)
				{
					int frameLeft, frameRight, frameBottom, frameTop;
					RecalcFrameSize(vw, out frameLeft, out frameRight, out frameBottom, out frameTop);
					int Pos = 0;
					int frameHorCenter = (frameLeft + frameRight) / 2;
					int frameVerCenter = (frameBottom + frameTop) / 2;
					if (e.X >= frameLeft - handleSize && e.X <= frameLeft + handleSize) Pos += 1;
					else if (e.X >= frameHorCenter - handleSize && e.X <= frameHorCenter + handleSize) Pos += 2;
					else if (e.X >= frameRight - handleSize && e.X <= frameRight + handleSize) Pos += 3;
					if (e.Y >= frameBottom - handleSize && e.Y <= frameBottom + handleSize) Pos += 10;
					else if (e.Y >= frameVerCenter - handleSize && e.Y <= frameVerCenter + handleSize) Pos += 20;
					else if (e.Y >= frameTop - handleSize && e.Y <= frameTop + handleSize) Pos += 30;
					switch (Pos)
					{
						case 11: lastCursorPosition = CursorPosition.OverHandleLB; return;
						case 12: lastCursorPosition = CursorPosition.OverHandleMB; return;
						case 13: lastCursorPosition = CursorPosition.OverHandleRB; return;
						case 21: lastCursorPosition = CursorPosition.OverHandleLM; return;
						case 22: lastCursorPosition = CursorPosition.OverHandleMM; return;
						case 23: lastCursorPosition = CursorPosition.OverHandleRM; return;
						case 31: lastCursorPosition = CursorPosition.OverHandleLT; return;
						case 32: lastCursorPosition = CursorPosition.OverHandleMT; return;
						case 33: lastCursorPosition = CursorPosition.OverHandleRT; return;
					}
				}
			}
			lastCursorHotspot = null;
			foreach (IHotSpot hsp in hotspots)
			{
				GeoPoint p = hsp.GetHotspotPosition();
				PointF pf = vw.Projection.ProjectF(p);
				if (e.X >= pf.X - handleSize && e.X <= pf.X + handleSize && e.Y >= pf.Y - handleSize && e.Y <= pf.Y + handleSize)
				{
					lastCursorPosition = CursorPosition.OverHotSpot;
					lastCursorHotspot = hsp;
					vw.Canvas.ShowToolTip(lastCursorHotspot.GetInfoText(InfoLevelMode.DetailedInfo));
					return;
				}
			}
			// hier angekommen heisst: kein Handle getroffen
			GeoObjectList ObjectsUnderCursor = this.ObjectsUnderCursor(e, vw, false);
			if (directFeedback) objectsUnderCursorFeedback = this.FeedbackObjectsUnderCursor(e, vw);
			if (ObjectsUnderCursor.Count > 0)
			{
				Set<IGeoObject> s1 = new Set<IGeoObject>();
				Set<IGeoObject> s2 = new Set<IGeoObject>();
				foreach (IGeoObject go in selectedObjects)
				{
					s1.Add(go);
				}
				foreach (IGeoObject go in ObjectsUnderCursor)
				{
					s2.Add(go);
				}
				if (s1.Intersection(s2).Count > 0)
				{
					lastCursorPosition = CursorPosition.OverSelectedObject;
				}
				else
				{
					lastCursorPosition = CursorPosition.OverObject;
				}
			}
			if (ObjectsUnderCursor.Count == 1)
			{
				// vw.Canvas.ShowToolTip(ObjectsUnderCursor[0].Description);
			}
			else
			{
				vw.Canvas.ShowToolTip(null);
			}
		}
		private string GetCursor(MouseEventArgs e, IView vw)
		{
			if (mode == Mode.DragRect) return "SmallMove";
			if (mode == Mode.NoAction || mode == Mode.TestDragRect) GetCursorPosition(e, vw);
			switch (lastCursorPosition)
			{
				case CursorPosition.EmptySpace: return "Arrow";
				case CursorPosition.OverObject: return "Hand";
				case CursorPosition.OverSelectedObject: return "Hand";
				case CursorPosition.OverFixPoint: return "Needle";
				case CursorPosition.OverHandleLB: return "SizeNESW";
				case CursorPosition.OverHandleLT: return "SizeNWSE";
				case CursorPosition.OverHandleRB: return "SizeNWSE";
				case CursorPosition.OverHandleRT: return "SizeNESW";
				case CursorPosition.OverHandleLM:
				case CursorPosition.OverHandleRM: return "SizeWE";
				case CursorPosition.OverHandleMB:
				case CursorPosition.OverHandleMT: return "SizeNS";
				case CursorPosition.OverHandleMM: return "SizeAll";
				case CursorPosition.OverHotSpot: return "Pen";

			}
			return "Arrow";
		}
		private void ActivateObjects()
		{
			activeObjects.Clear();
			cumulatedDrag = ModOp.Identity;
			for (int i = 0; i < selectedObjects.Count; ++i)
			{   // wichtig ist hier, dass die Nummerierung bestehen bleibt, da später
				// mit gleichem Index CopyGeometry aufgerufen wird
				IGeoObject go = selectedObjects[i] as IGeoObject; // muss gehen
				activeObjects.Add(go.Clone());
			}
			foreach (IView vw in base.Frame.AllViews)
			{
				//vw.SetPaintHandler(PaintBuffer.DrawingAspect.Active, new RepaintView(OnRepaintActive));
			}
		}
		private void UnActivateObjects()
		{
			activeObjects.Clear();
			foreach (IView vw in base.Frame.AllViews)
			{
				//vw.RemovePaintHandler(PaintBuffer.DrawingAspect.Active, new RepaintView(OnRepaintActive));
				vw.Invalidate(PaintBuffer.DrawingAspect.Active, vw.DisplayRectangle);
			}
		}
		/// <summary>
		/// Implements <see cref="Action.OnMouseMove"/>. If you override this method
		/// don't forget to call the bas implementation.
		/// </summary>
		/// <param name="e"><paramref name="Action.OnMouseMove.e"/></param>
		/// <param name="vw"><paramref name="Action.OnMouseMove.vw"/></param>
		public override void OnMouseMove(MouseEventArgs e, IView vw)
		{
			if (FilterMouseMessagesEvent != null)
			{
				bool handled = false;
				FilterMouseMessagesEvent(MouseAction.MouseMove, e, vw, ref handled);
				if (handled) return;
			}
			if (mode == Mode.TestDragRect && e.Button == MouseButtons.Left)
			{
				if (Math.Abs(e.X - downPoint.X) > dragWidth || Math.Abs(e.Y - downPoint.Y) > dragWidth)
				{
					if (lastCursorPosition == CursorPosition.EmptySpace || lastCursorPosition == CursorPosition.OverObject)
					{
						mode = Mode.DragRect;
						FirstPoint = downPoint;
						SecondPoint = new Point(e.X, e.Y);
					}
					else if (canStartDrag)
					{
						GeoObjectList dragList = new GeoObjectList(selectedObjects.Count);
						if (FilterDragListEvent != null) FilterDragListEvent(selectedObjects); // muss auf Selected Objects laufen, denn move greift darau zu
						for (int i = 0; i < selectedObjects.Count; i++)
							dragList.Add(selectedObjects[i].Clone());
						if (dragList.Count == 0) return;
						dragList.UserData.Add("DragDownPoint", vw.Projection.DrawingPlanePoint(downPoint));
						Guid tmpguid = Guid.NewGuid();
						dragList.UserData.Add("DragGuid", tmpguid);
						Frame.ActiveView.Model.UserData.Add("DragGuid", tmpguid);
						using (Frame.Project.Undo.UndoFrame)
						{
							DragDropEffects res = vw.DoDragDrop(dragList, DragDropEffects.All);
							if (res == DragDropEffects.Move)
							{
								//bool samemodel = false;
								//if (Frame.ActiveView.Model.UserData.Contains("DragGuid"))
								//{
								//    Guid guid = (Guid)Frame.ActiveView.Model.UserData.GetData("DragGuid");
								//    samemodel = guid == tmpguid;
								//}
								if (Frame.ActiveView.Model.UserData.ContainsData("DragGuid:" + tmpguid.ToString()))
								{   // im selben Modell verschoben
									ModOp move = (ModOp)Frame.ActiveView.Model.UserData.GetData("DragGuid:" + tmpguid.ToString());
									selectedObjects.Modify(move);
									Frame.ActiveView.Model.UserData.Remove("DragGuid:" + tmpguid.ToString());
								}
								else
								{   // in ein anderes Model verschoben
									GeoObjectList sel = selectedObjects.Clone();
									selectedObjects.Clear();
									Frame.ActiveView.Model.Remove(sel);
									vw.InvalidateAll();
									SetSelectedObjects(dragList);
								}
							}
							else
							{   // eine Kopie wurde eingefügt, das Original bleibt weiterhin selektiert
							}
						}
						Frame.ActiveView.Model.UserData.Remove("DragGuid");
					}
				}
			}
			if (mode == Mode.TestHotSpot && e.Button == MouseButtons.Left && lastCursorHotspot != null)
			{
				int dx = 4; // was System.Windows.Forms.SystemInformation.DoubleClickSize.Width;
				int dy = 4; // was System.Windows.Forms.SystemInformation.DoubleClickSize.Height;
				if (Math.Abs(e.X - downPoint.X) > dx || Math.Abs(e.Y - downPoint.Y) > dy)
				{
					mode = Mode.NoAction;
					lastCursorHotspot.IsSelected = true;
					lastCursorHotspot.StartDrag(Frame);
				}
			}
			if (e.Button == MouseButtons.None)
			{
				vw.SetCursor(GetCursor(e, vw));
				if (directFeedback)
				{
					vw.Invalidate(PaintBuffer.DrawingAspect.Active, vw.DisplayRectangle);
				}
			}
			if (e.Button == MouseButtons.Left)
			{
				switch (mode)
				{
					case Mode.DragRect:
						{
							Rectangle Clip = PaintBuffer.RectangleFromPoints(FirstPoint, SecondPoint, new Point(e.X, e.Y));
							Clip.Width += 1;
							Clip.Height += 1;
							SecondPoint = new Point(e.X, e.Y);
							vw.Invalidate(PaintBuffer.DrawingAspect.Select, Clip);
						}
						break;
					case Mode.DragMove:
						{
							GeoPoint newPoint = WorldPoint(e, vw);
							ModOp m = ModOp.Translate(newPoint - lastPoint);
							lastPoint = newPoint;
							activeObjects.Modify(m);
							cumulatedDrag = m * cumulatedDrag;
							vw.Invalidate(PaintBuffer.DrawingAspect.Active, vw.DisplayRectangle);
						}
						break;
					case Mode.DragSize:
						{
							GeoPoint newPoint = WorldPoint(e, vw);
							BoundingRect ext = activeObjects.GetExtent(vw.Projection, false, false);
							GeoPoint2D newPointPr = vw.Projection.ProjectionPlane.Project(newPoint);
							double dx, dy;
							if (ext.Width > 0.0) dx = Math.Abs(newPointPr.x - fixPoint.x) / ext.Width;
							else dx = 0.0;
							if (ext.Height > 0.0) dy = Math.Abs(newPointPr.y - fixPoint.y) / ext.Height;
							else dy = 0.0;
							ModOp m = ModOp.Scale(vw.Projection.DrawingPlane.ToGlobal(fixPoint), Math.Max(dx, dy));
							lastPoint = newPoint;
							activeObjects.Modify(m);
							cumulatedDrag = m * cumulatedDrag;
							vw.Invalidate(PaintBuffer.DrawingAspect.Active, vw.DisplayRectangle);
						}
						break;
					case Mode.DragWidth:
						{
							GeoPoint newPoint = WorldPoint(e, vw);
							BoundingRect ext = activeObjects.GetExtent(vw.Projection, false, false);
							GeoPoint2D newPointPr = vw.Projection.ProjectionPlane.Project(newPoint);
							double dx;
							if (ext.Width > 0.0)
							{
								dx = Math.Abs(newPointPr.x - fixPoint.x) / ext.Width;
								ModOp m = ModOp.Scale(vw.Projection.DrawingPlane.ToGlobal(fixPoint), vw.Projection.DrawingPlane.ToGlobal(GeoVector2D.XAxis), dx);
								lastPoint = newPoint;
								activeObjects.Modify(m);
								cumulatedDrag = m * cumulatedDrag;
								vw.Invalidate(PaintBuffer.DrawingAspect.Active, vw.DisplayRectangle);
							}
						}
						break;
					case Mode.DragHeight:
						{
							GeoPoint newPoint = WorldPoint(e, vw);
							BoundingRect ext = activeObjects.GetExtent(vw.Projection, false, false);
							GeoPoint2D newPointPr = vw.Projection.ProjectionPlane.Project(newPoint);
							double dy;
							if (ext.Height > 0.0)
							{
								dy = Math.Abs(newPointPr.y - fixPoint.y) / ext.Height;
								ModOp m = ModOp.Scale(vw.Projection.DrawingPlane.ToGlobal(fixPoint), vw.Projection.DrawingPlane.ToGlobal(GeoVector2D.YAxis), dy);
								lastPoint = newPoint;
								activeObjects.Modify(m);
								cumulatedDrag = m * cumulatedDrag;
								vw.Invalidate(PaintBuffer.DrawingAspect.Active, vw.DisplayRectangle);
							}
						}
						break;
				}
			}
		}
		/// <summary>
		/// Implements <see cref="Action.OnMouseUp"/>. If you override this method
		/// don't forget to call the bas implementation.
		/// </summary>
		/// <param name="e"><paramref name="Action.OnMouseUp.e"/></param>
		/// <param name="vw"><paramref name="Action.OnMouseUp.vw"/></param>
		public override void OnMouseUp(MouseEventArgs e, IView vw)
		{
			if (FilterMouseMessagesEvent != null)
			{
				bool handled = false;
				FilterMouseMessagesEvent(MouseAction.MouseUp, e, vw, ref handled);
				if (handled) return;
			}
			if (e.Button == MouseButtons.Left)
			{
				if (mode == Mode.DragRect)
				{
					GeoObjectList found = ObjectsInsideDragRect(vw);
					if (Frame.UIService.ModifierKeys == Keys.Control || accumulateObjects)
					{
						GeoObjectList l = selectedObjects.Clone();
						SetSelectedObjects(new GeoObjectList()); // leer
						l.AddRangeUnique(found);
						SetSelectedObjects(l);
					}
					else
					{
						SetSelectedObjects(found);
					}
					Rectangle Clip = PaintBuffer.RectangleFromPoints(FirstPoint, SecondPoint);
					Clip.Width += 1;
					Clip.Height += 1;
					vw.Invalidate(PaintBuffer.DrawingAspect.Select, Clip);
				}
				else if (mode == Mode.DragMove)
				{
					if (Frame.UIService.ModifierKeys == Keys.Control || accumulateObjects)
					{
						Frame.ActiveView.Model.Add(activeObjects);
						SetSelectedObjects(activeObjects);
						UnActivateObjects();
					}
					else
					{
						UnActivateObjects();
						GeoObjectList l = selectedObjects.Clone();
						ClearSelectedObjects(); // Löschen und setzen, weil Kreise zu Ellipsen werden können und somit andere Propeties gebraucht werden
						l.Modify(cumulatedDrag);
						SetSelectedObjects(l);
					}
				}
				else if (mode == Mode.DragSize || mode == Mode.DragHeight || mode == Mode.DragWidth)
				{
					UnActivateObjects();
					GeoObjectList l = selectedObjects.Clone();
					ClearSelectedObjects(); // Löschen und setzen, weil Kreise zu Ellipsen werden können und somit andere Propeties gebraucht werden
					l.Modify(cumulatedDrag);
					SetSelectedObjects(l);
				}
				else
				{
					if (lastCursorPosition == CursorPosition.OverHotSpot && lastCursorHotspot != null)
					{
						lastCursorHotspot.IsSelected = true;
					}
					else
					{
						if (Frame.UIService.ModifierKeys == Keys.Control || accumulateObjects)
						{
							GeoObjectList undercursor = ObjectsUnderCursor(e, vw, true);
							GeoObjectList toSelect = selectedObjects.Clone();
							foreach (IGeoObject go in undercursor)
							{
								if (toSelect.Contains(go))
								{
									toSelect.Remove(go);
									// RemoveSelectedObject(go);
								}
								else
								{
									toSelect.Add(go);
									// AddSelectedObject(go);
								}
							}
							SetSelectedObjects(toSelect);
						}
						else if (Frame.UIService.ModifierKeys == Keys.Shift)
						{
							GeoObjectList undercursor = ObjectsUnderCursor(e, vw, true);
							if (undercursor.Count == 1 && selectedObjects.Contains(undercursor[0]))
							{
								selectedObjectsProperty.Focus(undercursor[0]);
							}
						}

						else
						{
							SetSelectedObjects(ObjectsUnderCursor(e, vw, true));
						}

					}
				}
				mode = Mode.NoAction;
			}
			else if (e.Button == MouseButtons.Right)
			{
				if (vw is IView)
				{
					if (!(vw as IView).AllowContextMenu) return;
				}
				if (lastCursorHotspot != null)
				{
					MenuWithHandler[] cm = lastCursorHotspot.ContextMenu;
					if (cm != null)
					{
						vw.Canvas.ShowContextMenu(cm, new Point(e.X, e.Y));
					}
				}
				else
				{
					List<MenuWithHandler> cm = new List<MenuWithHandler>(MenuResource.LoadMenuDefinition("MenuId.SelectedObjects", false, Frame.CommandHandler));
					if (selectedObjects.Count == 1)
					{
						for (int i = 0; i < selectedObjectsProperty.SubEntriesCount; i++)
						{
							if (selectedObjectsProperty.SubEntries[i] is IGeoObjectShowProperty)
							{
								IGeoObjectShowProperty gsp = selectedObjectsProperty.SubEntries[i] as IGeoObjectShowProperty;
								if (gsp.GetGeoObject() == selectedObjects[0])
								{
									MenuWithHandler[] sub = MenuResource.LoadMenuDefinition(gsp.GetContextMenuId(), false, gsp as ICommandHandler);
									cm.AddRange(sub);
									break;
								}
							}
						}
					}

					var cmArray = cm.ToArray();
					BeforeShowContextMenuEvent?.Invoke(selectedObjects, cmArray);
					vw.Canvas.ShowContextMenu(cmArray, new Point(e.X, e.Y));
				}
			}
		}
		/// <summary>
		/// Implements <see cref="Action.OnMouseDown"/>. If you override this method
		/// don't forget to call the bas implementation.
		/// </summary>
		/// <param name="e"><paramref name="Action.OnMouseDown.e"/></param>
		/// <param name="vw"><paramref name="Action.OnMouseDown.vw"/></param>
		public override void OnMouseDown(MouseEventArgs e, IView vw)
		{
			if (FilterMouseMessagesEvent != null)
			{
				bool handled = false;
				FilterMouseMessagesEvent(MouseAction.MouseDown, e, vw, ref handled);
				if (handled) return;
			}
			if (e.Button == MouseButtons.Left)
			{
				this.GetCursorPosition(e, vw);
				BoundingRect ext = selectedObjects.GetExtent(vw.Projection, false, false);
				orgExtend = ext;
				switch (lastCursorPosition)
				{
					case CursorPosition.EmptySpace:
					case CursorPosition.OverSelectedObject:
					case CursorPosition.OverObject:
						mode = Mode.TestDragRect;
						break;
					case CursorPosition.OverHandleMM:
						mode = Mode.DragMove;
						ActivateObjects();
						break;
					case CursorPosition.OverHandleLB:
						mode = Mode.DragSize;
						fixPoint = ext.GetUpperRight();
						ActivateObjects();
						break;
					case CursorPosition.OverHandleRB:
						mode = Mode.DragSize;
						fixPoint = ext.GetUpperLeft();
						ActivateObjects();
						break;
					case CursorPosition.OverHandleLT:
						mode = Mode.DragSize;
						fixPoint = ext.GetLowerRight();
						ActivateObjects();
						break;
					case CursorPosition.OverHandleRT:
						mode = Mode.DragSize;
						fixPoint = ext.GetLowerLeft();
						ActivateObjects();
						break;
					case CursorPosition.OverHandleLM:
						mode = Mode.DragWidth;
						fixPoint = ext.GetMiddleRight();
						ActivateObjects();
						break;
					case CursorPosition.OverHandleRM:
						mode = Mode.DragWidth;
						fixPoint = ext.GetMiddleLeft();
						ActivateObjects();
						break;
					case CursorPosition.OverHandleMB:
						mode = Mode.DragHeight;
						fixPoint = ext.GetUpperMiddle();
						ActivateObjects();
						break;
					case CursorPosition.OverHandleMT:
						mode = Mode.DragHeight;
						fixPoint = ext.GetLowerMiddle();
						ActivateObjects();
						break;
					case CursorPosition.OverHotSpot:
						mode = Mode.TestHotSpot;
						break;
				}
				bool done = false;
				canStartDrag = false;
				if (selectedObjects.Count == 1)
				{
					BoundingRect pickrect = vw.Projection.BoundingRectWorld2d(e.X - pickRadius, e.X + pickRadius, e.Y + pickRadius, e.Y - pickRadius);
					if (selectedObjects[0].HitTest(vw.Projection, pickrect, false))
					{
						if (ClickOnSelectedObjectEvent != null)
						{
							ClickOnSelectedObjectEvent(selectedObjects[0], vw, e, ref done);
						}
						canStartDrag = vw.AllowDrag;
					}
				}
				else
				{
					BoundingRect pickrect = vw.Projection.BoundingRectWorld2d(e.X - pickRadius, e.X + pickRadius, e.Y + pickRadius, e.Y - pickRadius);
					for (int i = 0; i < selectedObjects.Count; i++)
					{
						if (selectedObjects[i].HitTest(vw.Projection, pickrect, false))
						{
							canStartDrag = vw.AllowDrag;
							break;
						}
					}
				}
				if (!done)
				{
					if (mode == Mode.NoAction) mode = Mode.TestDragRect;
					downPoint = new Point(e.X, e.Y);
					lastPoint = base.WorldPoint(e, vw);
				}
				else
				{
					mode = Mode.NoAction;
				}
			}
			else
			{
				mode = Mode.NoAction;
			}
		}
		/// <summary>
		/// Implements <see cref="Action.GetID"/>.
		/// </summary>
		public override string GetID()
		{
			return "SelectObjects";
		}
		/// <summary>
		/// Implements <see cref="Action.OnSetAction"/>. If you override this method
		/// don't forget to call the bas implementation.
		/// </summary>
		public override void OnSetAction()
		{   // Diese Aktion wird nicht vom Menue aus gesetzt, es entspricht ihr jedoch
			// der Button und das Menu "MenuId.Select". Deshalb hier die falschen Tatsachen vorspiegeln
			// als ob es vom menue aus gesetzt worden wäre.
			base.OnSetAction();
		}
		/// <summary>
		/// Returns true if the coordinate in the parameter e is inside a currently visible hotspot
		/// </summary>
		/// <param name="e">the parameter provided by the mouse event</param>
		/// <param name="vw">the view which issued the mouse event</param>
		/// <returns></returns>
		public bool IsOverHotSpot(MouseEventArgs e, IView vw)
		{
			foreach (IHotSpot hsp in hotspots)
			{
				GeoPoint p = hsp.GetHotspotPosition();
				PointF pf = vw.Projection.ProjectF(p);
				if (e.X >= pf.X - handleSize && e.X <= pf.X + handleSize && e.Y >= pf.Y - handleSize && e.Y <= pf.Y + handleSize)
				{
					return true;
				}
			}
			return false;
		}
		internal override int GetInfoProviderIndex(Point ScreenCursorPosition, IView View)
		{   // hier könnte man verschiedene Situationen regeln. z.Z. ist nur "Cursor Über Hotspot"
			// implementiert und wir liefern hier 1.
			if (lastCursorHotspot != null) return 1;
			return -1;
		}
		internal override string GetInfoProviderText(int Index, CADability.UserInterface.InfoLevelMode Level, IView View)
		{   // gesucht ist der ToolTip Text für den aktuellen Hotspot. Es wird einfach
			// der Label Text des zugehörigen IShowproperty genommen. Alternativ könnte in
			// IConstructProperty eine Methode eingefügt werden.
			if (Index == 1 && lastCursorHotspot != null)
			{
				return lastCursorHotspot.GetInfoText(Level);
			}
			return null;
		}
		internal override int GetInfoProviderVerticalPosition(int Index, IView View)
		{   // Der ToolTip erscheint gewöhnlich über dem Cursor (denn Hand und Pfeil-Cursor
			// befinden sich unterhalb ihres Zeigepunktes) Der Pen-Cursor ist aber oberhalb des
			// Zeigepunktes und deshalb soll der ToolTip unterhalb des Cursors erscheinen, sonst
			// wird der ToolTip vom Cursor überdeckt.
			if (Index == 1) return handleSize;
			return 0;
		}
		private void OnSettingChanged(string Name, object NewValue)
		{   // eine Einstellung wurde verändert
			switch (Name)
			{
				case "Select.HandleSize": handleSize = Settings.GetIntValue(NewValue); break;
				case "Select.FrameColor": frameColor = (NewValue as ColorSetting).Color; break;
				case "Select.HandleColor": handleColor = (NewValue as ColorSetting).Color; break;
				case "Select.UseFrame": useFrame = Settings.GetBoolValue(NewValue); break;
				case "Select.SelectColor": selectColor = (NewValue as ColorSetting).Color; break;
				case "Select.FocusColor": focusColor = (NewValue as ColorSetting).Color; break;
				case "Select.SelectWidth": selectWidth = Settings.GetIntValue(NewValue); break;
				case "Select.SelectTransparency": selectTransparency = Settings.GetIntValue(NewValue); break;
				case "Select.SelectAbove": selectAbove = Settings.GetBoolValue(NewValue); break;
				case "Select.DragWidth": dragWidth = Settings.GetIntValue(NewValue); break;
				case "Select.PickRadius": pickRadius = Settings.GetIntValue(NewValue); break;
				case "Select.FastFeedback": directFeedback = Settings.GetBoolValue(NewValue); break;
				default: return;
			}
			// neuzeichnen. Wenn es eine andere Einstellung war, dann hatten wir schon return.
			foreach (IView vw in base.Frame.AllViews)
			{
				vw.Invalidate(PaintBuffer.DrawingAspect.Select, vw.DisplayRectangle);
			}
		}
		private void OnWillChange(IGeoObject Sender, GeoObjectChange Change)
		{   // das Behandeln einer Eigenschaft eines markierten Objektes mit der Maus beginnt
			//			if (ChangeEventArg.mChangeType==GeoObjectChangeEvent.tChangeType.ModifyWithMouse &&
			//				(mode==Mode.NoAction || mode==Mode.TestDragRect))
			if (selectedObjectsProperty.IsChanging) return;
			if ((mode == Mode.NoAction || mode == Mode.TestDragRect))
			{
				mode = Mode.DragObjectProperty;
				foreach (IView vw in base.Frame.AllViews)
				{
					// das scheint nicht nötig zu sein und erzeugt gewaltige Zeitverzögerung
					// wenn mehrere tasuend objekte markiert sind und eine gemeinsame Eigenschaft geändert wird
					// vw.Invalidate(PaintBuffer.DrawingAspect.Select, GetFrameInvalidateRect(vw));
				}
			}
			if (mode == Mode.NoAction)
			{
				foreach (IView vw in base.Frame.AllViews)
				{
					vw.Invalidate(PaintBuffer.DrawingAspect.Select, GetFrameInvalidateRect(vw));
				}
			}
		}
		private void OnDidChange(IGeoObject Sender, GeoObjectChange Change)
		{   // das Behandeln einer Eigenschaft eines markierten Objektes mit der Maus endet
			//			if (ChangeEventArg.mChangeType==GeoObjectChangeEvent.tChangeType.ModifyWithMouse &&
			if (mode == Mode.DragObjectProperty)
			{
				mode = Mode.NoAction;
			}
			if (mode == Mode.NoAction)
			{
				//				selectedObjectsExtent = selectedObjects.GetExtent();
				//				RecalcFrameSize();
				//				DrwView.InvalidateSelect(GetFrameInvalidateRect());
			}
			//Sender.UserData.RemoveUserData("CADability.SelectList");
			displayList = null;
		}
		/// <summary>
		/// Implements <see cref="Action.OnDisplayChanged"/>. If you override this method
		/// don't forget to call the bas implementation.
		/// </summary>
		public override void OnDisplayChanged(DisplayChangeArg d)
		{
			throw new NotImplementedException();
			//			RecalcFrameSize();
			//			DrwView.InvalidateSelect(GetFrameInvalidateRect());
			//			base.OnDisplayChanged (d);
		}
		/// <summary>
		/// Implements <see cref="Action.OnViewsChanged"/>. If you override this method
		/// don't forget to call the bas implementation.
		/// </summary>
		public override void OnViewsChanged()
		{
			// das Abmelden bei den alten Views ist nicht notwendig, da diese 
			// ohnehin gelöscht werden
			foreach (IView vw in base.Frame.AllViews)
			{
				//vw.SetPaintHandler(PaintBuffer.DrawingAspect.Select, new RepaintView(OnRepaintSelect));
				vw.RemovePaintHandler(PaintBuffer.DrawingAspect.Select, new PaintView(OnRepaintSelect));
				vw.SetPaintHandler(PaintBuffer.DrawingAspect.Select, new PaintView(OnRepaintSelect));
			}
			ClearSelectedObjects();
			selectedObjectsProperty.Refresh();
		}
		/// <summary>
		/// Implements <see cref="Action.OnDelete"/>. This implementation initiates the menu command "MenuId.Object.Delete".
		/// </summary>
		public override bool OnDelete()
		{
			return OnCommand("MenuId.Object.Delete");
		}
		private void DecomposeAll()
		{   // wir wollen alle zerlegbaren Objekte einstufig zerlegen
			GeoObjectList sel = selectedObjects.Clone();
			ClearSelectedObjects();
			GeoObjectList newSelectedObjects = new GeoObjectList();
			using (Frame.Project.Undo.UndoFrame)
			{

				for (int i = 0; i < sel.Count; i++)
				{
					GeoObjectList l = sel[i].Decompose();
					if (l != null)
					{
						Frame.ActiveView.Model.Remove(sel[i]);
						Frame.ActiveView.Model.Add(l);
						newSelectedObjects.AddRange(l);
					}

					//if (sel[i] is Block)
					//{
					//    Block block = sel[i] as Block;
					//    Frame.ActiveView.Model.Remove(block);
					//    GeoObjectList l = block.Clear();
					//    Frame.ActiveView.Model.Add(l);
					//    newSelectedObjects.AddRange(l);
					//}
					//else if (sel[i] is BlockRef)
					//{   // BlockRef wird in einen Block zerlegt, also nicht aufgelöst, 
					//    // das geschieht erst beim 2. Mal
					//    BlockRef blRef = sel[i] as BlockRef;
					//    Block block = blRef.ReferencedBlock.Clone() as Block;
					//    block.Modify(blRef.Insertion);
					//    Frame.ActiveView.Model.Remove(blRef);
					//    Frame.ActiveView.Model.Add(block);
					//    newSelectedObjects.Add(block);
					//}
				}
				this.AddSelectedObjects(newSelectedObjects);
			}
		}
		/// <summary>
		/// Overrides <see cref="ICommandHandler.OnCommand"/>. See <see cref="ICommandHandler.OnCommand"/> for more information.
		/// </summary>
		/// <param name="MenuId"></param>
		/// <returns></returns>
		public override bool OnCommand(string MenuId)
		{
			if (!base.IsActive) return false;
			bool processed = false;
			Frame.ProcessContextMenu(this, MenuId, ref processed);
			if (processed) return true;
			switch (MenuId)
			{
				case "MenuId.SelectedObject.Delete":
					// kommt von einem ContextMenue eines markierten Objektes
					if (selectedObjectsProperty.ContextMenuSource != null)
					{
						GeoObjectList sel = selectedObjects.Clone();
						Frame.ActiveView.Model.Remove(selectedObjectsProperty.ContextMenuSource);
						sel.Remove(selectedObjectsProperty.ContextMenuSource);
						this.SetSelectedObjects(sel);
					}
					return true;
				case "MenuId.SelectedObject.DontSelect":
					if (selectedObjectsProperty.ContextMenuSource != null)
					{
						GeoObjectList sel = selectedObjects.Clone();
						sel.Remove(selectedObjectsProperty.ContextMenuSource);
						this.SetSelectedObjects(sel);
					}
					return true;
				case "MenuId.SelectedObject.SelectOnlyThis":
					if (selectedObjectsProperty.ContextMenuSource != null)
					{
						this.SetSelectedObjects(new GeoObjectList(selectedObjectsProperty.ContextMenuSource));
					}
					return true;
				case "MenuId.SelectedObject.ToBackground":
					if (selectedObjectsProperty.ContextMenuSource != null)
					{
						Frame.ActiveView.Model.MoveToBack(selectedObjectsProperty.ContextMenuSource);
					}
					return true;
				case "MenuId.SelectedObject.ToForeground":
					if (selectedObjectsProperty.ContextMenuSource != null)
					{
						Frame.ActiveView.Model.MoveToFront(selectedObjectsProperty.ContextMenuSource);
					}
					return true;
				case "MenuId.Object.Delete":
					{
						if (selectedObjects.Count == 0) return false;
						GeoObjectList sel = selectedObjects.Clone();
						ClearSelectedObjects(); // die Reihenfolge ist hier wichtig, sonst ist das Controlcenter überfordert
						using (Frame.Project.Undo.UndoFrame)
						{
							GeoObjectList toRemoveFromModel = new GeoObjectList();
							for (int i = 0; i < sel.Count; i++)
							{
								if (sel[i].Owner is Model)
								{
									if (sel[i].Owner == Frame.ActiveView.Model) toRemoveFromModel.Add(sel[i]);
									else (sel[i].Owner as Model).Remove(sel[i]); // kein Szenario bekannt, in dem dieses vorkommen kann
								}
								else if (sel[i].Owner is Block)
								{
									(sel[i].Owner as Block).Remove(sel[i]);
								}
							}
							if (toRemoveFromModel.Count > 0) Frame.ActiveView.Model.Remove(toRemoveFromModel);
						}
						// Frame.ActiveView.Model.Remove(sel); das brauchts doch nicht mehr
					}
					return true;
				case "MenuId.Align.Left":
					PositionObjects.AlignLeft(selectedObjects, Frame.ActiveView.Projection, Frame.Project);
					return true;
				case "MenuId.Align.Hcenter":
					PositionObjects.AlignHcenter(selectedObjects, Frame.ActiveView.Projection, Frame.Project);
					return true;
				case "MenuId.Align.Right":
					PositionObjects.AlignRight(selectedObjects, Frame.ActiveView.Projection, Frame.Project);
					return true;
				case "MenuId.Align.Top":
					PositionObjects.AlignTop(selectedObjects, Frame.ActiveView.Projection, Frame.Project);
					return true;
				case "MenuId.Align.Vcenter":
					PositionObjects.AlignVcenter(selectedObjects, Frame.ActiveView.Projection, Frame.Project);
					return true;
				case "MenuId.Align.Center":
					PositionObjects.AlignCenter(selectedObjects, Frame.ActiveView.Projection, Frame.Project);
					return true;
				case "MenuId.Align.Bottom":
					PositionObjects.AlignBottom(selectedObjects, Frame.ActiveView.Projection, Frame.Project);
					return true;
				case "MenuId.Space.Down":
					PositionObjects.SpaceDown(selectedObjects, Frame.ActiveView.Projection, Frame.Project);
					return true;
				case "MenuId.Space.Across":
					PositionObjects.SpaceAcross(selectedObjects, Frame.ActiveView.Projection, Frame.Project);
					return true;
				case "MenuId.Same.Width":
					PositionObjects.SameWidth(selectedObjects, Frame.ActiveView.Projection, Frame.Project);
					return true;
				case "MenuId.Same.Height":
					PositionObjects.SameHeight(selectedObjects, Frame.ActiveView.Projection, Frame.Project);
					return true;
				case "MenuId.Copy.Matrix":
					Frame.SetAction(new CopyMatrixObjects(selectedObjects));
					return true;
				case "MenuId.Copy.Circular":
					Frame.SetAction(new CopyCircularObjects(selectedObjects));
					return true;
				case "MenuId.Save.Symbol":
					{
						throw new ApplicationException("not implemented");
					}
				case "MenuId.Constr.Face.FromSelectedObject":
					{
						GeoObjectList select = new GeoObjectList(selectedObjects);
						ClearSelectedObjects();
						if (select.Count > 0)
						{
							Face fc = Face.MakeFace(select);
							if (fc != null)
							{
								using (Frame.Project.Undo.UndoFrame)
								{
									//Frame.ActiveView.Model.Remove(select);
									Frame.Project.SetDefaults(fc);
									Frame.ActiveView.Model.Add(fc);
									SetSelectedObjects(new GeoObjectList(fc));
								}
							}
							//Constr3DMakeFace temp = new Constr3DMakeFace();
							//GeoObjectList faceGeneric = temp.makeFaceDo(select, Frame);
							//if (faceGeneric == null)
							//    this.SetSelectedObjects(select);
							//else this.SetSelectedObjects(faceGeneric);
						}
					}
					return true;
				case "MenuId.Constr.Face.SelectedObjectExtrude":
					Frame.SetAction(new Constr3DPathExtrude(selectedObjects));
					//                    OnMakeSolid();
					return true;
				case "MenuId.Constr.Face.SelectedObjectRotate":
					Frame.SetAction(new Constr3DPathRotate(selectedObjects));
					return true;
				case "MenuId.Constr.Solid.SelectedFaceExtrude":
					Frame.SetAction(new Constr3DFaceExtrude(selectedObjects));
					//                    OnMakeSolid();
					return true;
				case "MenuId.Constr.Solid.SelectedFaceRotate":
					Frame.SetAction(new Constr3DFaceRotate(selectedObjects));
					return true;
				case "MenuId.Constr.Solid.SelectedRuledSolid":
					{
						GeoObjectList select = new GeoObjectList(selectedObjects);
						ClearSelectedObjects();
						if (select.Count == 1) Frame.SetAction(new Constr3DRuledSolid(select, Frame));
						else
						{
							//Constr3DRuledSolid temp = new Constr3DRuledSolid(select, Frame);
							//temp.ruledSolidDo(select, Frame);

							Constr3DRuledSolid.ruledSolidDo(select, Frame);
						}
					}
					return true;
				case "MenuId.Constr.Face.SelectedRuledFace":
					{
						GeoObjectList select = new GeoObjectList(selectedObjects);
						ClearSelectedObjects();
						if (select.Count == 1) Frame.SetAction(new Constr3DRuledFace(select, Frame));
						else
						{
							//Constr3DRuledSolid temp = new Constr3DRuledSolid(select, Frame);
							//temp.ruledSolidDo(select, Frame);

							Constr3DRuledFace.ruledFaceDo(select, Frame);
						}
					}
					return true;
				case "MenuId.SelectedObjects.SewFaces":
					{
						GeoObjectList select = new GeoObjectList(selectedObjects);
						GeoObjectList toAdd = Make3D.SewFacesAndShells(select);
						if (toAdd != null && toAdd.Count > 0)
						{
							ClearSelectedObjects();
							using (Frame.Project.Undo.UndoFrame)
							{
								Frame.ActiveView.Model.Remove(select);
								Frame.ActiveView.Model.Add(toAdd);
								SetSelectedObjects(toAdd);
							}
						}
					}
					return true;
				case "MenuId.Constr.Solid.Fuse":
					{
						GeoObjectList select = new GeoObjectList(selectedObjects);
						Solid s1 = null;
						Solid s2 = null;
						for (int i = 0; i < select.Count; ++i)
						{
							if (select[i] is Solid)
							{
								if (s1 == null) s1 = select[i] as Solid;
								else
								{
									s2 = select[i] as Solid;
									break;
								}
							}
						}
						if (s2 != null)
						{
							Solid toAdd = Solid.Unite(s1, s2);
							if (toAdd != null)
							{
								toAdd.CopyAttributes(s1);
								ClearSelectedObjects();
								using (Frame.Project.Undo.UndoFrame)
								{
									Frame.ActiveView.Model.Remove(select);
									Frame.ActiveView.Model.Add(toAdd);
									SetSelectedObjects(new GeoObjectList(toAdd));
								}
							}
						}
					}
					return true;
				case "MenuId.Constr.Solid.Intersect":
					{
						GeoObjectList select = new GeoObjectList(selectedObjects);
						Solid s1 = null;
						Solid s2 = null;
						for (int i = 0; i < select.Count; ++i)
						{
							if (select[i] is Solid)
							{
								if (s1 == null) s1 = select[i] as Solid;
								else
								{
									s2 = select[i] as Solid;
									break;
								}
							}
						}
						if (s2 != null)
						{
							Solid[] toAdd = Solid.Intersect(s1, s2);
							if (toAdd != null && toAdd.Length > 0)
							{
								for (int i = 0; i < toAdd.Length; ++i)
								{
									toAdd[i].CopyAttributes(s1);
								}
								ClearSelectedObjects();
								using (Frame.Project.Undo.UndoFrame)
								{
									Frame.ActiveView.Model.Remove(select);
									Frame.ActiveView.Model.Add(toAdd);
									SetSelectedObjects(new GeoObjectList((IGeoObject[])toAdd));
								}
							}
						}
					}
					return true;
				case "MenuId.Constr.Solid.Difference":
					{
						GeoObjectList select = new GeoObjectList(selectedObjects);
						Solid s1 = null;
						Solid s2 = null;
						for (int i = 0; i < select.Count; ++i)
						{
							if (select[i] is Solid)
							{
								if (s1 == null) s1 = select[i] as Solid;
								else
								{
									s2 = select[i] as Solid;
									break;
								}
							}
						}
						if (s2 != null)
						{
							Solid[] toAdd = Solid.Subtract(s1, s2);
							if (toAdd != null && toAdd.Length > 0)
							{
								for (int i = 0; i < toAdd.Length; ++i)
								{
									toAdd[i].CopyAttributes(s1);
								}
								ClearSelectedObjects();
								using (Frame.Project.Undo.UndoFrame)
								{
									Frame.ActiveView.Model.Remove(select);
									Frame.ActiveView.Model.Add(toAdd);
									SetSelectedObjects(new GeoObjectList((IGeoObject[])toAdd));
								}
							}
						}
					}
					return true;
				case "MenuId.Object.Move":
					OnMoveObjects();
					return true;
				case "MenuId.Object.Rotate":
					OnRotateObjects();
					return true;
				case "MenuId.Object.Scale":
					OnScaleObjects();
					return true;
				case "MenuId.Object.Reflect":
					OnReflectObjects();
					return true;
				case "MenuId.Object.Snap":
					OnSnapObjects();
					return true;
				case "MenuId.Object.Hatch":
					OnHatchObjects();
					return true;
				case "MenuId.Object.MakePath":
					OnMakePath();
					return true;
				case "MenuId.SelectedObjects.ComposeAll":
					{
						using (Frame.Project.Undo.UndoFrame)
						{
							Block block = Block.Construct();
							block.Name = "no name";
							GeoObjectList sel = new GeoObjectList(selectedObjects);
							ClearSelectedObjects();
							Frame.ActiveView.Model.Remove(sel);
							BoundingRect brect = sel.GetExtent(Frame.ActiveView.Projection, false, false);
							for (int i = 0; i < sel.Count; i++)
							{
								if (block.Layer == null) block.Layer = sel[i].Layer;
								block.Add(sel[i]);
							}
							GeoPoint2D gp = brect.GetCenter();
							block.RefPoint = new GeoPoint(gp.x, gp.y);
							Frame.ActiveView.Model.Add(block);
							SetSelectedObjects(new GeoObjectList(block));
							// den Fokus auf den Namen des Blockes setzen
							IShowProperty[] props = selectedObjectsProperty.SubEntries;
							for (int i = 0; i < selectedObjectsProperty.SubEntriesCount; i++)
							{
								ShowPropertyBlock blProp = props[i] as ShowPropertyBlock;
								if (blProp != null)
								{
									selectedObjectsProperty.ShowOpen(blProp);
									blProp.EditName();

								}
							}
						}
						return true;
					}
				case "MenuId.SelectedObjects.Decompose":
					DecomposeAll();
					return true;
				case "MenuId.SelectedObjects.Approximate":
					ApproximateAll();
					return true;
				case "MenuId.Edit.Cut":
					{
						if (selectedObjects.Count == 0) return false;
						Frame.UIService.SetClipboardData(selectedObjects, true);
						GeoObjectList lst = new GeoObjectList(selectedObjects);
						ClearSelectedObjects();
						Frame.ActiveView.Model.Remove(lst);
						return true;
					}
				case "MenuId.Edit.Copy":

					if (selectedObjects.Count == 0) return false;
					Frame.UIService.SetClipboardData(selectedObjects, true);
					//Clipboard.SetDataObject(selectedObjects, true);
					return true;
				case "MenuId.Edit.Paste":
					object data = Frame.UIService.GetClipboardData(typeof(GeoObjectList));
					if (data is GeoObjectList l) // System.Windows.Forms.DataFormats.Serializable))
					{
						// GeoObjectList l = data.GetData(typeof(GeoObjectList)) as GeoObjectList; // System.Windows.Forms.DataFormats.Serializable) as GeoObjectList;
						if (l != null && l.Count > 0)
							if (Settings.GlobalSettings.GetBoolValue("Select.KeepPasteActive", false))
							{
								PlaceObjects mo = new PlaceObjects(l, l.GetExtent().GetCenter());
								Frame.SetAction(mo);
							}
							else
							{
								using (Frame.Project.Undo.UndoFrame)
								{
									foreach (IGeoObject go in l)
									{
										if (go.Style != null && go.Style.Name == "CADability.EdgeStyle")
										{
											go.Style = Frame.Project.StyleList.Current;
										}
										AttributeListContainer.UpdateObjectAttrinutes(Frame.Project, go);
										go.UpdateAttributes(Frame.Project);
										Frame.ActiveView.Model.Add(go);
									}
								}
							}
						if (l != null) SetSelectedObjects(l);
					}
					return true;
				//case "MenuId.SelectedObjects.PreferEdges":
				//    pickEdges = !pickEdges;
				//    return true;
				case "MenuId.SelectedObjects.MakeFilter":
					OnMakeFilter();
					return true;
				case "MenuId.SelectedObjects.Mode.All":
					pickMode = PickMode.normal;
					return true;
				case "MenuId.SelectedObjects.Mode.FacesOnly":
					pickMode = PickMode.onlyFaces;
					return true;
				case "MenuId.SelectedObjects.Mode.EdgesOnly":
					pickMode = PickMode.onlyEdges;
					return true;
				default: return false;
			}

		}
		private void ApproximateAll()
		{
			GeoObjectList sel = selectedObjects.Clone();
			ClearSelectedObjects();
			GeoObjectList newSelectedObjects = new GeoObjectList();
			using (Frame.Project.Undo.UndoFrame)
			{
				Model addTo = Frame.ActiveView.Model;
				for (int i = 0; i < sel.Count; i++)
				{
					if (sel[i] is ICurve)
					{
						ICurve app = (sel[i] as ICurve).Approximate(Frame.GetIntSetting("Approximate.Mode", 0) == 0, Frame.GetDoubleSetting("Approximate.Precision", 0.01));
						addTo.Remove(sel[i]);
						IGeoObject go = app as IGeoObject;
						go.CopyAttributes(sel[i]);
						addTo.Add(go);
						newSelectedObjects.Add(go);
					}
				}
			}
			this.AddSelectedObjects(newSelectedObjects);
		}
		/// <summary>
		/// Overrides <see cref="ICommandHandler.OnUpdateCommand"/>. See <see cref="ICommandHandler.OnUpdateCommand"/> for more information.
		/// </summary>
		/// <param name="MenuId"></param>
		/// <returns></returns>
		public override bool OnUpdateCommand(string MenuId, CommandState CommandState)
		{
			bool processed = false;
			Frame.UpdateContextMenu(this, MenuId, CommandState, ref processed);
			if (processed) return true; ;
			switch (MenuId)
			{
				case "MenuId.Object.Reflect":
				case "MenuId.Object.Snap":
				case "MenuId.Object.Hatch":
				case "MenuId.Object.Scale":
				case "MenuId.Object.Rotate":
				case "MenuId.Object.Move":
				case "MenuId.Object.Delete":
				case "MenuId.Copy.Matrix":
				case "MenuId.Copy.Circular":
					CommandState.Enabled = (base.IsActive && selectedObjects.Count > 0);
					return true;
				case "MenuId.Constr.Face.SelectedObjectExtrude":
				case "MenuId.Constr.Face.SelectedObjectRotate":
					CommandState.Enabled = base.IsActive && Constr3DPathExtrude.pathTest(selectedObjects);
					return true;
				case "MenuId.Constr.Solid.SelectedFaceExtrude":
				case "MenuId.Constr.Face.FromSelectedObject":
					CommandState.Enabled = base.IsActive && Constr3DFaceExtrude.faceTestExtrude(selectedObjects);
					return true;
				case "MenuId.Constr.Solid.SelectedFaceRotate":
					CommandState.Enabled = base.IsActive && Constr3DFaceRotate.faceTestRotate(selectedObjects);
					return true;
				case "MenuId.Constr.Solid.SelectedRuledSolid":
					CommandState.Enabled = base.IsActive && Constr3DRuledSolid.ruledSolidTest(selectedObjects, Frame.ActiveView.Model);
					return true;
				case "MenuId.Constr.Face.SelectedRuledFace":
					CommandState.Enabled = base.IsActive && Constr3DRuledFace.ruledFaceTest(selectedObjects, Frame.ActiveView.Model);
					return true;
				case "MenuId.SelectedObjects.SewFaces":
					CommandState.Enabled = base.IsActive;
					if (base.IsActive)
					{
						// überprüfen, ob auf oberster Ebene mehrere face oder shell objekte sind
						int count = 0;
						for (int i = 0; i < selectedObjects.Count; i++)
						{
							if (selectedObjects[i] is Face || selectedObjects[i] is Shell) ++count;
						}
						CommandState.Enabled = count > 1; // mindestens zwei
					}
					return true;
				case "MenuId.Constr.Solid.Fuse":
					CommandState.Enabled = base.IsActive;
					if (base.IsActive)
					{
						// überprüfen, ob auf oberster Ebene mehrere face oder shell objekte sind
						int count = 0;
						for (int i = 0; i < selectedObjects.Count; i++)
						{
							if (selectedObjects[i] is Solid) ++count;
						}
						CommandState.Enabled = count > 1; // mindestens zwei
					}
					return true;
				case "MenuId.Constr.Solid.Intersect":
				case "MenuId.Constr.Solid.Difference":
					CommandState.Enabled = base.IsActive;
					if (base.IsActive)
					{
						// überprüfen, ob auf oberster Ebene mehrere face oder shell objekte sind
						int count = 0;
						for (int i = 0; i < selectedObjects.Count; i++)
						{
							if (selectedObjects[i] is Solid) ++count;
						}
						CommandState.Enabled = count == 2; // genau zwei
					}
					return true;
				case "MenuId.SelectedObjects.Decompose":
					CommandState.Enabled = false;
					if (base.IsActive)
					{
						for (int i = 0; i < selectedObjects.Count; i++)
						{
							if (selectedObjects[i] is Block) CommandState.Enabled = true;
							if (selectedObjects[i] is BlockRef) CommandState.Enabled = true;
						}
					}
					return true;
				case "MenuId.SelectedObjects.Approximate":
					CommandState.Enabled = base.IsActive;
					if (base.IsActive)
					{
						// überprüfen, ob auf oberster Ebene eine Kurve ist, aber nicht Linie oder Polylinie
						bool hasCurve = false;
						for (int i = 0; i < selectedObjects.Count; i++)
						{
							if (selectedObjects[i] is ICurve)
							{
								if (!(selectedObjects[i] is Line) && !(selectedObjects[i] is Polyline))
								{
									hasCurve = true;
									break;
								}
							}
						}
						CommandState.Enabled = hasCurve;
					}
					return true;
				case "MenuId.Align.Hcenter":
				case "MenuId.Align.Vcenter":
				case "MenuId.Align.Center":
					CommandState.Enabled = base.IsActive && (selectedObjects.Count > 0);
					return true;
				case "MenuId.Object.MakePath":
				case "MenuId.Align.Left":
				case "MenuId.Align.Right":
				case "MenuId.Align.Top":
				case "MenuId.Align.Bottom":
				case "MenuId.Same.Width":
				case "MenuId.Same.Height":
				case "MenuId.Save.Symbol":
				case "MenuId.SelectedObjects.ComposeAll":
					CommandState.Enabled = base.IsActive && (selectedObjects.Count > 1);
					return true;
				case "MenuId.Space.Down":
				case "MenuId.Space.Across":
					CommandState.Enabled = base.IsActive && (selectedObjects.Count > 2);
					return true;
				//case "MenuId.SelectedObjects.PreferEdges":
				//    CommandState.Enabled = base.IsActive;
				//    CommandState.Checked = pickEdges;
				//    return true;
				case "MenuId.Edit.Copy":
				case "MenuId.Edit.Cut":
					CommandState.Enabled = selectedObjects.Count > 0;
					return true;
				case "MenuId.Edit.Paste":
					if (!Settings.GlobalSettings.GetBoolValue("DontCheckClipboardContent", false))
					{
						CommandState.Enabled = Frame.UIService.HasClipboardData(typeof(GeoObjectList));
					}
					return true;
				case "MenuId.SelectedObjects.Mode.All":
					CommandState.Checked = pickMode == PickMode.normal;
					return true;
				case "MenuId.SelectedObjects.Mode.FacesOnly":
					CommandState.Checked = pickMode == PickMode.onlyFaces;
					return true;
				case "MenuId.SelectedObjects.Mode.EdgesOnly":
					CommandState.Checked = pickMode == PickMode.onlyEdges;
					return true;
				default: return base.OnUpdateCommand(MenuId, CommandState);
			}
		}
		private void OnMakePath()
		{
			CADability.GeoObject.Path NewPath = CADability.GeoObject.Path.Construct();
			using (Frame.Project.Undo.UndoFrame)
			{
				GeoObjectList sel = new GeoObjectList(selectedObjects);
				if (selectedObjectsProperty.focusedSelectedObject != null)
				{   // dann den Pfad mit diesem beginnen
					sel.MoveToBack(selectedObjectsProperty.focusedSelectedObject);
				}
				ClearSelectedObjects();
				GeoObjectList cloned = sel.CloneObjects();
				for (int i = 0; i < cloned.Count; i++)
				{
					cloned[i].UserData.Add("CADability.temp.original", sel[i]);
				}
				// wir machen clones, sonst geht das mit dem UNDO nicht richtig (wenn man den Pfad nachher verschiebt und 2 mal undo drückt).
				if (NewPath.Set(cloned, true, this.Frame.GetDoubleSetting("Path.MaxGap", Precision.eps)))
				{
					GeoObjectList toRemove = new GeoObjectList();
					for (int i = 0; i < NewPath.Count; i++)
					{
						if ((NewPath.Curve(i) as IGeoObject).UserData.Contains("CADability.temp.original"))
						{
							toRemove.Add((NewPath.Curve(i) as IGeoObject).UserData.GetData("CADability.temp.original") as IGeoObject);
							(NewPath.Curve(i) as IGeoObject).UserData.Remove("CADability.temp.original");
						}
					}
					Frame.ActiveView.Model.Remove(toRemove);
					NewPath.Flatten();
					Frame.ActiveView.Model.Add(NewPath);
					this.AddSelectedObject(NewPath);
				}
				else
				{
					Frame.UIService.ShowMessageBox(StringTable.GetString("Error.MakePath.Impossible"), StringTable.GetString("Error.MakePath.Title"), MessageBoxButtons.OK);
				}
			}
		}
		//private void OnMakeSolid()
		//{
		//    if (selectedObjects.Count == 1 && selectedObjects[0] is Face)
		//    {
		//        Face ToStartFrom = selectedObjects[0] as Face;
		//        ClearSelectedObjects();
		//        Frame.SetAction(new ConstrPrism(ToStartFrom));
		//    }
		//}
		private void OnMoveObjects()
		{
			Frame.SetAction(new MoveObjects(selectedObjects));
			// base.Frame.Project.GetActiveModel().Remove(selectedObjects);
			// TODO: die Objekte sollen hier nicht rausgenommen werden
			// Es ist noch nicht klar, ob Kopien oder Originale verschoben werden.
			// deshalb zuerst nur Kopien verschieben, und beim Einfügen entweder
			// die Kopien einfügen, oder die Original verändern.
			ClearSelectedObjects();
		}
		private void OnRotateObjects()
		{
			Frame.SetAction(new RotateObjects(selectedObjects));
			// base.Frame.Project.GetActiveModel().Remove(selectedObjects);
			// TODO: die Objekte sollen hier nicht rausgenommen werden
			// Es ist noch nicht klar, ob Kopien oder Originale verschoben werden.
			// deshalb zuerst nur Kopien verschieben, und beim Einfügen entweder
			// die Kopien einfügen, oder die Original verändern.
			ClearSelectedObjects();
		}
		private void OnScaleObjects()
		{
			Frame.SetAction(new ScaleObjects(selectedObjects));
			// base.Frame.Project.GetActiveModel().Remove(selectedObjects);
			// TODO: die Objekte sollen hier nicht rausgenommen werden
			// Es ist noch nicht klar, ob Kopien oder Originale verschoben werden.
			// deshalb zuerst nur Kopien verschieben, und beim Einfügen entweder
			// die Kopien einfügen, oder die Original verändern.
			ClearSelectedObjects();
		}
		private void OnReflectObjects()
		{
			Frame.SetAction(new ReflectObjects(selectedObjects));
			// base.Frame.Project.GetActiveModel().Remove(selectedObjects);
			// TODO: die Objekte sollen hier nicht rausgenommen werden
			// Es ist noch nicht klar, ob Kopien oder Originale verschoben werden.
			// deshalb zuerst nur Kopien verschieben, und beim Einfügen entweder
			// die Kopien einfügen, oder die Original verändern.
			ClearSelectedObjects();
		}
		private void OnSnapObjects()
		{
			Frame.SetAction(new SnapObjects(selectedObjects));
			// base.Frame.Project.GetActiveModel().Remove(selectedObjects);
			// TODO: die Objekte sollen hier nicht rausgenommen werden
			// Es ist noch nicht klar, ob Kopien oder Originale verschoben werden.
			// deshalb zuerst nur Kopien verschieben, und beim Einfügen entweder
			// die Kopien einfügen, oder die Original verändern.
			ClearSelectedObjects();
		}
		private void OnHatchObjects()
		{
			Plane pln;
			double prec = selectedObjects.GetExtent().Size * 1e-4;
			CompoundShape cs = CompoundShape.CreateFromList(selectedObjects, prec, out pln);
			if (cs != null)
			{   // die Ebene zur Zeichenebene ausrichten
				Plane OrgPlane = pln;
				pln.Align(Frame.ActiveView.Projection.DrawingPlane, false);
				HatchStyle hs = Frame.Project.HatchStyleList.Current;
				Hatch h = Hatch.Construct();
				h.CompoundShape = cs.Project(OrgPlane, pln);
				h.Plane = pln;
				h.HatchStyle = hs;
				h.Recalc();
				Frame.Project.GetActiveModel().Add(h);
				this.ClearSelectedObjects();
				this.AddSelectedObject(h); // damit steht es im ControlCenter und die Schraffurart kann geändert werden

				if (selectedObjectsProperty.SubEntriesCount == 1)
				{
					IPropertyPage ipd = base.Frame.GetPropertyDisplay("Action");
					ShowPropertyHatch sph = selectedObjectsProperty.SubEntries[0] as ShowPropertyHatch;
					if (sph != null)
					{
						ipd.OpenSubEntries(sph, true);
						ipd.MakeVisible(sph.GetHatchStyleProperty() as IPropertyEntry);
						ipd.SelectEntry(sph.GetHatchStyleProperty() as IPropertyEntry);
					}

				}
			}
			else
			{
				Frame.UIService.ShowMessageBox(StringTable.GetString("Error.HatchObjects.Impossible"), StringTable.GetString("Error.HatchObjects.Title"), MessageBoxButtons.OK);
			}
		}
		private void OnMakeFilter()
		{
			Filter f = Frame.Project.FilterList.AddNewFilter();
			for (int i = 0; i < selectedObjects.Count; ++i)
			{
				IGeoObject go = selectedObjects[i];
				ILayer ilayer = go as ILayer;
				if (ilayer != null && ilayer.Layer != null) f.Add(ilayer.Layer);
				IColorDef iColorDef = go as IColorDef;
				if (iColorDef != null && iColorDef.ColorDef != null) f.Add(iColorDef.ColorDef);
				ILineWidth iLineWidth = go as ILineWidth;
				if (iLineWidth != null && iLineWidth.LineWidth != null) f.Add(iLineWidth.LineWidth);
				ILinePattern iLinePattern = go as ILinePattern;
				if (iLinePattern != null && iLinePattern.LinePattern != null) f.Add(iLinePattern.LinePattern);
				IDimensionStyle iDimensionStyle = go as IDimensionStyle;
				if (iDimensionStyle != null && iDimensionStyle.DimensionStyle != null) f.Add(iDimensionStyle.DimensionStyle);
				IHatchStyle iHatchStyle = go as IHatchStyle;
				if (iHatchStyle != null && iHatchStyle.HatchStyle != null) f.Add(iHatchStyle.HatchStyle);
			}
		}
		/// <summary>
		/// Will be called when a hotspot becomes visible/invisible or selected/deselected. Will also be called, when a hotspot has been moved as a result of the 
		/// change notification of the GeoObject. this is not the place to modify the GeoObject.
		/// </summary>
		/// <param name="sender">The hotspot that caused this notification</param>
		/// <param name="mode">The kind of the change operation</param>
		protected void OnHotspotChanged(IHotSpot sender, HotspotChangeMode mode)
		{
			switch (mode)
			{
				case HotspotChangeMode.Visible:
					if (!hotspots.Contains(sender)) hotspots.Add(sender);
					// wird leider bei Refresh mehrfach aufgerufen
					break;
				case HotspotChangeMode.Invisible:
					hotspots.Remove(sender);
					if (selectedHotspot == sender) selectedHotspot = null;
					break;
				case HotspotChangeMode.Selected:
					selectedHotspot = sender;
					break;
				case HotspotChangeMode.Deselected:
					if (selectedHotspot == sender) selectedHotspot = null;
					break;
				case HotspotChangeMode.Moved:
					// hier ist nix zu tun, update erfolgt durch Invaildate
					break;
			}
			foreach (IView vw in base.Frame.AllViews)
			{
				vw.Invalidate(PaintBuffer.DrawingAspect.Select, vw.DisplayRectangle);
			}
		}
		private void OnGeoObjectRemoved(IGeoObject go)
		{   // wird ein selektiertes Objekt aus dem Modell entfernt, so darf es
			// auch nicht mehr selektiert bleiben
			if (selectedObjects.Contains(go))
			{
				GeoObjectList newSelection = new GeoObjectList(selectedObjects);
				newSelection.Remove(go);
				SetSelectedObjects(newSelection);
			}
		}

		internal void SelectAllVisibleObjects(IView ActiveView)
		{
			IActionInputView pm = ActiveView as IActionInputView;
			List<Layer> vl = new List<Layer>(pm.GetVisibleLayers());
			GeoObjectList toSelect = new GeoObjectList();
			for (int i = 0; i < ActiveView.Model.Count; i++)
			{
				if (ActiveView.Model[i].Layer == null || vl.Contains(ActiveView.Model[i].Layer))
				{
					toSelect.Add(ActiveView.Model[i]);
				}
			}
			SetSelectedObjects(toSelect);
		}

		/// <summary>
		/// Get the last hotspot under the cursor
		/// </summary>
		public IHotSpot LastCursorHotspot
		{
			get { return lastCursorHotspot; }
		}

		/// <summary>
		/// Get the area the cursor is currently over.
		/// e.g. EmptySpace or Hotspot
		/// </summary>
		public CursorPosition LastCursorPosition
		{
			get { return lastCursorPosition; }
		}
	}
}
