using CADability.Curve2D;
using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Collections;
#if WEBASSEMBLY
using CADability.WebDrawing;
using Point = CADability.WebDrawing.Point;
#else
using System.Drawing;
using Point = System.Drawing.Point;
#endif
using MouseEventArgs = CADability.Substitutes.MouseEventArgs;
using MouseButtons = CADability.Substitutes.MouseButtons;
using Keys = CADability.Substitutes.Keys;
using KeyEventArgs = CADability.Substitutes.KeyEventArgs;

namespace CADability.Actions
{
	using Attribute;
	using Shapes;
	using System.Collections.Generic;
	using System.Threading;

	/// <summary>
	/// This interface is implemented by Actions that only temporary construct some input
	/// for other actions. It is used by those actions to stay active until the IIntermediateConstruction
	/// action terminates.
	/// </summary>

	public interface IIntermediateConstruction
	{
		/// <summary>
		/// Returns the property handled by this action
		/// </summary>
		/// <returns></returns>
		IPropertyEntry GetHandledProperty();
		bool Succeeded
		{
			get;
		}
	}

	/* Mehrfachauswahl von Lösungen:
     * gibt es mehrere Lösungen, so wird bei Rechtsklick "nächste Lösung" "vorige Lösung"
     * im Kontext-Menue angezeigt. Alternativ gibt es PgUp, PgDown als Auswahlmöglichkeit.
     * ConstructAction hat einen event, den es bei PgUp/Down feuert. Ist dieser Event
     * gesetzt, so ercheint in beliebigem Zusammenhang ein Kontextmenue
     * 
    */
	/// <summary>
	/// Exception thrown by <see cref="ConstructAction"/>.
	/// </summary>

	public class ConstructActionException : ApplicationException
	{
		internal ConstructActionException(string message)
			: base(message)
		{
		}
	}
	/// <summary>
	/// Base class for construct actions. 
	/// </summary>
	public abstract class ConstructAction : Action
		, IPropertyEntry
	{
		#region Default Objects
		/// <summary>
		/// A DefaultLength object is usually used in conjunction with a <see cref="LengthInput"/>
		/// object (see <see cref="LengthInput.DefaultLength"/>). It specifies a default value
		/// for the input field as long as the user didn't specify that input via
		/// keyboard or mouse. When the length input is locked, the value is saved in the corresponding DefaultLength
		/// object. DefaultLength objects are usually static in a ConstructAction to preserve that value
		/// from one instance of the action to the next instance.
		/// </summary>
		public class DefaultLength
		{
			private Action activeAction;
			private double length;
			private bool isDefined;
			/// <summary>
			/// How should the value be initialized
			/// </summary>
			public enum StartValue
			{
				/// <summary>
				/// The width of the currently visible view
				/// </summary>
				ViewWidth,
				/// <summary>
				/// The width of the currently visible view divided by 2
				/// </summary>
				ViewWidth2,
				/// <summary>
				/// The width of the currently visible view divided by 2
				/// </summary>
				ViewWidth4,
				/// <summary>
				/// The width of the currently visible view divided by 4
				/// </summary>
				ViewWidth6,
				/// <summary>
				/// The width of the currently visible view divided by 6
				/// </summary>
				ViewWidth8,
				/// <summary>
				/// The width of the currently visible view divided by 8
				/// </summary>
				ViewWidth40,
				/// <summary>
				/// The width of corresponding model
				/// </summary>
				ModelWidth,
				/// <summary>
				/// The width 0.0
				/// </summary>
				Zero
			}
			private StartValue startValue;
			/// <summary>
			/// Creates an uninitialized DefaultLength object.
			/// </summary>
			public DefaultLength()
			{
				length = 0.0;
				isDefined = false;
				startValue = StartValue.ViewWidth6;
				activeAction = null;
			}
			/// <summary>
			/// Creates an uninitialized DefaultLength object with a definition how to
			/// initialize upon first usage.
			/// </summary>
			/// <param name="sw">how to initilize</param>
			public DefaultLength(StartValue sw)
			{
				length = 0.0;
				isDefined = false;
				startValue = sw;
				activeAction = null;
			}
			internal void SetAction(Action a)
			{
				activeAction = a; // kann auch null sein
			}
			/// <summary>
			/// Converts to a double
			/// </summary>
			/// <param name="rl">convert this</param>
			/// <returns>the double value</returns>
			public static implicit operator double(DefaultLength rl)
			{
				if (rl.isDefined) return rl.length;
				int WindowSize = 400; // irgend ein Pixelmaß für ein Fenster (Breite+Höhe)/2
									  // wird meist gleich überschrieben
				double DeviceToWorldFactor = 1.0; // wird in den meisten Fällen überschrieben
				if (rl.activeAction == null)
				{
					if (FrameImpl.MainFrame != null)
					{
						if (FrameImpl.MainFrame.ActiveView != null)
						{
							Rectangle r = FrameImpl.MainFrame.ActiveView.DisplayRectangle;
							if ((r.Height + r.Width) > 2)
							{
								WindowSize = (r.Height + r.Width) / 2;
								DeviceToWorldFactor = FrameImpl.MainFrame.ActiveView.Projection.DeviceToWorldFactor;
							}
						}
					}
				}
				else
				{
					Rectangle r = rl.activeAction.Frame.ActiveView.DisplayRectangle;
					if ((r.Height + r.Width) > 2)
					{
						WindowSize = (r.Height + r.Width) / 2;
						DeviceToWorldFactor = rl.activeAction.Frame.ActiveView.Projection.DeviceToWorldFactor;
					}
				}
				switch (rl.startValue)
				{
					default:
					case StartValue.ViewWidth:
						return DeviceToWorldFactor * WindowSize;
					case StartValue.ViewWidth2:
						return DeviceToWorldFactor * WindowSize / 2.0;
					case StartValue.ViewWidth4:
						return DeviceToWorldFactor * WindowSize / 4.0;
					case StartValue.ViewWidth6:
						return DeviceToWorldFactor * WindowSize / 6.0;
					case StartValue.ViewWidth8:
						return DeviceToWorldFactor * WindowSize / 8.0;
					case StartValue.ViewWidth40:
						return DeviceToWorldFactor * WindowSize / 40.0;
					case StartValue.ModelWidth:
						throw new NotImplementedException();
					case StartValue.Zero:
						return 0.0;
				}
			}
			/// <summary>
			/// Returns the current value
			/// </summary>
			public double Length
			{
				get { return length; }
				set
				{
					isDefined = true;
					length = value;
				}
			}
			/// <summary>
			/// Locked if true. A locked input is considered fixed (see <see cref="InputObject.Fixed"/>)
			/// </summary>
			public bool Locked;
		}
		/// <summary>
		/// A DefaultAngle object is usually used in conjunction with a <see cref="AngleInput"/>
		/// object (see <see cref="AngleInput.DefaultAngle"/>). It specifies a default value
		/// for the input field as long as the user didn't specify that input via
		/// keybord or mouse. When the angle input is locked, the value is saved in the corresponding DefaultAngle
		/// object. DefaultAngle objects are usually static in a ConstructAction to preserve that value
		/// from one instance of the action to the next instance.
		/// </summary>
		public class DefaultAngle
		{
			private Action activeAction;
			private Angle angle;
			private bool isDefined;
			/// <summary>
			/// How should the value be initialized
			/// </summary>
			public enum StartValue
			{
				/// <summary>
				/// horizontal to right (0°)
				/// </summary>
				ToRight,
				/// <summary>
				/// upward (90°)
				/// </summary>
				ToTop,
				/// <summary>
				/// horizontal to left (180°)
				/// </summary>
				ToLeft,
				/// <summary>
				/// downwards (0°)
				/// </summary>
				ToBottom,
				/// <summary>
				/// 45°
				/// </summary>
				To45
			}
			private StartValue startValue;
			/// <summary>
			/// Creates an uninitialized DefaultAngle
			/// </summary>
			public DefaultAngle()
			{
				angle = 0.0;
				isDefined = false;
				startValue = StartValue.ToRight;
				activeAction = null;
			}
			/// <summary>
			/// Creates an uninitialized DefaultAngle object with a definition how to
			/// initialize upon first usage.
			/// </summary>
			/// <param name="sw">how to initilize</param>
			public DefaultAngle(StartValue sw)
			{
				angle = 0.0;
				isDefined = false;
				startValue = sw;
				activeAction = null;
			}
			internal void SetAction(Action a)
			{
				activeAction = a; // kann auch null sein
			}
			/// <summary>
			/// Converts to an Angle
			/// </summary>
			/// <param name="da">convert this</param>
			/// <returns></returns>
			public static implicit operator Angle(DefaultAngle da)
			{
				if (da.isDefined) return da.angle;
				switch (da.startValue)
				{
					default:
					case StartValue.ToRight:
						return new Angle(0.0);
					case StartValue.ToTop:
						return new Angle(Math.PI / 2.0);
					case StartValue.ToLeft:
						return new Angle(Math.PI);
					case StartValue.ToBottom:
						return new Angle(3.0 * Math.PI / 2.0);
					case StartValue.To45:
						return new Angle(Math.PI / 4.0);
				}
			}
			/// <summary>
			/// returns the Angle
			/// </summary>
			public Angle Angle
			{
				get { return angle; }
				set
				{
					isDefined = true;
					angle = value;
				}
			}
			/// <summary>
			/// Locked if true. A locked input is considered fixed (see <see cref="InputObject.Fixed"/>)
			/// </summary>
			public bool Locked;
		}
		/// <summary>
		/// A DefaultGeoPoint object is usually used in conjunction with a <see cref="GeoPointInput"/>
		/// object (see <see cref="GeoPointInput.DefaultGeoPoint"/>). It specifies a default value
		/// for the input field as long as the user didn't specify that input via
		/// keybord or mouse. When the point input is locked, the value is saved in the corresponding DefaultGeoPoint
		/// object. DefaultGeoPoint objects are usually static in a ConstructAction to preserve that value
		/// from one instance of the action to the next instance.
		/// </summary>
		public class DefaultGeoPoint
		{
			private Action activeAction;
			private GeoPoint point;
			private bool isDefined;
			/// <summary>
			/// How should the value be initialized
			/// </summary>
			public enum StartValue
			{
				/// <summary>
				/// the center of the current view.
				/// </summary>
				CenterView,
				/// <summary>
				/// tzhe center of the model.
				/// </summary>
				CenterModel
			}
			private StartValue startValue;
			/// <summary>
			/// Creates an uninitialized DefaultGeoPoint
			/// </summary>
			public DefaultGeoPoint()
			{
				point = new GeoPoint(0.0, 0.0, 0.0);
				isDefined = false;
				startValue = StartValue.CenterView;
				activeAction = null;
			}
			internal void SetAction(Action a)
			{
				activeAction = a; // kann auch null sein
			}
			/// <summary>
			///  Converts to a GeoPoint
			/// </summary>
			/// <param name="rg">convert this</param>
			/// <returns></returns>
			public static implicit operator GeoPoint(DefaultGeoPoint rg)
			{
				if (rg.isDefined) return rg.point;
				IView CondorView = null;
				if (rg.activeAction == null)
				{
					if (FrameImpl.MainFrame != null)
					{
						if (FrameImpl.MainFrame.ActiveView != null)
						{
							CondorView = FrameImpl.MainFrame.ActiveView;
						}
					}
				}
				else
				{
					CondorView = rg.activeAction.Frame.ActiveView;
				}
				if (CondorView == null) return new GeoPoint(0.0, 0.0, 0.0); // absoluter Notausgang, 
				if (!(CondorView is IActionInputView)) return new GeoPoint(0.0, 0.0, 0.0); // absoluter Notausgang, 
																						   // wenn es überhaupt keinen View gibt, sollte nie vorkommen
				switch (rg.startValue)
				{
					default:
					case StartValue.CenterView:
						Rectangle r = CondorView.DisplayRectangle;
						Point p = new Point(r.Left + r.Width / 2, r.Top + r.Height / 2);
						if (CondorView.Projection.Height > 0 && CondorView.Projection.Width > 0) return CondorView.Projection.DrawingPlanePoint(p);
						else return GeoPoint.Origin;
					case StartValue.CenterModel:
						throw new NotImplementedException();
				}
			}
			/// <summary>
			/// Returns or sets the current value
			/// </summary>
			public GeoPoint Point
			{
				get
				{
					return point;
				}
				set
				{
					isDefined = true;
					point = value;
				}
			}
			/// <summary>
			/// Locked if true. A locked input is considered fixed (see <see cref="InputObject.Fixed"/>)
			/// </summary>
			public bool Locked;
		}
		/// <summary>
		/// A DefaultGeoVector object is usually used in conjunction with a <see cref="GeoVectorInput"/>
		/// object (see <see cref="GeoVectorInput.DefaultGeoVector"/>). It specifies a default value
		/// for the input field as long as the user didn't specify that input via
		/// keybord or mouse. When the point input is locked, the value is saved in the corresponding DefaultGeoVector
		/// object. DefaultGeoVector objects are usually static in a ConstructAction to preserve that value
		/// from one instance of the action to the next instance.
		/// </summary>
		public class DefaultGeoVector
		{
			private Action activeAction;
			private GeoVector vector;
			private bool isDefined;
			/// <summary>
			/// How should the direction be initialized
			/// </summary>
			public enum StartDirection
			{
				/// <summary>
				/// direction of the x axis
				/// </summary>
				XAxis,
				/// <summary>
				/// direction of the y axis
				/// </summary>
				YAxis,
				/// <summary>
				/// direction of the z axis
				/// </summary>
				ZAxis
			}
			/// <summary>
			/// How should the Length be initialized
			/// </summary>
			public enum StartLength
			{
				/// <summary>
				/// length = 1.0
				/// </summary>
				UnitOne,
				/// <summary>
				/// the width of the current view
				/// </summary>
				ViewWidth,
				/// <summary>
				/// the width of the current view divided by 2
				/// </summary>
				ViewWidth2,
				/// <summary>
				/// the width of the current view divided by 4
				/// </summary>
				ViewWidth4,
				/// <summary>
				/// the width of the current view divided by 6
				/// </summary>
				ViewWidth6,
				/// <summary>
				/// the width of the current view divided by 8
				/// </summary>
				ViewWidth8,
				/// <summary>
				/// the width of the model
				/// </summary>
				ModelWidth
			}
			private StartDirection startDirection;
			private StartLength startLength;
			/// <summary>
			/// Creates an uninitialized DefaultGeoVector
			/// </summary>
			public DefaultGeoVector()
			{
				vector = new GeoVector(0.0, 0.0, 0.0);
				isDefined = false;
				startDirection = StartDirection.XAxis;
				startLength = StartLength.UnitOne;
				activeAction = null;
				startDirection = StartDirection.XAxis;
				startLength = StartLength.UnitOne;
			}
			/// <summary>
			/// Creates an uninitialized DefaultGeoVector with a description how to initialize.
			/// </summary>
			/// <param name="startDirection">initial direction</param>
			/// <param name="startLength">initial length</param>
			public DefaultGeoVector(StartDirection startDirection, StartLength startLength)
				: this()
			{
				this.startDirection = startDirection;
				this.startLength = startLength;
			}
			internal void SetAction(Action a)
			{
				activeAction = a; // kann auch null sein
			}
			/// <summary>
			/// converts to a GeoVector
			/// </summary>
			/// <param name="dgv">convert this</param>
			/// <returns></returns>
			public static implicit operator GeoVector(DefaultGeoVector dgv)
			{
				if (dgv.isDefined) return dgv.vector;
				GeoVector res;
				switch (dgv.startDirection)
				{
					default:
					case StartDirection.XAxis:
						res = new GeoVector(1.0, 0.0, 0.0);
						break;
					case StartDirection.YAxis:
						res = new GeoVector(1.0, 0.0, 0.0);
						break;
					case StartDirection.ZAxis:
						res = new GeoVector(1.0, 0.0, 0.0);
						break;
				}
				if (dgv.activeAction == null) return res;
				Rectangle r = dgv.activeAction.Frame.ActiveView.DisplayRectangle;
				int w = (r.Height + r.Width) / 2;
				if (w == 0) w = 8;
				double l;
				switch (dgv.startLength)
				{
					default:
					case StartLength.UnitOne:
						l = 1.0;
						break; // stimmt schon
					case StartLength.ViewWidth:
						l = dgv.activeAction.WorldLength(w);
						break;
					case StartLength.ViewWidth2:
						l = dgv.activeAction.WorldLength(w / 2.0);
						break;
					case StartLength.ViewWidth4:
						l = dgv.activeAction.WorldLength(w / 4.0);
						break;
					case StartLength.ViewWidth6:
						l = dgv.activeAction.WorldLength(w / 6.0);
						break;
					case StartLength.ViewWidth8:
						l = dgv.activeAction.WorldLength(w / 8.0);
						break;
					case StartLength.ModelWidth:
						throw new NotImplementedException();
				}
				return l * res;
			}
			/// <summary>
			/// Returns or sets the current value
			/// </summary>
			public GeoVector Vector
			{
				get
				{
					return vector;
				}
				set
				{
					isDefined = true;
					vector = value;
				}
			}
			/// <summary>
			/// Locked if true. A locked input is considered fixed (see <see cref="InputObject.Fixed"/>)
			/// </summary>
			public bool Locked;
		}
		/// <summary>
		/// A DefaultBoolean object is usually used in conjunction with a <see cref="BooleanInput"/>
		/// object (see <see cref="BooleanInput.defaultBoolean"/>). It specifies a default value
		/// for the input field as long as the user didn't specify that input via
		/// keybord or mouse. When the point input is locked, the value is saved in the corresponding DefaultBoolean
		/// object. DefaultBoolean objects are usually static in a ConstructAction to preserve that value
		/// from one instance of the action to the next instance.
		/// </summary>
		public class DefaultBoolean
		{
			private bool val;
			private bool isDefined;
			/// <summary>
			///  Creates an undefined DefaultBoolean
			/// </summary>
			public DefaultBoolean()
			{
				val = false;
				isDefined = false;
			}
			/// <summary>
			///  Creates a defined DefaultBoolean
			/// </summary>
			/// <param name="StartValue">start value</param>
			public DefaultBoolean(bool StartValue)
			{
				isDefined = false;
				val = StartValue;
			}
			/// <summary>
			/// Converts to a boolean
			/// </summary>
			/// <param name="db">convert this</param>
			/// <returns></returns>
			public static implicit operator bool(DefaultBoolean db)
			{
				if (db.isDefined) return db.val;
				else return false; // was solls?
			}
			/// <summary>
			///  Gets or sets the current value.
			/// </summary>
			public bool Boolean
			{
				get { return val; }
				set
				{
					isDefined = true;
					val = value;
				}
			}
		}
		/// <summary>
		/// A DefaultInteger object is usually used in conjunction with a <see cref="MultipleChoiceInput"/>
		/// object (see <see cref="MultipleChoiceInput.DefaultChoice"/>). It specifies a default value
		/// for the input field as long as the user didn't specify that input via
		/// keybord or mouse. When the point input is locked, the value is saved in the corresponding DefaultInteger
		/// object. DefaultInteger objects are usually static in a ConstructAction to preserve that value
		/// from one instance of the action to the next instance.
		/// </summary>
		public class DefaultInteger
		{
			private int val;
			private bool isDefined;
			/// <summary>
			/// Creates an undefined DefaultInteger
			/// </summary>
			public DefaultInteger()
			{
				val = 0;
				isDefined = false;
			}
			/// <summary>
			/// Creates a defined DefaultInteger
			/// </summary>
			/// <param name="StartValue">initial value</param>
			public DefaultInteger(int StartValue)
			{
				isDefined = false;
				val = StartValue;
			}
			/// <summary>
			///  Converts to an int
			/// </summary>
			/// <param name="di">convert this</param>
			/// <returns></returns>
			public static implicit operator int(DefaultInteger di)
			{
				if (di.isDefined) return di.val;
				else return 0; // was solls?
			}
			/// <summary>
			/// Gets or sets the current value.
			/// </summary>
			public int Integer
			{
				get { return val; }
				set
				{
					isDefined = true;
					val = value;
				}
			}
		}
		#endregion

		#region Input Objects
		/// <summary>
		/// Delegate for mouse clickt of the Input objects
		/// </summary>
		/// <param name="up"></param>
		/// <param name="MousePosition"></param>
		/// <param name="View"></param>
		public delegate void MouseClickDelegate(bool up, GeoPoint MousePosition, IView View);
		/// <summary>
		/// Der Zustand der linken Maustaste
		/// </summary>
		private enum MouseState { ClickDown, MoveDown, ClickUp, MoveUp, RightUp }
		private enum ActivationMode { Inactivate, BySelection, ByHotspot }
		/// <summary>
		/// Dieses Interface dient der inneren Kommunikation dieser Klasse mit den
		/// Input Objekten. Es ist nicht nach außen sichtbar 
		/// </summary>
		private interface IInputObject
		{
			void Init(ConstructAction a); // hier die Referenz nach außen halten (ConstructAction)
			void Refresh(); // Darstellung soll refresht werden (nur wenn nötig)
			IPropertyEntry BuildShowProperty(); // Die Darstellung im Controlcenter wird erzeugt
			IPropertyEntry GetShowProperty();
			bool OnEnter(IPropertyEntry sender, KeyEventArgs args);
			void OnMouse(MouseEventArgs e, MouseState mouseState, IView vw); // Reaktion auf Mausbewegung
			void Activate(ActivationMode activationMode); // dieses Objekt bekommt den Input oder verliert ihn
			bool AcceptInput(bool acceptOptional); // will dieses Objekt Input annehmen
			void MouseLeft(); // die Maus hat die Anzeigefläche verlassen.
			void OnActionDone(); // die Aktion wird explizit abgeschlossen
			bool HasHotspot { get; }
			GeoPoint HotspotPosition { get; }
			Image HotSpotIcon { get; }
			bool IsFixed();
			void SetFixed(bool isFixed);
		}
		/// <summary>
		/// Common base class for input objects for the ConstructAction (see <see cref="ConstructAction.SetInput"/>
		/// </summary>
		public abstract class InputObject
		{
			/// <summary>
			/// If set to true, the user cannot type in a value for this input.
			/// Default: false;
			/// </summary>
			public virtual bool ReadOnly
			{
				set
				{
					readOnly = value;
					AdjustHighlight();
				}
				get { return readOnly; }
			}
			private bool readOnly;
			private bool optional;
			/// <summary>
			/// If set to true, the user cannot modify this intput with the mouse.
			/// Default: false;
			/// </summary>
			public virtual bool Optional
			{
				set
				{
					optional = value;
					AdjustHighlight();
				}
				get { return optional; }
			}
			private bool isfixed;
			/// <summary>
			/// If true, there already was an input either from the keyboard or
			/// by a mouse click.
			/// </summary>
			public bool Fixed
			{
				set
				{
					isfixed = value;
					AdjustHighlight();
				}
				get { return isfixed; }
			}
			protected virtual void AdjustHighlight()
			{
			}
			/// <summary>
			/// The string id for several text strings: ResourceId+".Label": the Label left of the
			/// edit box. ResourceId+".ShortInfo": a short tooltip text ResourceId+"DetailedInfo":
			/// a longer tooltip text.
			/// </summary>
			public string ResourceId;
			/// <summary>
			/// Constructor to set the resourceId for that input field
			/// </summary>
			/// <param name="resourceId"></param>
			protected InputObject(string resourceId)
			{
				ReadOnly = false;
				Optional = false;
				Fixed = false;
				ResourceId = resourceId;
			}
			/// <summary>
			/// Back reference to the ConstructAction this input is used for.
			/// </summary>
			protected ConstructAction constructAction; // Rückverweis
			#region IInputObject Helper
			/// <summary>
			/// Internal!
			/// </summary>
			/// <param name="a">a</param>
			internal protected virtual void Init(ConstructAction a)
			{
				constructAction = a;
			}
			/// <summary>
			/// Internal!
			/// </summary>
			internal protected virtual void Refresh()
			{
				// TODO:  Add IInputObjectImpl.Refresh implementation
			}
			/// <summary>
			/// Internal!
			/// </summary>
			/// <returns>Internal!</returns>
			internal protected virtual bool AcceptInput(bool acceptOptional)
			{
				if (acceptOptional) return (!Fixed && !readOnly);
				else return (!Fixed && !Optional && !readOnly);
			}
			/// <summary>
			/// Internal!
			/// </summary>
			internal protected virtual void MouseLeft()
			{
			}
			/// <summary>
			/// Internal!
			/// </summary>
			internal protected virtual void OnActionDone()
			{
				// TODO:  Add IInputObjectImpl.OnActionDone implementation
			}
			/// <summary>
			/// Internal!
			/// </summary>
			internal protected virtual bool HasHotspot
			{
				get
				{
					return false;
				}
			}
			/// <summary>
			/// Internal!
			/// </summary>
			internal protected virtual GeoPoint HotspotPosition
			{
				get
				{
					return GeoPoint.Origin;
				}
			}
			public virtual void SetError(string errorMessage)
			{

			}

			#endregion
		}
		public class SeparatorInput : InputObject, IInputObject
		{
			public SeparatorInput(string resourceId)
				: base(resourceId)
			{
			}
			#region IInputObject Members

			void IInputObject.Init(ConstructAction a)
			{
			}

			void IInputObject.Refresh()
			{
			}
			SeperatorProperty sepprop;
			IPropertyEntry IInputObject.BuildShowProperty()
			{
				sepprop = new SeperatorProperty(ResourceId);
				return sepprop;
			}

			void IInputObject.OnMouse(MouseEventArgs e, MouseState mouseState, IView vw)
			{
			}

			void IInputObject.Activate(ActivationMode activationMode)
			{
			}

			bool IInputObject.AcceptInput(bool acceptOptional)
			{
				return false;
			}

			void IInputObject.MouseLeft()
			{
			}

			void IInputObject.OnActionDone()
			{
			}
			IPropertyEntry IInputObject.GetShowProperty()
			{
				return sepprop;
			}
			bool IInputObject.OnEnter(IPropertyEntry sender, KeyEventArgs args)
			{
				return false;
			}
			bool IInputObject.HasHotspot
			{
				get { return false; }
			}

			GeoPoint IInputObject.HotspotPosition
			{
				get { return GeoPoint.Origin; }
			}

			Image IInputObject.HotSpotIcon
			{
				get { return null; }
			}

			bool IInputObject.IsFixed()
			{
				return true;
			}

			void IInputObject.SetFixed(bool isFixed)
			{
			}

			public override void SetError(string errorMessage)
			{
				// no error on this input possible
			}

			#endregion
		}
		public class InputContainer : InputObject, IInputObject
		/// <summary>
		/// Input object for the ConstructAction that yields an <see cref="Angle"/>.
		/// See <see cref="ConstructAction.SetInput"/>
		/// </summary>
		{
			SimplePropertyGroup simplePropertyGroup; // der Container
			IPropertyEntry[] contents; // der Inhalt, wenn es noch keinen Container gibt
			public InputContainer(string resourceId)
				: base(resourceId)
			{
			}
			public void SetShowProperties(IPropertyEntry[] contents)
			{
				if (simplePropertyGroup == null)
				{
					this.contents = contents;
				}
				else
				{
					simplePropertyGroup.RemoveAll();
					simplePropertyGroup.Add(contents);
					if (constructAction.propertyTreeView != null)
					{
						constructAction.propertyTreeView.Refresh(simplePropertyGroup);
					}
				}
			}
			public void Open(bool open)
			{
				if (constructAction.propertyTreeView != null && simplePropertyGroup != null)
				{
					constructAction.propertyTreeView.OpenSubEntries(simplePropertyGroup, open);
				}
			}
			public IPropertyEntry[] GetShowProperties()
			{
				if (simplePropertyGroup == null)
				{
					return contents;
				}
				else
				{
					return simplePropertyGroup.SubItems;
				}
			}
			#region IInputObject Members

			void IInputObject.Init(ConstructAction a)
			{
				base.Init(a);
			}

			void IInputObject.Refresh()
			{
				if (constructAction.propertyTreeView != null && simplePropertyGroup != null)
				{
					constructAction.propertyTreeView.Refresh(simplePropertyGroup);
				}
			}

			IPropertyEntry IInputObject.BuildShowProperty()
			{
				simplePropertyGroup = new SimplePropertyGroup(ResourceId);
				if (contents != null) simplePropertyGroup.Add(contents);
				return simplePropertyGroup;
			}

			void IInputObject.OnMouse(MouseEventArgs e, MouseState mouseState, IView vw)
			{
			}

			void IInputObject.Activate(ActivationMode activationMode)
			{
			}

			bool IInputObject.AcceptInput(bool acceptOptional)
			{
				return false;
			}

			void IInputObject.MouseLeft()
			{
			}

			void IInputObject.OnActionDone()
			{
			}

			IPropertyEntry IInputObject.GetShowProperty()
			{
				return simplePropertyGroup;
			}

			bool IInputObject.OnEnter(IPropertyEntry sender, KeyEventArgs args)
			{
				return false;
			}
			bool IInputObject.HasHotspot
			{
				get { return false; }
			}

			GeoPoint IInputObject.HotspotPosition
			{
				get { return GeoPoint.Origin; }
			}

			Image IInputObject.HotSpotIcon
			{
				get { return null; }
			}

			bool IInputObject.IsFixed()
			{
				return false;
			}

			void IInputObject.SetFixed(bool isFixed)
			{
			}
			public override void SetError(string errorMessage)
			{

			}

			#endregion
		}
		public class AngleInput : InputObject, IInputObject
		{
			/// <summary>
			/// Constructs an AngleInput.
			/// </summary>
			/// <param name="resourceId">resource id for the label</param>
			public AngleInput(string resourceId)
				: base(resourceId)
			{
				calculationMode = CalculationModes.FromBasePoint;
			}
			/// <summary>
			/// Constructs an AngleInput.
			/// </summary>
			/// <param name="resourceId">resource id for the label</param>
			/// <param name="StartValue">start value for the angle</param>
			public AngleInput(string resourceId, Angle StartValue)
				: this(resourceId)
			{
				Angle = StartValue;
			}
			private AngleProperty angleProperty; // die Anzeige desselben

			private Angle angle;
			public Angle Angle
			{
				get
				{
					return angle;
				}
				private set
				{
					if (angle == value)
						return;

					angle = value;
					InputValueChangedEvent?.Invoke(value);
				}
			}
			private enum CalculationModes { FromBasePoint, FromPlane, FromLine, FromCallback }
			private CalculationModes calculationMode; // wie wird der Abstand berechnet
			private Plane angleFromPlane; // Winkel bezüglich X-Achse dieser Ebene
			private GeoPoint angleFromLineStartPoint; // Winkel bezüglich dieser Linie
			private GeoPoint angleFromLineEndPoint;
			/// <summary>
			/// Sets the given plane as a basis for the calculation of the angle.
			/// The calculated angle is the angle of the mouseposition relative to the X-axis
			/// of this plane.
			/// </summary>
			/// <param name="p">the plane</param>
			public void SetAngleFromPlane(Plane p)
			{
				calculationMode = CalculationModes.FromPlane;
				angleFromPlane = p;
			}
			/// <summary>
			/// Sets the line as a basis for the calculation of the angle.
			/// The calculated angle is the angle of line from the startPoint to the mouseposition 
			/// with the line from the startPoint to the endPoint.
			/// </summary>
			/// <param name="startPoint">startpoint of the line</param>
			/// <param name="endPoint">endpoint of the line</param>
			public void SetAngleFromLine(GeoPoint startPoint, GeoPoint endPoint)
			{
				calculationMode = CalculationModes.FromLine;
				angleFromLineStartPoint = startPoint;
				angleFromLineEndPoint = endPoint;
			}
			private Angle CalcAngle(GeoPoint p)
			{
				if (CalculateAngleEvent != null) return CalculateAngleEvent(p);
				switch (calculationMode)
				{
					case CalculationModes.FromBasePoint:
						{
							if (!constructAction.basePointIsValid) throw (new ActionException("ExpectLength: BasePoint not defined"));
							GeoPoint2D bp2d = constructAction.ActiveDrawingPlane.Project(constructAction.BasePoint);
							GeoPoint2D p2d = constructAction.ActiveDrawingPlane.Project(p);
							GeoVector2D v2d = p2d - bp2d;
							return v2d.Angle;
						}
					case CalculationModes.FromPlane:
						{
							GeoPoint2D pr = angleFromPlane.Project(p);
							GeoVector2D dir = pr - GeoPoint2D.Origin;
							return dir.Angle;
						}
					case CalculationModes.FromLine:
						throw new NotImplementedException(); // TODO: Geometry.Angle3P(GeoPoint start, GeoPoint common, GeoPoint end)
					default:
						throw new NotImplementedException();
				}
			}
			private DefaultAngle defaultAngle;
			/// <summary>
			/// Sets a DefaultAngle, which should be a static value, that carries the last
			/// input value of this length to the next instantiation of the action. 
			/// </summary>
			public DefaultAngle DefaultAngle
			{
				set { defaultAngle = value; }
			}
			/// <summary>
			/// Forces the input object to the specified value. The input filed is updated accordingly.
			/// </summary>
			/// <param name="val">the value to set</param>
			public void ForceValue(Angle val)
			{
				InternalSetAngle(val);
				constructAction.RefreshDependantProperties();
				angleProperty.AngleChanged(); // damit es in der Editbox dargestellt wird
			}
			private void InternalSetAngle(Angle a)
			{
				if (ReadOnly) return;
				if (SetAngleEvent == null) return;
				if (SetAngleEvent(a))
				{
					Angle = a;
				}
			}
			private void OnModifyWithMouse(IPropertyEntry sender, bool StartModifying)
			{
				(this as IInputObject).SetFixed(false);
				constructAction.SetCurrentInputIndex(this, true);
			}
			/// <summary>
			/// Delegate definition for the <see cref="SetAngleEvent"/>.
			/// </summary>
			/// <param name="angle">the new value for the angle</param>
			/// <returns>true: accepted, false: discarded</returns>
			public delegate bool SetAngleDelegate(Angle angle);
			/// <summary>
			/// Provide a method here to get the result of this input (and modify your object)
			/// </summary>
			public event SetAngleDelegate SetAngleEvent;
			/// <summary>
			/// delegate definition for <see cref="GetAngleEvent"/>
			/// </summary>
			/// <returns>the current angle</returns>
			public delegate Angle GetAngleDelegate();
			/// <summary>
			/// Provide a method here, if the angle not only depends from this input, but is also
			/// modified by other means.
			/// </summary>
			public event GetAngleDelegate GetAngleEvent;
			/// <summary>
			/// delegate definition for a custom method for the calculation of an angle.
			/// </summary>
			/// <param name="MousePosition">current mouse position in model coordinates</param>
			/// <returns>the calculated angle</returns>
			public delegate double CalculateAngleDelegate(GeoPoint MousePosition);
			/// <summary>
			/// Provide a method here, if you want to calculate the angle yourself, i.e. if distance
			/// from point, line or plane is not appropriate to your needs.
			/// </summary>
			public event CalculateAngleDelegate CalculateAngleEvent;
			/// <summary>
			/// Event that is fired when a mousclick happens and this AngleInput has the focus.
			/// </summary>
			public event MouseClickDelegate MouseClickEvent;
			/// <summary>
			/// Delegate definiation for <see cref="InputValueChangedEvent"/>
			/// </summary>
			/// <param name="length">New length value</param>
			public delegate void InputValueChangedDelegate(Angle angle);
			/// <summary>
			/// Event will be rasied if the Angle property changed
			/// </summary>
			public event InputValueChangedDelegate InputValueChangedEvent;
			/// <summary>
			/// true: this input field does not accept user input, 
			/// false: normal input field that requires user input
			/// </summary>
			public override bool ReadOnly
			{
				get
				{
					return base.ReadOnly;
				}
				set
				{
					base.ReadOnly = value;
					if (angleProperty != null) angleProperty.ReadOnly = value;
				}
			}
			private IInputObject[] forwardMouseInputTo;
			/// <summary>
			/// Mouse input should be forwarded to another input object and only processed
			/// by this input, when the other input object is fixed.
			/// </summary>
			public object ForwardMouseInputTo
			{
				set
				{
					if (value is IInputObject)
					{
						forwardMouseInputTo = new IInputObject[] { value as IInputObject };
					}
					else if (value is object[])
					{
						object[] v = value as object[];
						forwardMouseInputTo = new IInputObject[v.Length];
						for (int i = 0; i < v.Length; ++i)
						{
							forwardMouseInputTo[i] = v[i] as IInputObject;
						}
					}
				}
				get
				{
					return forwardMouseInputTo;
				}
			}

			#region IInputObject
			void IInputObject.Init(ConstructAction a)
			{
				base.Init(a);
				if (defaultAngle != null)
				{
					defaultAngle.SetAction(a);
					InternalSetAngle(defaultAngle);
				}
				if (GetAngleEvent != null) Angle = GetAngleEvent();
				if (angleProperty != null) angleProperty.AngleChanged();
			}
			void IInputObject.Refresh()
			{
				if (GetAngleEvent != null)
				{
					Angle = GetAngleEvent();
					if (angleProperty != null) angleProperty.AngleChanged();
				}
			}
			IPropertyEntry IInputObject.BuildShowProperty()
			{
				angleProperty = new AngleProperty(constructAction.Frame, ResourceId);
				angleProperty.OnGetValue = () => Angle;
				angleProperty.OnSetValue = (a) =>
				{
					((IInputObject)this).SetFixed(true);
					InternalSetAngle(a);
					if (defaultAngle != null)
						defaultAngle.Angle = a;
				};
				angleProperty.ModifyWithMouse += new ModifyWithMouseDelegate(OnModifyWithMouse);
				angleProperty.PropertyEntryChangedStateEvent += new PropertyEntryChangedStateDelegate(constructAction.ShowPropertyStateChanged);
				angleProperty.LockedChanged += OnLockedChanged;

				if (defaultAngle != null && !Optional)
				{
					angleProperty.Lockable = true;
					angleProperty.Locked = defaultAngle.Locked;
					if (defaultAngle.Locked) Fixed = true;
				}
				if (ReadOnly)
				{
					angleProperty.ReadOnly = true;
				}
				else if (!Optional && !Fixed)
				{
					angleProperty.Highlight = true;
				}
				angleProperty.AngleChanged(); // damit der Startwert dargestellt wird
				return angleProperty;
			}
			void IInputObject.OnMouse(MouseEventArgs e, MouseState mouseState, IView vw)
			{
				if (forwardMouseInputTo != null)
				{   // ggf. weiterleiten an einen anderen Input
					for (int i = 0; i < forwardMouseInputTo.Length; ++i)
					{
						if (!forwardMouseInputTo[i].IsFixed())
						{
							forwardMouseInputTo[i].OnMouse(e, mouseState, vw);
							return;
						}
					}
				}
				SnapPointFinder.DidSnapModes DidSnap;
				GeoPoint p = constructAction.SnapPoint(e, vw, out DidSnap);
				if (MouseClickEvent != null)
				{
					if (mouseState == MouseState.ClickDown) MouseClickEvent(false, p, vw);
					if (mouseState == MouseState.ClickUp) MouseClickEvent(true, p, vw);
				}
				if (Fixed) return; // kein Mausinput wird hier akzeptiert
				try
				{
					Angle a = CalcAngle(p);
					InternalSetAngle(a);
					constructAction.RefreshDependantProperties();
					angleProperty.AngleChanged(); // damit es in der Editbox dargestellt wird
					if (mouseState == MouseState.ClickUp)
					{
						(this as IInputObject).SetFixed(true);
						if (defaultAngle != null) defaultAngle.Angle = a;
						constructAction.SetNextInputIndex(true);
					}
				}
				catch (ActionException)
				{   // das Berechnen des Winkels ging schief, meistens weil kein Basepoint gesetzt ist
					// und die deshalb nichts berechnet werden kann.
				}
			}
			void IInputObject.MouseLeft()
			{
			}
			bool IInputObject.AcceptInput(bool acceptOptional)
			{
				return base.AcceptInput(acceptOptional);
			}
			void IInputObject.Activate(ActivationMode activationMode)
			{
				switch (activationMode)
				{
					case ActivationMode.Inactivate: break;
					case ActivationMode.BySelection: break;
					case ActivationMode.ByHotspot: (this as IInputObject).SetFixed(false); break;
				}
			}
			IPropertyEntry IInputObject.GetShowProperty()
			{
				return angleProperty;
			}
			bool IInputObject.OnEnter(IPropertyEntry sender, KeyEventArgs args)
			{
				if (sender == angleProperty && !args.Control)
				{
					(this as IInputObject).SetFixed(true); // als Reaktion auf Enter
					constructAction.SetNextInputIndex(true);
					return true;
				}
				return false;
			}
			bool IInputObject.HasHotspot
			{
				get
				{
					return false;
				}
			}
			GeoPoint IInputObject.HotspotPosition
			{
				get
				{
					return GeoPoint.Origin;
				}
			}
			Image IInputObject.HotSpotIcon { get { return null; } }

			void IInputObject.OnActionDone()
			{
			}
			bool IInputObject.IsFixed()
			{
				return Fixed;
			}
			protected override void AdjustHighlight()
			{
				if (angleProperty != null) angleProperty.Highlight = !Optional && !Fixed && !ReadOnly;
			}
			void IInputObject.SetFixed(bool isFixed)
			{
				Fixed = isFixed;
			}
			public override void SetError(string errorMessage)
			{
				angleProperty.ErrorMessage = errorMessage;
			}
			#endregion
			private Angle AnglePropertyOnGetAngle()
			{
				return Angle;
			}
			private void AnglePropertyOnSetAngle(Angle a)
			{
				(this as IInputObject).SetFixed(true);
				//angleProperty.SetMouseButton(MouseButtonMode.MouseLocked);
				InternalSetAngle(a);
				if (defaultAngle != null) defaultAngle.Angle = a;
			}
			private void OnLockedChanged(bool locked)
			{
				if (defaultAngle != null) defaultAngle.Locked = locked;
				if (locked)
				{
					(this as IInputObject).SetFixed(true);
					if (constructAction.GetCurrentInputObject() == this)
					{
						constructAction.SetNextInputIndex(true);
					}
				}
				else
				{
					(this as IInputObject).SetFixed(false);
				}
			}
		}
		/// <summary>
		/// Defines an input object for an action derived from ConstructAction.
		/// This input object extpects the input of a length or distance. The length may be either entered
		/// on the keyboard or by moving the mouse. The calculation of a length from the mouse position
		/// depends on various settings.
		/// Pressing enter or TAB or clicking the mouse proceeds to the next input object. 
		/// </summary>
		public class LengthInput : InputObject, IInputObject
		{
			private enum CalculationModes { FromBasePoint, FromPoint, FromPlane, FromLine, FromCallback }
			private CalculationModes calculationMode; // wie wird der Abstand berechnet
			private Plane distFromPlane; // Abstand von dieser Ebene
			private GeoPoint distFromLineStartPoint;
			private GeoPoint distFromLineEndPoint;
			private GeoPoint distFromBasePoint;
			/// <summary>
			/// Sets the given plane as a basis for the calculation of the distance or length.
			/// The calculated length is the distance of the mouseposition from this plane.
			/// </summary>
			/// <param name="p">the plane</param>
			public void SetDistanceFromPlane(Plane p)
			{
				calculationMode = CalculationModes.FromPlane;
				distFromPlane = p;
			}
			/// <summary>
			/// Sets the line as a basis for the calculation of the distance or length.
			/// The calculated length is the distance of the mouseposition from this line
			/// </summary>
			/// <param name="startPoint">startpoint of the line</param>
			/// <param name="endPoint">endpoint of the line</param>
			public void SetDistanceFromLine(GeoPoint startPoint, GeoPoint endPoint)
			{
				calculationMode = CalculationModes.FromLine;
				distFromLineStartPoint = startPoint;
				distFromLineEndPoint = endPoint;
			}
			/// <summary>
			/// Sets the point as a basis for the calculation of the distance or length.
			/// The calculated length is the distance of the mouseposition from this point.
			/// </summary>
			/// <param name="basePoint">the point</param>
			public void SetDistanceFromPoint(GeoPoint basePoint)
			{
				calculationMode = CalculationModes.FromPoint;
				distFromBasePoint = basePoint;
			}
			internal DefaultLength defaultLength; // TODO: private machen!
			/// <summary>
			/// Sets a DefaultLength, which should be a static value, that carries the last
			/// input value of this length to the next instantiation of the action. 
			/// </summary>
			public DefaultLength DefaultLength
			{
				set { defaultLength = value; }
			}
			private LengthProperty lengthProperty; // die Anzeige desselben
			/// <summary>
			/// Constructs a LengthInput object.
			/// </summary>
			/// <param name="resourceId">the resource id to specify a string from the StringTable.
			/// ResourceId+".Label": the Label left of the
			/// edit box. ResourceId+".ShortInfo": a short tooltip text ResourceId+"DetailedInfo":
			/// a longer tooltip text.
			/// </param>
			public LengthInput(string resourceId)
				: base(resourceId)
			{
				calculationMode = CalculationModes.FromBasePoint;
				ReadOnly = false;
				Optional = false;
				Fixed = false;
			}
			/// <summary>
			/// Constructs a LengthInput object with a start value
			/// </summary>
			/// <param name="resourceId">the resource id to specify a string from the StringTable.
			/// ResourceId+".Label": the Label left of the
			/// edit box. ResourceId+".ShortInfo": a short tooltip text ResourceId+"DetailedInfo":
			/// a longer tooltip text.
			/// </param>
			/// <param name="StartValue">the initial value</param>
			public LengthInput(string resourceId, double StartValue)
				: this(resourceId)
			{
				Length = StartValue;
			}
			private double CalcLength(GeoPoint p)
			{
				if (CalculateLengthEvent != null) return CalculateLengthEvent(p);
				switch (calculationMode)
				{
					case CalculationModes.FromBasePoint:
						if (!constructAction.basePointIsValid) throw (new ActionException("ExpectLength: BasePoint not defined"));
						return Geometry.Dist(constructAction.basePoint, p);
					case CalculationModes.FromPoint:
						return Geometry.Dist(p, distFromBasePoint);
					case CalculationModes.FromPlane:
						return distFromPlane.Distance(p);
					case CalculationModes.FromLine:
						return Geometry.DistPL(p, distFromLineStartPoint, distFromLineEndPoint);
					default:
						throw new NotImplementedException();
				}
			}
			/// <summary>
			/// Delegate definition for <see cref="SetLengthEvent"/>
			/// </summary>
			/// <param name="Length">the length that has been entered by the user</param>
			/// <returns></returns>
			public delegate bool SetLengthDelegate(double Length);
			/// <summary>
			/// Provide a method here to get the result of this input (and modify your object)
			/// </summary>
			public event SetLengthDelegate SetLengthEvent;
			/// <summary>
			/// Delegate definition for <see cref="GetLengthEvent"/>.
			/// </summary>
			/// <returns>the current length</returns>
			public delegate double GetLengthDelegate();
			/// <summary>
			/// Provide a method here, if the length not only depends from this input, but is also
			/// modified by other means.
			/// </summary>
			public event GetLengthDelegate GetLengthEvent;
			/// <summary>
			/// Delegate definition for <see cref="CalculateLengthEvent"/>
			/// </summary>
			/// <param name="MousePosition">mouse position in model coordinates</param>
			/// <returns>the calculated length</returns>
			public delegate double CalculateLengthDelegate(GeoPoint MousePosition);
			/// <summary>
			/// Provide a method here, if you want to calculate the length yourself, i.e. if distance
			/// from point, line or plane is not appropriate to your needs.
			/// </summary>
			public event CalculateLengthDelegate CalculateLengthEvent;
			/// <summary>
			/// Delegate definiation for <see cref="InputValueChangedEvent"/>
			/// </summary>
			/// <param name="length">New length value</param>
			public delegate void InputValueChangedDelegate(double length);
			/// <summary>
			/// Event will be rasied if the Length property changed
			/// </summary>
			public event InputValueChangedDelegate InputValueChangedEvent;

			private double length;
			public double Length
			{
				get
				{
					return length;
				}
				private set
				{
					if (length == value)
						return;

					length = value;
					InputValueChangedEvent?.Invoke(value);
				}
			}
			/// <summary>
			/// Forces the input object to the specified value. The input field is updated accordingly.
			/// </summary>
			/// <param name="val">the value to set</param>
			public void ForceValue(double val)
			{
				InternalSetLength(val);
				constructAction.RefreshDependantProperties();
				lengthProperty.LengthChanged(); // damit es in der Editbox dargestellt wird
			}
			private void InternalSetLength(double Length)
			{
				if (ReadOnly) return;
				if (SetLengthEvent == null)
				{
					this.Length = Length; // Wert auch übernehemen wenn kein SetLengthEvent gesetzt ist
					return;
				}
				if (SetLengthEvent(Length))
				{
					this.Length = Length; // Wert auch übernehemen wenn kein SetLengthEvent gesetzt ist
				}
			}
			private void OnModifyWithMouse(IPropertyEntry sender, bool StartModifying)
			{
				(this as IInputObject).SetFixed(false);
				constructAction.SetCurrentInputIndex(this, true);
			}
			/// <summary>
			/// Event that is fired when a mousclick happens and this input has the focus.
			/// </summary>
			public event MouseClickDelegate MouseClickEvent;
			/// <summary>
			/// true: this input field does not accept user input, 
			/// false: normal input field that requires user input
			/// </summary>
			public override bool ReadOnly
			{
				get
				{
					return base.ReadOnly;
				}
				set
				{
					base.ReadOnly = value;
					if (lengthProperty != null) lengthProperty.ReadOnly = value;
				}
			}
			private IInputObject[] forwardMouseInputTo;
			/// <summary>
			/// Mouse input should be forwarded to another input object and only processed
			/// by this input, when the other input object is fixed.
			/// </summary>
			public object ForwardMouseInputTo
			{
				set
				{
					if (value is IInputObject)
					{
						forwardMouseInputTo = new IInputObject[] { value as IInputObject };
					}
					else if (value is object[])
					{
						object[] v = value as object[];
						forwardMouseInputTo = new IInputObject[v.Length];
						for (int i = 0; i < v.Length; ++i)
						{
							forwardMouseInputTo[i] = v[i] as IInputObject;
						}
					}
				}
				get
				{
					return forwardMouseInputTo;
				}
			}
			#region IInputObject
			void IInputObject.Init(ConstructAction a)
			{
				base.Init(a);
				if (defaultLength != null)
				{
					defaultLength.SetAction(a);
					InternalSetLength(defaultLength);
				}
				if (GetLengthEvent != null) Length = GetLengthEvent();
				if (lengthProperty != null) lengthProperty.LengthChanged();
			}
			void IInputObject.Refresh()
			{
				if (GetLengthEvent != null)
				{
					Length = GetLengthEvent();
					if (lengthProperty != null) lengthProperty.LengthChanged();
				}
			}
			IPropertyEntry IInputObject.BuildShowProperty()
			{
				lengthProperty = new LengthProperty(constructAction.Frame, ResourceId);
				lengthProperty.OnGetValue = () => Length;
				lengthProperty.OnSetValue = (l) =>
				{
					(this as IInputObject).SetFixed(true);
					InternalSetLength(l);
					constructAction.RefreshDependantProperties();
					if (defaultLength != null)
						defaultLength.Length = l;
				};
				lengthProperty.ModifyWithMouse += new ModifyWithMouseDelegate(OnModifyWithMouse);
				lengthProperty.PropertyEntryChangedStateEvent += new PropertyEntryChangedStateDelegate(constructAction.ShowPropertyStateChanged);
				lengthProperty.LockedChanged += new LengthProperty.LockedChangedDelegate(OnLockedChanged);
				if (defaultLength != null && !Optional)
				{
					lengthProperty.Lockable = true;
					lengthProperty.Locked = defaultLength.Locked;
					if (defaultLength.Locked) Fixed = true;
				}

				if (ReadOnly)
				{
					lengthProperty.ReadOnly = true;
				}
				else if (!Optional && !Fixed)
				{
					lengthProperty.Highlight = true;
				}
				lengthProperty.LengthChanged(); // damit der Startwert dargestellt wird
				return lengthProperty;
			}
			void IInputObject.OnMouse(MouseEventArgs e, MouseState mouseState, IView vw)
			{
				if (forwardMouseInputTo != null)
				{   // ggf. weiterleiten an einen anderen Input
					for (int i = 0; i < forwardMouseInputTo.Length; ++i)
					{
						if (!forwardMouseInputTo[i].IsFixed())
						{
							forwardMouseInputTo[i].OnMouse(e, mouseState, vw);
							return;
						}
					}
				}
				SnapPointFinder.DidSnapModes DidSnap;
				GeoPoint p = constructAction.SnapPoint(e, vw, out DidSnap);
				if (MouseClickEvent != null)
				{
					if (mouseState == MouseState.ClickDown) MouseClickEvent(false, p, vw);
					if (mouseState == MouseState.ClickUp) MouseClickEvent(true, p, vw);
				}
				if (Fixed) return; // kein Mausinput wird hier akzeptiert
				try
				{
					double l = CalcLength(p);
					InternalSetLength(l);
					constructAction.RefreshDependantProperties();
					lengthProperty.LengthChanged(); // damit es in der Editbox dargestellt wird
					if ((mouseState == MouseState.ClickUp))
					{
						(this as IInputObject).SetFixed(true);
						if (defaultLength != null) defaultLength.Length = l;
						constructAction.SetNextInputIndex(true);
					}
				}
				catch (ActionException)
				{   // das Berechnen der Länge ging schief, meisten weil kein Basepoint gesetzt ist
					// und die deshalb nichts berechnet werden kann.
				}
			}
			void IInputObject.Activate(ActivationMode activationMode)
			{
				switch (activationMode)
				{
					case ActivationMode.Inactivate:
						if (defaultLength != null) defaultLength.SetAction(null);
						break;
					case ActivationMode.BySelection: break;
					case ActivationMode.ByHotspot: (this as IInputObject).SetFixed(false); break;
				}
			}
			bool IInputObject.AcceptInput(bool acceptOptional)
			{
				if (acceptOptional) return (!Fixed);
				else return (!Fixed && !Optional);
			}
			void IInputObject.MouseLeft()
			{
				// wenn die Maus das Spielfeld verlässt, dann wird wieder der default-Wert gesetzt
				if (defaultLength != null)
				{
					InternalSetLength(defaultLength);
					constructAction.RefreshDependantProperties();
					lengthProperty.LengthChanged(); // damit es in der Editbox dargestellt wird
				}
			}

			bool IInputObject.HasHotspot
			{
				get
				{
					return false;
				}
			}
			GeoPoint IInputObject.HotspotPosition
			{
				get
				{
					return GeoPoint.Origin;
				}
			}
			Image IInputObject.HotSpotIcon { get { return null; } }
			void IInputObject.OnActionDone()
			{   // versuchsweise defaultLength setzen, wenn die Aktion zu ende geht
				// das müsste noch in den anderen Fällen berücksichtigt werden
				if (defaultLength != null) defaultLength.Length = Length;
			}
			IPropertyEntry IInputObject.GetShowProperty()
			{
				return lengthProperty;
			}
			bool IInputObject.OnEnter(IPropertyEntry sender, KeyEventArgs args)
			{
				if (sender == lengthProperty && !args.Control)
				{
					(this as IInputObject).SetFixed(true); // als Reaktion auf Enter
					constructAction.SetNextInputIndex(true);
					return true;
				}
				return false;
			}
			bool IInputObject.IsFixed()
			{
				return Fixed;
			}
			protected override void AdjustHighlight()
			{
				if (lengthProperty != null) lengthProperty.Highlight = !Optional && !Fixed && !ReadOnly;
			}
			void IInputObject.SetFixed(bool isFixed)
			{
				Fixed = isFixed;
			}
			public override void SetError(string errorMessage)
			{
				lengthProperty.ErrorMessage = errorMessage;
			}
			#endregion
			
			private void OnLockedChanged(bool locked)
			{
				if (defaultLength != null) defaultLength.Locked = locked;
				if (locked)
				{
					(this as IInputObject).SetFixed(true);
					if (constructAction.GetCurrentInputObject() == this)
					{
						constructAction.SetNextInputIndex(true);
					}
				}
				else
				{
					(this as IInputObject).SetFixed(false);
				}
			}

		}
		/// <summary>
		/// Defines an input object for an action derived from ConstructAction.
		/// This input object extpects the input of a double value. The value may be either entered
		/// on the keyboard or by moving the mouse. The calculation of a value from the mouse position
		/// is performed via a callback (event) method.
		/// Pressing enter or TAB or clicking the mouse proceeds to the next input object. 
		/// </summary>
		public class DoubleInput : InputObject, IInputObject
		{
			private DoubleProperty doubleProperty; // die Anzeige desselben
			/// <summary>
			/// Constructs a DoubleInput object.
			/// </summary>
			/// <param name="resourceId">the resource id to specify a string from the StringTable.
			/// ResourceId+".Label": the Label left of the
			/// edit box. ResourceId+".ShortInfo": a short tooltip text ResourceId+"DetailedInfo":
			/// a longer tooltip text.
			/// </param>
			public DoubleInput(string resourceId)
				: base(resourceId)
			{
			}
			/// <summary>
			/// Constructs a DoubleInput object with a initial value.
			/// </summary>
			/// <param name="resourceId">the resource id to specify a string from the StringTable.
			/// ResourceId+".Label": the Label left of the
			/// edit box. ResourceId+".ShortInfo": a short tooltip text ResourceId+"DetailedInfo":
			/// a longer tooltip text.
			/// </param>
			/// <param name="startValue">the initial value</param>
			public DoubleInput(string resourceId, double startValue)
				: this(resourceId)
			{
				DoubleValue = startValue;
			}
			private double CalcDouble(GeoPoint p)
			{
				if (CalculateDoubleEvent != null) return CalculateDoubleEvent(p);
				throw new NotImplementedException();
			}
			/// <summary>
			/// Delegate definition for <see cref="SetDoubleEvent"/>
			/// </summary>
			/// <param name="val">the value that has been provided by the user</param>
			/// <returns>true if accepted, fale otherwise</returns>
			public delegate bool SetDoubleDelegate(double val);
			/// <summary>
			/// Provide a method here to get the result of this input (and modify your object)
			/// </summary>
			public event SetDoubleDelegate SetDoubleEvent;
			/// <summary>
			/// Delegate definition for <see cref="GetDoubleEvent"/>
			/// </summary>
			/// <returns>the double value</returns>
			public delegate double GetDoubleDelegate();
			/// <summary>
			/// Provide a method here, if the length not only depends from this input, but is also
			/// modified by other means.
			/// </summary>
			public event GetDoubleDelegate GetDoubleEvent;
			/// <summary>
			/// Delegate definition for <see cref="CalculateDoubleEvent"/>
			/// </summary>
			/// <param name="MousePosition">current mous position in model coordinates</param>
			/// <returns>the calculates double value</returns>
			public delegate double CalculateDoubleDelegate(GeoPoint MousePosition);
			/// <summary>
			/// Provide a method here, if you want to calculate the length yourself, i.e. if distance
			/// from point, line or plane is not appropriate to your needs.
			/// </summary>
			public event CalculateDoubleDelegate CalculateDoubleEvent;
			/// <summary>
			/// Delegate definiation for <see cref="InputValueChangedEvent"/>
			/// </summary>
			/// <param name="length">New length value</param>
			public delegate void InputValueChangedDelegate(double newValue);
			/// <summary>
			/// Event will be rasied if the Length property changed
			/// </summary>
			public event InputValueChangedDelegate InputValueChangedEvent;
			private double val;
			public double DoubleValue
			{
				get => val;
				private set
				{
					val = value;
					InputValueChangedEvent?.Invoke(value);
				}
			}
			/// <summary>
			/// Forces the input object to the specified value. The input filed is updated accordingly.
			/// </summary>
			/// <param name="val">the value to set</param>
			public void ForceValue(double val)
			{
				InternalSetDouble(val);
				constructAction.RefreshDependantProperties();
				doubleProperty.DoubleChanged(); // damit es in der Editbox dargestellt wird
			}
			private void InternalSetDouble(double val)
			{
				if (ReadOnly) return;
				if (SetDoubleEvent == null) return;
				if (SetDoubleEvent(val))
				{
					DoubleValue = val;
				}
			}
			private void OnModifyWithMouse(IShowProperty sender, bool StartModifying)
			{
				(this as IInputObject).SetFixed(false);
				constructAction.SetCurrentInputIndex(this, true);
			}
			/// <summary>
			/// Event that is fired when a mousclick happens and this input has the focus.
			/// </summary>
			public event MouseClickDelegate MouseClickEvent;
			/// <summary>
			/// true: this input field does not accept user input, 
			/// false: normal input field that requires user input
			/// </summary>
			public override bool ReadOnly
			{
				get
				{
					return base.ReadOnly;
				}
				set
				{
					base.ReadOnly = value;
					if (doubleProperty != null) doubleProperty.ReadOnly = value;
				}
			}
			private IInputObject[] forwardMouseInputTo;
			/// <summary>
			/// Mouse input should be forwarded to another input object and only processed
			/// by this input, when the other input object is fixed.
			/// </summary>
			public object ForwardMouseInputTo
			{
				set
				{
					if (value is IInputObject)
					{
						forwardMouseInputTo = new IInputObject[] { value as IInputObject };
					}
					else if (value is object[])
					{
						object[] v = value as object[];
						forwardMouseInputTo = new IInputObject[v.Length];
						for (int i = 0; i < v.Length; ++i)
						{
							forwardMouseInputTo[i] = v[i] as IInputObject;
						}
					}
				}
				get
				{
					return forwardMouseInputTo;
				}
			}
			#region IInputObject
			void IInputObject.Init(ConstructAction a)
			{
				base.Init(a);
				if (GetDoubleEvent != null) DoubleValue = GetDoubleEvent();
				if (doubleProperty != null) doubleProperty.DoubleChanged();
			}
			void IInputObject.Refresh()
			{
				if (GetDoubleEvent != null)
				{
					DoubleValue = GetDoubleEvent();
					if (doubleProperty != null) doubleProperty.DoubleChanged();
				}
			}
			IPropertyEntry IInputObject.BuildShowProperty()
			{
				doubleProperty = new DoubleProperty(constructAction.Frame, ResourceId);
				doubleProperty.OnGetValue = () => DoubleValue;
				doubleProperty.OnSetValue = (d) =>
				{
					(this as IInputObject).SetFixed(true);
					constructAction.RefreshDependantProperties();
					InternalSetDouble(d);
				};

				doubleProperty.PropertyEntryChangedStateEvent += new PropertyEntryChangedStateDelegate(constructAction.ShowPropertyStateChanged);
				if (ReadOnly)
				{
					// doubleProperty.ReadOnly = true;
				}
				if (Optional)
				{
					// doubleProperty.ShowMouseButton = false;
				}
				else if (!Optional && !Fixed)
				{
					doubleProperty.Highlight = true;
				}
				doubleProperty.DoubleChanged(); // damit der Startwert dargestellt wird
				return doubleProperty;
			}
			void IInputObject.OnMouse(MouseEventArgs e, MouseState mouseState, IView vw)
			{
				if (forwardMouseInputTo != null)
				{   // ggf. weiterleiten an einen anderen Input
					for (int i = 0; i < forwardMouseInputTo.Length; ++i)
					{
						if (!forwardMouseInputTo[i].IsFixed())
						{
							forwardMouseInputTo[i].OnMouse(e, mouseState, vw);
							return;
						}
					}
				}
				SnapPointFinder.DidSnapModes DidSnap;
				GeoPoint p = constructAction.SnapPoint(e, vw, out DidSnap);
				if (MouseClickEvent != null)
				{
					if (mouseState == MouseState.ClickDown) MouseClickEvent(false, p, vw);
					if (mouseState == MouseState.ClickUp) MouseClickEvent(true, p, vw);
				}
				if (Fixed) return; // kein Mausinput wird hier akzeptiert
				try
				{
					double d = CalcDouble(p);
					InternalSetDouble(d);
					constructAction.RefreshDependantProperties();
					doubleProperty.DoubleChanged(); // damit es in der Editbox dargestellt wird
					if (mouseState == MouseState.ClickUp)
					{
						(this as IInputObject).SetFixed(true);
						constructAction.SetNextInputIndex(true);
					}
				}
				catch (ActionException)
				{   // das Berechnen der Länge ging schief, meisten weil kein Basepoint gesetzt ist
					// und die deshalb nichts berechnet werden kann.
				}
			}
			void IInputObject.Activate(ActivationMode activationMode)
			{
				switch (activationMode)
				{
					case ActivationMode.Inactivate: break;
					case ActivationMode.BySelection: break;
					case ActivationMode.ByHotspot: (this as IInputObject).SetFixed(false); break;
				}
			}
			bool IInputObject.AcceptInput(bool acceptOptional)
			{
				return base.AcceptInput(acceptOptional);
			}
			void IInputObject.MouseLeft()
			{
				// wenn die Maus das Spielfeld verlässt, dann wird wieder der default-Wert gesetzt
			}

			bool IInputObject.HasHotspot
			{
				get
				{
					return false;
				}
			}
			GeoPoint IInputObject.HotspotPosition
			{
				get
				{
					return GeoPoint.Origin;
				}
			}
			Image IInputObject.HotSpotIcon { get { return null; } }

			void IInputObject.OnActionDone()
			{
			}
			IPropertyEntry IInputObject.GetShowProperty()
			{
				return doubleProperty;
			}
			bool IInputObject.OnEnter(IPropertyEntry sender, KeyEventArgs args)
			{
				if (sender == doubleProperty && !args.Control)
				{
					(this as IInputObject).SetFixed(true); // als Reaktion auf Enter
					constructAction.SetNextInputIndex(true);
					return true;
				}
				return false;
			}
			bool IInputObject.IsFixed()
			{
				return Fixed;
			}
			protected override void AdjustHighlight()
			{
				if (doubleProperty != null) doubleProperty.Highlight = !Optional && !Fixed && !ReadOnly;
			}
			void IInputObject.SetFixed(bool isFixed)
			{
				Fixed = isFixed;
			}
			public override void SetError(string errorMessage)
			{
				doubleProperty.ErrorMessage = errorMessage;
			}
			#endregion
			private double DoublePropertyOnGetDouble(DoubleProperty sender)
			{
				return DoubleValue;
			}
			private void DoublePropertyOnSetDouble(DoubleProperty sender, double l)
			{
				(this as IInputObject).SetFixed(true);
				constructAction.RefreshDependantProperties();
				InternalSetDouble(l);
			}
		}
		/// <summary>
		/// Defines an input object for an action derived from ConstructAction.
		/// This input object extpects the input of a point. This point may be either entered
		/// on the keyboard or by moving the mouse. Pressing enter or TAB or clicking the mouse
		/// proceeds to the next input object. 
		/// </summary>
		public class GeoPointInput : InputObject, IInputObject
		{
			/// <summary>
			/// If set to true this point is the basepoint for snapping.
			/// </summary>
			public bool DefinesBasePoint;
			/// <summary>
			/// true: the point is displayed as hotspot (small sqaure). The user can drag this
			/// hotspot with the mouse to modify this input.
			/// </summary>
			public bool DefinesHotSpot;
			/// <summary>
			/// The icon for the hotspot, for example "Hotspots.png:3"
			/// </summary>
			public string HotSpotSource; // z.B. "Hotspots.png:3"
			/// <summary>
			/// Constructs a GeoPointInput object.
			/// </summary>
			/// <param name="resourceId">the resource id to specify a string from the StringTable.
			/// ResourceId+".Label": the Label left of the
			/// edit box. ResourceId+".ShortInfo": a short tooltip text ResourceId+"DetailedInfo":
			/// a longer tooltip text.
			/// </param>
			public GeoPointInput(string resourceId)
				: base(resourceId)
			{
				ResourceId = resourceId;
				Fixed = false;
				DefinesHotSpot = false;
			}
			/// <summary>
			/// Constructs a GeoPointInput object with an inital value.
			/// </summary>
			/// <param name="resourceId">the resource id to specify a string from the StringTable.
			/// ResourceId+".Label": the Label left of the
			/// edit box. ResourceId+".ShortInfo": a short tooltip text ResourceId+"DetailedInfo":
			/// a longer tooltip text.
			/// </param>
			/// <param name="StartValue">the initial value</param>
			public GeoPointInput(string resourceId, GeoPoint StartValue)
				: this(resourceId)
			{
				Point = StartValue;
			}
			private DefaultGeoPoint defaultGeoPoint;
			/// <summary>
			/// Sets a DefaultGeoPoint, which should be a static value, that carries the last
			/// input value of this point to the next instantiation of the action. 
			/// </summary>
			public DefaultGeoPoint DefaultGeoPoint
			{
				set
				{
					defaultGeoPoint = value;
				}
			}
			private GeoPointProperty geoPointProperty; // die Anzeige desselben
			/// <summary>
			/// Delegate definition for <see cref="SetGeoPointEvent"/>
			/// </summary>
			/// <param name="p">the point provided by the user (via mouse or keyboard)</param>
			public delegate void SetGeoPointDelegate(GeoPoint p);
			/// <summary>
			/// Delegate definition for <see cref="SetGeoPointExEvent"/>
			/// </summary>
			/// <param name="p">the point provided by the user</param>
			/// <param name="didSnap">the snap mode</param>
			/// <returns>true, if point is accepted</returns>
			public delegate bool SetGeoPointExDelegate(GeoPoint p, SnapPointFinder.DidSnapModes didSnap);
			/// <summary>
			/// Delegate definition for <see cref="GetGeoPointEvent"/>
			/// </summary>
			/// <returns>the current point</returns>
			public delegate GeoPoint GetGeoPointDelegate();
			/// <summary>
			/// Provide a method for this event to receive the value of the point as it is
			/// defined by either keyboard or mouse input.
			/// </summary>
			public event SetGeoPointDelegate SetGeoPointEvent;
			/// <summary>
			/// Provide a method for this event to receive the value of the point as it is
			/// defined by either keyboard or mouse input.
			/// </summary>
			public event SetGeoPointExDelegate SetGeoPointExEvent;
			/// <summary>
			/// This event is used by the display to show the current value of the point.
			/// This is necessary if this point is also modified by other means, not only by this input field.
			/// </summary>
			public event GetGeoPointDelegate GetGeoPointEvent;
			/// <summary>
			/// Delegate definition for <see cref="InputValueChangedEvent"/>
			/// </summary>
			/// <param name="point">New point value</param>
			public delegate void InputValueChangedDelegate(GeoPoint point);
			/// <summary>
			/// This event will be raised if the Point property was changed
			/// </summary>
			public event InputValueChangedDelegate InputValueChangedEvent;

			private GeoPoint point; // der aktuelle Wert des Punktes
			public GeoPoint Point
			{
				get
				{
					return point;
				}
				private set
				{
					if (point == value)
						return;

					point = value;
					InputValueChangedEvent?.Invoke(value);
				}
			}
			private int partiallyFixed; // Teile des Punktes (z.B. X_-Komponente) sind schon gefixed
			/// <summary>
			/// Forces the input object to the specified value. The input filed is updated accordingly.
			/// </summary>
			/// <param name="p">the point to set</param>
			public void ForceValue(GeoPoint p)
			{
				SetGeoPoint(p, SnapPointFinder.DidSnapModes.KeyboardInput);
				constructAction.RefreshDependantProperties();
				geoPointProperty.GeoPointChanged(); // damit es in der Editbox dargestellt wird
			}
			private void SetGeoPoint(GeoPoint p, SnapPointFinder.DidSnapModes didSnap)
			{
				if (SetGeoPointExEvent != null)
				{
					bool accepted = SetGeoPointExEvent(p, didSnap);
					if (!accepted) return;
				}
				if (DefinesHotSpot)
				{
					foreach (IView vw in constructAction.Frame.AllViews)
					{
						if (vw is IActionInputView)
						{
							PointF pf1 = vw.Projection.ProjectF(p);
							PointF pf2 = vw.Projection.ProjectF(Point);
							int xmin = (int)Math.Min(pf1.X, pf2.X) - 8;
							int xmax = (int)Math.Max(pf1.X, pf2.X) + 8;
							int ymin = (int)Math.Min(pf1.Y, pf2.Y) - 8;
							int ymax = (int)Math.Max(pf1.Y, pf2.Y) + 8;
							Rectangle rct = new Rectangle(xmin, ymin, xmax - xmin, ymax - ymin);
							vw.Invalidate(PaintBuffer.DrawingAspect.Select, rct);
						}
					}
				}
				if (ReadOnly) return;
				Point = p;
				if (SetGeoPointEvent != null)
				{
					SetGeoPointEvent(p);
				}
			}
			private bool GetGeoPoint()
			{
				if (GetGeoPointEvent != null)
				{
					Point = GetGeoPointEvent();
					return true;
				}
				return false;
			}
			private void OnModifyWithMouse(IPropertyEntry sender, bool StartModifying)
			{
				(this as IInputObject).SetFixed(false);
				constructAction.SetCurrentInputIndex(this, true);
			}
			/// <summary>
			/// Event that is fired when a mousclick happens and this input has the focus.
			/// </summary>
			public event MouseClickDelegate MouseClickEvent;
			/// <summary>
			/// true: this input field does not accept user input, 
			/// false: normal input field that requires user input
			/// </summary>
			public override bool ReadOnly
			{
				get
				{
					return base.ReadOnly;
				}
				set
				{
					base.ReadOnly = value;
					if (geoPointProperty != null) geoPointProperty.ReadOnly = value;
				}
			}
			private IInputObject[] forwardMouseInputTo;
			/// <summary>
			/// Mouse input should be forwarded to another input object and only processed
			/// by this input, when the other input object is fixed.
			/// </summary>
			public object ForwardMouseInputTo
			{
				set
				{
					if (value is IInputObject)
					{
						forwardMouseInputTo = new IInputObject[] { value as IInputObject };
					}
					else if (value is object[])
					{
						object[] v = value as object[];
						forwardMouseInputTo = new IInputObject[v.Length];
						for (int i = 0; i < v.Length; ++i)
						{
							forwardMouseInputTo[i] = v[i] as IInputObject;
						}
					}
				}
				get
				{
					return forwardMouseInputTo;
				}
			}
			#region IInputObject
			void IInputObject.Init(ConstructAction a)
			{
				constructAction = a;
				if (defaultGeoPoint != null)
				{
					defaultGeoPoint.SetAction(a);
					SetGeoPoint(defaultGeoPoint, SnapPointFinder.DidSnapModes.KeyboardInput);
				}
				GetGeoPoint();
			}
			void IInputObject.Refresh()
			{   // irgend etwas anderes hat sich verändert. Soll diese Anzeige verändert werden?
				if (GetGeoPoint())
				{
					if (geoPointProperty != null) geoPointProperty.GeoPointChanged();
				}
			}
			IPropertyEntry IInputObject.BuildShowProperty()
			{
				geoPointProperty = new GeoPointProperty(constructAction.Frame, ResourceId);
				geoPointProperty.OnGetValue = () => Point;
				geoPointProperty.OnSetValue = (p) =>
				{
					// this is beeing called by the controlcenter, either when the value is edited or subentries are beeing edited or a subaction changed the point
					partiallyFixed = (int)geoPointProperty.InputFromSubEntries;
					if (partiallyFixed == 7) partiallyFixed = 0; // all subcomponents have been fixed
					if (partiallyFixed == 0) // this was "!= 0" but i think it must be "== 0", because when typing in the edit field of the point, it should be marked as fixed
					{
						(this as IInputObject).SetFixed(true);
					}
					SetGeoPoint(p, SnapPointFinder.DidSnapModes.KeyboardInput);
					if (DefinesBasePoint) constructAction.BasePoint = p;
					// we need to set BasePoint because we also set "fixed"
					if (defaultGeoPoint != null) defaultGeoPoint.Point = p;
					constructAction.RefreshDependantProperties();
				};

				geoPointProperty.ModifyWithMouse += OnModifyWithMouse;
				geoPointProperty.PropertyEntryChangedStateEvent += new PropertyEntryChangedStateDelegate(constructAction.ShowPropertyStateChanged);
				if (ReadOnly)
				{
					geoPointProperty.ReadOnly = true;
				}
				else if (!Optional && !Fixed)
				{
					geoPointProperty.Highlight = true;
				}
				geoPointProperty.GeoPointChanged();
				return geoPointProperty;
			}
			void IInputObject.OnMouse(MouseEventArgs e, MouseState mouseState, IView vw)
			{   // die Maus hat sich bewegt
				if (forwardMouseInputTo != null)
				{   // ggf. weiterleiten an einen anderen Input
					for (int i = 0; i < forwardMouseInputTo.Length; ++i)
					{
						if (!forwardMouseInputTo[i].IsFixed())
						{
							forwardMouseInputTo[i].OnMouse(e, mouseState, vw);
							return;
						}
					}
				}
				SnapPointFinder.DidSnapModes DidSnap;
				GeoPoint p = constructAction.SnapPoint(e, vw, out DidSnap);
				if (MouseClickEvent != null)
				{
					if (mouseState == MouseState.ClickDown) MouseClickEvent(false, p, vw);
					if (mouseState == MouseState.ClickUp) MouseClickEvent(true, p, vw);
				}
				if (Fixed) return;
				if (partiallyFixed != 0)
				{
					p = geoPointProperty.GetPartiallyFixed(p);
					SetGeoPoint(p, DidSnap);
					geoPointProperty.RefreshPartially(); // damit es in der Editbox dargestellt wird
				}
				else
				{
					SetGeoPoint(p, DidSnap);
					geoPointProperty.Refresh(); // damit es in der Editbox dargestellt wird
				}
				constructAction.RefreshDependantProperties();
				if (mouseState == MouseState.ClickUp)
				{
					(this as IInputObject).SetFixed(true);
					if (DefinesBasePoint) constructAction.BasePoint = p;
					if (defaultGeoPoint != null) defaultGeoPoint.Point = p;
					constructAction.SetNextInputIndex(true);
				}
			}
			void IInputObject.Activate(ActivationMode activationMode)
			{
				switch (activationMode)
				{
					case ActivationMode.Inactivate:
						if (defaultGeoPoint != null) defaultGeoPoint.SetAction(null);
						break;
					case ActivationMode.BySelection:
						break;
					case ActivationMode.ByHotspot:
						(this as IInputObject).SetFixed(false);
						break;
				}
			}
			bool IInputObject.AcceptInput(bool acceptOptional)
			{
				if (acceptOptional) return (!Fixed);
				else return (!Fixed && !Optional);
			}
			void IInputObject.MouseLeft()
			{   // die Maus hat die Zeichenfläche verlassen. Wenn es einen default Punkt gibt,
				// so nehmen wir diesen
				if (!Fixed && defaultGeoPoint != null)
				{
					SetGeoPoint(defaultGeoPoint, SnapPointFinder.DidSnapModes.KeyboardInput);
					constructAction.RefreshDependantProperties();
					geoPointProperty.GeoPointChanged();
				}
			}
			void IInputObject.OnActionDone()
			{   // Hotspots wieder wegmachen
				if (DefinesHotSpot)
				{
					foreach (IView vw in constructAction.Frame.AllViews)
					{
						if (vw is IActionInputView)
						{
							PointF pf = vw.Projection.ProjectF(Point);
							int xmin = (int)pf.X - 8;
							int xmax = (int)pf.X + 8;
							int ymin = (int)pf.Y - 8;
							int ymax = (int)pf.Y + 8;
							Rectangle rct = new Rectangle(xmin, ymin, xmax - xmin, ymax - ymin);
							vw.Invalidate(PaintBuffer.DrawingAspect.Select, rct);
						}
					}
				}
			}
			IPropertyEntry IInputObject.GetShowProperty()
			{
				return geoPointProperty;
			}
			bool IInputObject.OnEnter(IPropertyEntry sender, KeyEventArgs args)
			{
				if (sender == geoPointProperty && !args.Control)
				{
					if (geoPointProperty.IsOpen && !geoPointProperty.IsSelected)
					{   // there muste be one of the x, y or z entries selected
						IPropertyEntry[] subentries = geoPointProperty.SubItems;
						IPropertyEntry selected = (geoPointProperty.Parent as IPropertyPage).GetCurrentSelection();
						for (int i = 0; i < subentries.Length; i++)
						{
							if (subentries[i] == selected)
							{
								if (i == subentries.Length - 1)
								{   // Enter auf die letzte Komponente
									(this as IInputObject).SetFixed(true); // als Reaktion auf Enter
									constructAction.SetNextInputIndex(true);
									return true;
								}
								else
								{
									subentries[i + 1].Select(); // .IsSelected = true; // den nächsten selektieren
									return true;
								}
							}
						}
					}
					// falls obiges nicht zum return geführt hat
					(this as IInputObject).SetFixed(true); // als Reaktion auf Enter
					constructAction.SetNextInputIndex(true);
					return true;
				}
				else
				{
					IShowProperty[] subentries = geoPointProperty.SubEntries;
					DoubleProperty ZProperty = subentries[subentries.Length - 1] as DoubleProperty;
					if (sender == ZProperty)
					{
						(this as IInputObject).SetFixed(true); // als Reaktion auf Enter
						constructAction.SetNextInputIndex(true);
						return true;
					}
				}
				return false;
			}
			bool IInputObject.HasHotspot
			{
				get
				{
					return DefinesHotSpot;
				}
			}
			GeoPoint IInputObject.HotspotPosition
			{
				get
				{
					return Point;
				}
			}
			Image IInputObject.HotSpotIcon
			{
				get
				{
					if (HotSpotSource != null)
					{
						return constructAction.Frame.UIService.GetBitmap(HotSpotSource);
					}
					return null;
				}
			}
			bool IInputObject.IsFixed()
			{
				return Fixed;
			}
			protected override void AdjustHighlight()
			{
				if (geoPointProperty != null) geoPointProperty.Highlight = !Optional && !Fixed && !ReadOnly;
			}
			void IInputObject.SetFixed(bool isFixed)
			{
				Fixed = isFixed;
			}
			public override void SetError(string errorMessage)
			{
				geoPointProperty.ErrorMessage = errorMessage;
			}
			#endregion
			
		}
		/// <summary>
		/// Defines an input object for an action derived from ConstructAction.
		/// This input object extpects the input of a vector. The vector may be either entered
		/// on the keyboard or by moving the mouse. The calculation of a vector from the mouse position
		/// depends on various settings.
		/// Pressing enter or TAB or clicking the mouse proceeds to the next input object. 
		/// </summary>
		public class GeoVectorInput : InputObject, IInputObject
		{
			private enum CalculationModes { FromBasePoint, FromPoint, PerpToPlane, PerpToLine, FromCallback }
			private CalculationModes calculationMode; // wie wird der Abstand berechnet
			private GeoPoint vectorFromBasePoint; // zu diesem Punkt wird der Mausinput betrachtet
			/// <summary>
			/// Sets the point as a basis for the calculation of the vector.
			/// The calculated vector is the vector from this point to the mouseposition.
			/// </summary>
			/// <param name="basePoint">the point</param>
			public void SetVectorFromPoint(GeoPoint basePoint)
			{
				calculationMode = CalculationModes.FromPoint;
				vectorFromBasePoint = basePoint;
			}
			private DefaultGeoVector defaultGeoVector;
			/// <summary>
			/// Sets a DefaultGeoVector, which should be a static value, that carries the last
			/// input value of this vector to the next instantiation of the action. 
			/// </summary>
			public DefaultGeoVector DefaultGeoVector
			{
				set { defaultGeoVector = value; }
			}
			private bool isAngle;
			/// <value>
			/// The vector should be interpreted as an angle (IsAngle==true) or as
			/// a direction (IsAngle==false). For an angle there is no vector length,
			/// i.e. the length will always be 1 (unit vector). The PropertyDisplay will
			/// only display a single value (the angle in the drawing plane), the subentries
			/// will contain two angles: drawingplane angle and elevation from drawing plane
			/// </value>
			public bool IsAngle
			{
				get
				{
					return isAngle;
				}
				set
				{
					isAngle = value;
					if (geoVectorProperty != null) geoVectorProperty.IsAngle = isAngle;
				}
			}
			/// <summary>
			/// Defines the plane in which the angle should be computed. The input point
			/// will be projected into this plane and the Angle to the x-axis will be used
			/// as the input
			/// </summary>
			public Plane PlaneForAngle
			{
				get
				{
					if (geoVectorProperty != null) return geoVectorProperty.PlaneForAngle;
					else return constructAction.Frame.ActiveView.Projection.DrawingPlane;
				}
				set
				{
					if (geoVectorProperty != null) geoVectorProperty.PlaneForAngle = value;
				}
			}
			private GeoVectorProperty geoVectorProperty; // die Anzeige desselben
			/// <summary>
			/// Constructs a LengthInput object.
			/// </summary>
			/// <param name="resourceId">the resource id to specify a string from the StringTable.
			/// ResourceId+".Label": the Label left of the
			/// edit box. ResourceId+".ShortInfo": a short tooltip text ResourceId+"DetailedInfo":
			/// a longer tooltip text.
			/// </param>
			public GeoVectorInput(string resourceId)
				: base(resourceId)
			{
				calculationMode = CalculationModes.FromBasePoint;
			}
			/// <summary>
			/// Constructs a LengthInput object with an initial value
			/// </summary>
			/// <param name="resourceId">the resource id to specify a string from the StringTable.
			/// ResourceId+".Label": the Label left of the
			/// edit box. ResourceId+".ShortInfo": a short tooltip text ResourceId+"DetailedInfo":
			/// a longer tooltip text.
			/// </param>
			/// <param name="StartValue">the initial value</param>
			public GeoVectorInput(string resourceId, GeoVector StartValue)
				: this(resourceId)
			{
				Vector = StartValue;
			}
			private GeoVector CalcVector(GeoPoint p)
			{
				if (CalculateGeoVectorEvent != null) return CalculateGeoVectorEvent(p);
				switch (calculationMode)
				{
					case CalculationModes.FromBasePoint:
						if (!constructAction.basePointIsValid) throw (new ActionException("ExpectGeoVector: BasePoint not defined"));
						return p - constructAction.basePoint;
					case CalculationModes.FromPoint:
						return p - vectorFromBasePoint;
					default:
						throw new NotImplementedException();
				}
			}
			/// <summary>
			/// Delegate definition for <see cref="SetGeoVectorEvent"/>
			/// </summary>
			/// <param name="vector">the user provided vector</param>
			/// <returns>true: accepted, false not accepted</returns>
			public delegate bool SetGeoVectorDelegate(GeoVector vector);
			/// <summary>
			/// Provide a method here to get the result of this input (and modify your object)
			/// </summary>
			public event SetGeoVectorDelegate SetGeoVectorEvent;
			/// <summary>
			/// Delegate definitionn for <see cref="GetGeoVectorEvent"/>.
			/// </summary>
			/// <returns>the current value of the vector</returns>
			public delegate GeoVector GetGeoVectorDelegate();
			/// <summary>
			/// Provide a method here, if the vector not only depends from this input, but is also
			/// modified by other means.
			/// </summary>
			public event GetGeoVectorDelegate GetGeoVectorEvent;
			/// <summary>
			/// Delegate definition for the <see cref="CalculateGeoVectorEvent"/>.
			/// </summary>
			/// <param name="MousePosition">current mouse position in model coordinates</param>
			/// <returns>the calculated vector</returns>
			public delegate GeoVector CalculateGeoVectorDelegate(GeoPoint MousePosition);
			/// <summary>
			/// Provide a method here, if you want to calculate the vector yourself, i.e. if vector
			/// from point, line or plane is not appropriate to your needs.
			/// </summary>
			public event CalculateGeoVectorDelegate CalculateGeoVectorEvent;
			/// <summary>
			/// Delegate definition for <see cref="InputValueChangedEvent"/>.
			/// </summary>
			/// <param name="vector">New value of vector</param>
			public delegate void InputValueChangedDelegate(GeoVector vector);
			/// <summary>
			/// This event is raised if the Vector property is changed
			/// </summary>
			public event InputValueChangedDelegate InputValueChangedEvent;

			private GeoVector vector;
			public GeoVector Vector
			{
				get => vector;
				private set
				{
					if (vector == value)
						return;

					vector = value;
					InputValueChangedEvent?.Invoke(value);
				}
			}

			/// <summary>
			/// Forces the input object to the specified value. The input filed is updated accordingly.
			/// </summary>
			/// <param name="val">the value to set</param>
			public void ForceValue(GeoVector val)
			{
				InternalSetGeoVector(val);
				constructAction.RefreshDependantProperties();
				geoVectorProperty.GeoVectorChanged(); // damit es in der Editbox dargestellt wird
			}
			private void InternalSetGeoVector(GeoVector v)
			{
				if (ReadOnly) return;
				if (SetGeoVectorEvent == null) return;
				if (SetGeoVectorEvent(v))
				{
					Vector = v;
				}
			}
			private void OnModifyWithMouse(IPropertyEntry sender, bool StartModifying)
			{
				(this as IInputObject).SetFixed(false);
				constructAction.SetCurrentInputIndex(this, true);
			}
			/// <summary>
			/// Event that is fired when a mousclick happens and this input has the focus.
			/// </summary>
			public event MouseClickDelegate MouseClickEvent;
			/// <summary>
			/// true: this input field does not accept user input, 
			/// false: normal input field that requires user input
			/// </summary>
			public override bool ReadOnly
			{
				get
				{
					return base.ReadOnly;
				}
				set
				{
					base.ReadOnly = value;
					if (geoVectorProperty != null) geoVectorProperty.ReadOnly = value;
				}
			}
			private IInputObject[] forwardMouseInputTo;
			/// <summary>
			/// Mouse input should be forwarded to another input object and only processed
			/// by this input, when the other input object is fixed.
			/// </summary>
			public object ForwardMouseInputTo
			{
				set
				{
					if (value is IInputObject)
					{
						forwardMouseInputTo = new IInputObject[] { value as IInputObject };
					}
					else if (value is object[])
					{
						object[] v = value as object[];
						forwardMouseInputTo = new IInputObject[v.Length];
						for (int i = 0; i < v.Length; ++i)
						{
							forwardMouseInputTo[i] = v[i] as IInputObject;
						}
					}
				}
				get
				{
					return forwardMouseInputTo;
				}
			}
			#region IInputObject
			void IInputObject.Init(ConstructAction a)
			{
				base.Init(a);
				if (defaultGeoVector != null)
				{
					defaultGeoVector.SetAction(a);
					InternalSetGeoVector(defaultGeoVector);
				}
				if (GetGeoVectorEvent != null) Vector = GetGeoVectorEvent();
				if (geoVectorProperty != null) geoVectorProperty.GeoVectorChanged();
			}
			void IInputObject.Refresh()
			{
				if (GetGeoVectorEvent != null)
				{
					Vector = GetGeoVectorEvent();
					if (geoVectorProperty != null) geoVectorProperty.GeoVectorChanged();
				}
			}
			IPropertyEntry IInputObject.BuildShowProperty()
			{
				geoVectorProperty = new GeoVectorProperty(constructAction.Frame, ResourceId);
				geoVectorProperty.OnGetValue = () => Vector;
				geoVectorProperty.OnSetValue = (v) =>
				{
					(this as IInputObject).SetFixed(true);
					InternalSetGeoVector(v);
					constructAction.RefreshDependantProperties();
					if (defaultGeoVector != null)
						defaultGeoVector.Vector = v;
				};

				geoVectorProperty.ModifyWithMouse += new ModifyWithMouseDelegate(OnModifyWithMouse);
				geoVectorProperty.PropertyEntryChangedStateEvent += new PropertyEntryChangedStateDelegate(constructAction.ShowPropertyStateChanged);
				geoVectorProperty.LockedChanged += new GeoVectorProperty.LockedChangedDelegate(OnLockedChanged);
				if (defaultGeoVector != null && !Optional)
				{
					geoVectorProperty.Lockable = true;
					geoVectorProperty.Locked = defaultGeoVector.Locked;
					if (defaultGeoVector.Locked) (this as IInputObject).SetFixed(true);
				}

				if (ReadOnly)
				{
					geoVectorProperty.ReadOnly = true;
				}
				else if (!Optional && !Fixed)
				{
					geoVectorProperty.Highlight = true;
				}
				geoVectorProperty.GeoVectorChanged(); // damit der Startwert dargestellt wird
				geoVectorProperty.IsAngle = isAngle;
				if (isAngle)
				{
					geoVectorProperty.PlaneForAngle = constructAction.Frame.ActiveView.Projection.DrawingPlane;
				}
				return geoVectorProperty;
			}
			void IInputObject.OnMouse(MouseEventArgs e, MouseState mouseState, IView vw)
			{
				if (forwardMouseInputTo != null)
				{   // ggf. weiterleiten an einen anderen Input
					for (int i = 0; i < forwardMouseInputTo.Length; ++i)
					{
						if (!forwardMouseInputTo[i].IsFixed())
						{
							forwardMouseInputTo[i].OnMouse(e, mouseState, vw);
							return;
						}
					}
				}
				SnapPointFinder.DidSnapModes DidSnap;
				GeoPoint p;
				switch (calculationMode)
				{
					case CalculationModes.FromBasePoint:
						if (!constructAction.basePointIsValid)
							p = constructAction.SnapPoint(e, vectorFromBasePoint, vw, out DidSnap);
						else p = constructAction.SnapPoint(e, constructAction.basePoint, vw, out DidSnap);
						break;
					case CalculationModes.FromPoint:
						p = constructAction.SnapPoint(e, vectorFromBasePoint, vw, out DidSnap);
						break;
					default:
						p = constructAction.SnapPoint(e, vw, out DidSnap);
						break;
				}
				if (MouseClickEvent != null)
				{
					if (mouseState == MouseState.ClickDown) MouseClickEvent(false, p, vw);
					if (mouseState == MouseState.ClickUp) MouseClickEvent(true, p, vw);
				}
				if (Fixed) return; // kein Mausinput wird hier akzeptiert
				try
				{
					GeoVector v = CalcVector(p);
					InternalSetGeoVector(v);
					constructAction.RefreshDependantProperties();
					geoVectorProperty.GeoVectorChanged(); // damit es in der Editbox dargestellt wird
					if (mouseState == MouseState.ClickUp)
					{
						(this as IInputObject).SetFixed(true);
						if (defaultGeoVector != null) defaultGeoVector.Vector = v;
						constructAction.SetNextInputIndex(true);
					}
				}
				catch (ActionException)
				{   // das Berechnen der Länge ging schief, meisten weil kein Basepoint gesetzt ist
					// und die deshalb nichts berechnet werden kann.
				}
			}
			void IInputObject.Activate(ActivationMode activationMode)
			{
				switch (activationMode)
				{
					case ActivationMode.Inactivate:
						if (defaultGeoVector != null) defaultGeoVector.SetAction(null);
						break;
					case ActivationMode.BySelection:
						break;
					case ActivationMode.ByHotspot:
						(this as IInputObject).SetFixed(false);
						break;
				}
			}
			bool IInputObject.AcceptInput(bool acceptOptional)
			{
				if (acceptOptional) return (!Fixed);
				else return (!Fixed && !Optional);
			}
			void IInputObject.MouseLeft()
			{
				// wenn die Maus das Spielfeld verlässt, dann wird wieder der default-Wert gesetzt
				if (defaultGeoVector != null)
				{
					InternalSetGeoVector(defaultGeoVector);
					constructAction.RefreshDependantProperties();
					geoVectorProperty.GeoVectorChanged(); // damit es in der Editbox dargestellt wird
				}
			}

			bool IInputObject.HasHotspot
			{
				get
				{
					return false;
				}
			}
			GeoPoint IInputObject.HotspotPosition
			{
				get
				{
					return GeoPoint.Origin;
				}
			}
			Image IInputObject.HotSpotIcon { get { return null; } }

			void IInputObject.OnActionDone() { }
			IPropertyEntry IInputObject.GetShowProperty()
			{
				return geoVectorProperty;
			}
			bool IInputObject.OnEnter(IPropertyEntry sender, KeyEventArgs args)
			{
				if (sender == geoVectorProperty && !args.Control)
				{
					if (geoVectorProperty.IsOpen && !geoVectorProperty.IsSelected)
					{
						IPropertyEntry[] subentries = geoVectorProperty.SubItems;
						IPropertyEntry selected = (geoVectorProperty.Parent as IPropertyPage).GetCurrentSelection();
						for (int i = 0; i < subentries.Length; i++)
						{
							if (subentries[i] == selected)
							{
								if (i == subentries.Length - 1)
								{   // Enter auf die letzte Komponente
									(this as IInputObject).SetFixed(true); // als Reaktion auf Enter
									constructAction.SetNextInputIndex(true);
									return true;
								}
								else
								{
									subentries[i + 1].Select(); // .IsSelected = true; // den nächsten selektieren
									return true;
								}
							}
						}
					}
					// falls obiges nichts gebracht hat
					(this as IInputObject).SetFixed(true); // als Reaktion auf Enter
					constructAction.SetNextInputIndex(true);
					return true;
				}
				else
				{
					IShowProperty[] subentries = geoVectorProperty.SubEntries;
					DoubleProperty ZProperty = subentries[subentries.Length - 1] as DoubleProperty;
					if (sender == ZProperty)
					{
						(this as IInputObject).SetFixed(true); // als Reaktion auf Enter
						constructAction.SetNextInputIndex(true);
						return true;
					}
				}
				return false;
			}
			bool IInputObject.IsFixed()
			{
				return Fixed;
			}
			protected override void AdjustHighlight()
			{
				if (geoVectorProperty != null) geoVectorProperty.Highlight = !Optional && !Fixed && !ReadOnly;
			}
			void IInputObject.SetFixed(bool isFixed)
			{
				Fixed = isFixed;
			}
			public override void SetError(string errorMessage)
			{
				geoVectorProperty.ErrorMessage = errorMessage;
			}
			#endregion
			private void OnLockedChanged(bool locked)
			{
				if (defaultGeoVector != null) defaultGeoVector.Locked = locked;
				if (locked)
				{
					(this as IInputObject).SetFixed(true);
					if (constructAction.GetCurrentInputObject() == this)
					{
						constructAction.SetNextInputIndex(true);
					}
				}
				else
				{
					(this as IInputObject).SetFixed(false);
				}
			}
		}
		/// <summary>
		/// Defines an input object for an action derived from ConstructAction.
		/// This input object expects the input of a plane. The value may be either entered
		/// on the keyboard or by moving the mouse. There is always a base plane which is by
		/// default the drawing plane. The mouse input is defines a plane which is parallel
		/// to the base plane and contains the mouse point (snapping is applied)
		/// Pressing enter or TAB or clicking the mouse proceeds to the next input object. 
		/// </summary>
		public class PlaneInput : InputObject, IInputObject, ICommandHandler
		{
			private LengthProperty lengthProperty; // die einfache Anzeige
			private Plane basePlane; // die Basis-Ebene
			/// <summary>
			/// Constructs a PlaneInput object.
			/// </summary>
			/// <param name="resourceId">the resource id to specify a string from the StringTable.</param>
			public PlaneInput(string resourceId)
				: base(resourceId)
			{
			}
			/// <summary>
			/// Constructs a PlaneInput object with a initial value.
			/// </summary>
			/// <param name="resourceId">the resource id to specify a string from the StringTable.</param>
			/// <param name="basePlane">the base plane</param>
			public PlaneInput(string resourceId, Plane basePlane)
				: this(resourceId)
			{
				this.basePlane = basePlane;
			}
			/// <summary>
			/// Delegate definition for <see cref="SetPlaneEvent"/>
			/// </summary>
			/// <param name="val">the value that has been provided by the user</param>
			/// <returns>true if accepted, fale otherwise</returns>
			public delegate bool SetPlaneDelegate(Plane val);
			/// <summary>
			/// Provide a method here to get the result of this input (and modify your object)
			/// </summary>
			public event SetPlaneDelegate SetPlaneEvent;
			/// <summary>
			/// Delegate definition for <see cref="GetPlaneEvent"/>
			/// </summary>
			/// <returns>the double value</returns>
			public delegate Plane GetPlaneDelegate();
			/// <summary>
			/// Provide a method here, if the length not only depends from this input, but is also
			/// modified by other means.
			/// </summary>
			public event GetPlaneDelegate GetPlaneEvent;
			private Plane val; // der aktuelle Wert der Ebene
			private double offset; // der anzuzeigende offset
			/// <summary>
			/// Forces the input object to the specified value. The input filed is updated accordingly.
			/// </summary>
			/// <param name="val">the value to set</param>
			public void ForceValue(Plane val)
			{
				offset = 0.0;
				basePlane = val; // base plane wird hiermit auch gesetzt
				InternalSetPlane(val);
				constructAction.RefreshDependantProperties();
				lengthProperty.Refresh(); // damit es in der Editbox dargestellt wird
			}
			private void InternalSetPlane(Plane val)
			{
				if (ReadOnly) return;
				if (SetPlaneEvent == null) return;
				if (SetPlaneEvent(val))
				{
					this.val = val;
				}
			}
			private void OnModifyWithMouse(IShowProperty sender, bool StartModifying)
			{
				(this as IInputObject).SetFixed(false);
				constructAction.SetCurrentInputIndex(this, true);
			}
			/// <summary>
			/// Event that is fired when a mousclick happens and this input has the focus.
			/// </summary>
			public event MouseClickDelegate MouseClickEvent;
			/// <summary>
			/// true: this input field does not accept user input, 
			/// false: normal input field that requires user input
			/// </summary>
			public override bool ReadOnly
			{
				get
				{
					return base.ReadOnly;
				}
				set
				{
					base.ReadOnly = value;
					if (lengthProperty != null) lengthProperty.ReadOnly = value;
				}
			}
			private IInputObject[] forwardMouseInputTo;
			/// <summary>
			/// Mouse input should be forwarded to another input object and only processed
			/// by this input, when the other input object is fixed.
			/// </summary>
			public object ForwardMouseInputTo
			{
				set
				{
					if (value is IInputObject)
					{
						forwardMouseInputTo = new IInputObject[] { value as IInputObject };
					}
					else if (value is object[])
					{
						object[] v = value as object[];
						forwardMouseInputTo = new IInputObject[v.Length];
						for (int i = 0; i < v.Length; ++i)
						{
							forwardMouseInputTo[i] = v[i] as IInputObject;
						}
					}
				}
				get
				{
					return forwardMouseInputTo;
				}
			}
			#region IInputObject
			void IInputObject.Init(ConstructAction a)
			{
				base.Init(a);
				basePlane = constructAction.ActiveDrawingPlane;
				val = basePlane;
				offset = 0;
				if (GetPlaneEvent != null)
				{
					val = GetPlaneEvent();
					offset = basePlane.Distance(val.Location);
				}
				if (lengthProperty != null) lengthProperty.Refresh();
			}
			void IInputObject.Refresh()
			{
				if (GetPlaneEvent != null)
				{
					val = GetPlaneEvent();
					offset = basePlane.Distance(val.Location);
					if (lengthProperty != null) lengthProperty.Refresh();
				}
			}
			IPropertyEntry IInputObject.BuildShowProperty()
			{
				lengthProperty = new LengthProperty(constructAction.Frame, ResourceId);
				lengthProperty.OnGetValue = () => offset;
				lengthProperty.OnSetValue = (l) =>
				{
					offset = l;
					(this as IInputObject).SetFixed(true);
					InternalSetPlane(basePlane.Offset(l));
				};

				lengthProperty.PropertyEntryChangedStateEvent += new PropertyEntryChangedStateDelegate(constructAction.ShowPropertyStateChanged);
				lengthProperty.SetContextMenu("MenuId.IntersectionPlane", this);

				if (ReadOnly)
				{
					// doubleProperty.ReadOnly = true;
				}
				if (Optional)
				{
					// doubleProperty.ShowMouseButton = false;
				}
				else if (!Optional && !Fixed)
				{
					lengthProperty.Highlight = true;
				}
				lengthProperty.Refresh();
				return lengthProperty;
			}
			void IInputObject.OnMouse(MouseEventArgs e, MouseState mouseState, IView vw)
			{
				if (forwardMouseInputTo != null)
				{   // ggf. weiterleiten an einen anderen Input
					for (int i = 0; i < forwardMouseInputTo.Length; ++i)
					{
						if (!forwardMouseInputTo[i].IsFixed())
						{
							forwardMouseInputTo[i].OnMouse(e, mouseState, vw);
							return;
						}
					}
				}
				SnapPointFinder.DidSnapModes DidSnap;
				GeoPoint p = constructAction.SnapPoint(e, vw, out DidSnap);
				if (MouseClickEvent != null)
				{
					if (mouseState == MouseState.ClickDown) MouseClickEvent(false, p, vw);
					if (mouseState == MouseState.ClickUp) MouseClickEvent(true, p, vw);
				}
				if (Fixed) return; // kein Mausinput wird hier akzeptiert
				try
				{
					offset = basePlane.Distance(p);
					// Plane pl = basePlane.Offset(offset);
					// nicht nur Offset sonder auch die echte Position, das gibt bessere Rückmeldung
					Plane pl = basePlane; // Plane ist struct
					pl.Location = p;
					InternalSetPlane(pl);
					constructAction.RefreshDependantProperties();
					lengthProperty.Refresh(); // damit es in der Editbox dargestellt wird
					if (mouseState == MouseState.ClickUp)
					{
						(this as IInputObject).SetFixed(true);
						constructAction.SetNextInputIndex(true);
					}
				}
				catch (ActionException)
				{   // das Berechnen der Länge ging schief, meisten weil kein Basepoint gesetzt ist
					// und die deshalb nichts berechnet werden kann.
				}
			}
			void IInputObject.Activate(ActivationMode activationMode)
			{
				switch (activationMode)
				{
					case ActivationMode.Inactivate: break;
					case ActivationMode.BySelection: break;
					case ActivationMode.ByHotspot: (this as IInputObject).SetFixed(false); break;
				}
			}
			bool IInputObject.AcceptInput(bool acceptOptional)
			{
				return base.AcceptInput(acceptOptional);
			}
			void IInputObject.MouseLeft()
			{
				// wenn die Maus das Spielfeld verlässt, dann wird wieder der default-Wert gesetzt
			}

			bool IInputObject.HasHotspot
			{
				get
				{
					return false;
				}
			}
			GeoPoint IInputObject.HotspotPosition
			{
				get
				{
					return GeoPoint.Origin;
				}
			}
			Image IInputObject.HotSpotIcon { get { return null; } }
			void IInputObject.OnActionDone()
			{
			}
			IPropertyEntry IInputObject.GetShowProperty()
			{
				return lengthProperty;
			}
			bool IInputObject.OnEnter(IPropertyEntry sender, KeyEventArgs args)
			{
				if (sender == lengthProperty && !args.Control)
				{
					(this as IInputObject).SetFixed(true); // als Reaktion auf Enter
					constructAction.SetNextInputIndex(true);
					return true;
				}
				return false;
			}
			bool IInputObject.IsFixed()
			{
				return Fixed;
			}
			protected override void AdjustHighlight()
			{
				if (lengthProperty != null) lengthProperty.Highlight = !Optional && !Fixed && !ReadOnly;
			}
			void IInputObject.SetFixed(bool isFixed)
			{
				Fixed = isFixed;
			}
			public override void SetError(string errorMessage)
			{
			}
			#endregion

			#region ICommandHandler Members
			bool ICommandHandler.OnCommand(string MenuId)
			{
				switch (MenuId)
				{
					case "MenuId.DrawingPlane.StandardXY":
						ForceValue(new Plane(Plane.StandardPlane.XYPlane, 0.0));
						return true;
					case "MenuId.DrawingPlane.StandardXZ":
						ForceValue(new Plane(Plane.StandardPlane.XZPlane, 0.0));
						return true;
					case "MenuId.DrawingPlane.StandardYZ":
						ForceValue(new Plane(Plane.StandardPlane.YZPlane, 0.0));
						return true;
					case "MenuId.DrawingPlane.Three.Points":
						ConstructPlane cp = new ConstructPlane("Construct.DrawingPlane");
						cp.ActionDoneEvent += new ActionDoneDelegate(OnConstructPlaneDone);
						constructAction.Frame.SetAction(cp);
						return true;
					case "MenuId.Plane.2PointsDrawingPlane":
						ConstructPlane2PointsDrawingPlane cp2 = new ConstructPlane2PointsDrawingPlane();
						cp2.ActionDoneEvent += new ActionDoneDelegate(OnConstructPlaneDone);
						constructAction.Frame.SetAction(cp2);
						return true;
					case "MenuId.Plane.OriginNormalPoint":
						ConstructPlaneOriginNormalPoint cp3 = new ConstructPlaneOriginNormalPoint();
						cp3.ActionDoneEvent += new ActionDoneDelegate(OnConstructPlaneDone);
						constructAction.Frame.SetAction(cp3);
						return true;
					case "MenuId.DrawingPlane.Tangential":
						ConstructTangentialPlane ct = new ConstructTangentialPlane("Construct.DrawingPlane");
						ct.ActionDoneEvent += new ActionDoneDelegate(OnConstructPlaneDone);
						constructAction.Frame.SetAction(ct);
						return true;
					case "MenuId.Plane.OfCurve":
						ConstructPlaneOfCurve cpc = new ConstructPlaneOfCurve("Construct.Plane.OfCurve");
						cpc.ActionDoneEvent += new ActionDoneDelegate(OnConstructPlaneDone);
						constructAction.Frame.SetAction(cpc);
						return true;
				}
				return false;
			}
			void OnConstructPlaneDone(ConstructAction ca, bool success)
			{
				if (success)
				{
					ForceValue((ca as IConstructPlane).ConstructedPlane);
					(this as IInputObject).SetFixed(true);
					constructAction.SetNextInputIndex(true);
				}
			}
			bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
			{
				switch (MenuId)
				{
					case "MenuId.DrawingPlane.StandardXY":
					case "MenuId.DrawingPlane.StandardXY.Offset":
					case "MenuId.DrawingPlane.StandardXZ":
					case "MenuId.DrawingPlane.StandardXZ.Offset":
					case "MenuId.DrawingPlane.StandardYZ":
					case "MenuId.DrawingPlane.StandardYZ.Offset":
					case "MenuId.DrawingPlane.Three.Points":
						// immer Enabled
						return true;
				}
				return false;
			}
			void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
			#endregion
		}
		/// <summary>
		/// Defines an input object for an action derived from ConstructAction.
		/// This input object extpects the input of a boolen value. The boolen value
		/// is selected from a combobox.
		/// Pressing enter or TAB or clicking the mouse proceeds to the next input object. 
		/// </summary>
		public class BooleanInput : InputObject, IInputObject
		{
			private string resourceIdValues; // String-ID der Werte für true bzw. false
			private DefaultBoolean defaultBoolean;
			/// <summary>
			/// Sets a DefaultGeoVector, which should be a static value, that carries the last
			/// input value of this vector to the next instantiation of the action. 
			/// </summary>
			public DefaultBoolean DefaultBoolean
			{
				get { return defaultBoolean; }
				set { defaultBoolean = value; }
			}
			private BooleanProperty booleanProperty;
			/// <summary>
			/// Delegate definition for <see cref="SetBooleanEvent"/>
			/// </summary>
			/// <param name="val">the selection made by the user</param>
			public delegate void SetBooleanDelegate(bool val);
			/// <summary>
			/// Provide a method here to get the result of this input (and modify your object)
			/// </summary>
			public event SetBooleanDelegate SetBooleanEvent;
			/// <summary>
			/// Delegate definition for <see cref="GetBooleanEvent"/>
			/// </summary>
			/// <returns>the current value of this input</returns>
			public delegate bool GetBooleanDelegate();
			/// <summary>
			/// Provide a method here, if the boolean value not only depends from this input, but is also
			/// modified by other means.
			/// </summary>
			public event GetBooleanDelegate GetBooleanEvent;
			/// <summary>
			/// Delegate definiation for <see cref="InputValueChangedEvent"/>
			/// </summary>
			/// <param name="length">New length value</param>
			public delegate void InputValueChangedDelegate(bool newValue);
			/// <summary>
			/// Event will be rasied if the Length property changed
			/// </summary>
			public event InputValueChangedDelegate InputValueChangedEvent;
			bool boolval;
			public bool Value
			{
				get
				{
					return boolval;
				}
				private set
				{
					if (boolval == value)
						return;

					boolval = value;
					InputValueChangedEvent?.Invoke(value);
				}
			}

			/// <summary>
			/// Forces the input object to the specified value.
			/// </summary>
			/// <param name="val">the value to set</param>
			public void ForceValue(bool val)
			{
				//No InternalSetBoolean implemented                
				if (ReadOnly) return;
				SetBooleanEvent(val);
				Value = val;
				constructAction.RefreshDependantProperties();
				BooleanChanged(this, val);
			}

			/// <summary>
			/// Constructs a BooleanInput object with no initial value
			/// </summary>
			/// <param name="resourceId">the resource id to specify a string from the StringTable.
			/// ResourceId+".Label": the Label left of the
			/// edit box. ResourceId+".ShortInfo": a short tooltip text ResourceId+"DetailedInfo":
			/// a longer tooltip text.
			/// </param>
			/// <param name="resourceIdValues">resource id of the strings for the values starting with
			/// a delimiter and seperated by the same delimiter e.g. "|true|false"</param>
			public BooleanInput(string resourceId, string resourceIdValues)
				: base(resourceId)
			{
				this.resourceIdValues = resourceIdValues;
			}
			/// <summary>
			/// Constructs a BooleanInput object with an initial value
			/// </summary>
			/// <param name="resourceId">the resource id to specify a string from the StringTable.
			/// ResourceId+".Label": the Label left of the
			/// edit box. ResourceId+".ShortInfo": a short tooltip text ResourceId+"DetailedInfo":
			/// a longer tooltip text.
			/// </param>
			/// <param name="resourceIdValues">resource id of the strings for the values starting with
			/// a delimiter and seperated by the same delimiter e.g. "|true|false"</param>
			/// <param name="StartValue">the initial value</param>
			public BooleanInput(string resourceId, string resourceIdValues, bool StartValue)
				: this(resourceId, resourceIdValues)
			{
				Value = StartValue;
			}
			private IInputObject[] forwardMouseInputTo;
			/// <summary>
			/// Mouse input should be forwarded to another input object and only processed
			/// by this input, when the other input object is fixed.
			/// </summary>
			public object ForwardMouseInputTo
			{
				set
				{
					if (value is IInputObject)
					{
						forwardMouseInputTo = new IInputObject[] { value as IInputObject };
					}
					else if (value is object[])
					{
						object[] v = value as object[];
						forwardMouseInputTo = new IInputObject[v.Length];
						for (int i = 0; i < v.Length; ++i)
						{
							forwardMouseInputTo[i] = v[i] as IInputObject;
						}
					}
				}
				get
				{
					return forwardMouseInputTo;
				}
			}
			void IInputObject.Init(ConstructAction a)
			{
				base.Init(a);
				if (defaultBoolean != null)
				{
					Value = defaultBoolean;
				}
				if (GetBooleanEvent != null) Value = GetBooleanEvent();
			}
			void IInputObject.Refresh()
			{
				if (GetBooleanEvent != null)
				{
					Value = GetBooleanEvent();
					if (booleanProperty != null) booleanProperty.BooleanValue = boolval;
				}
			}
			IPropertyEntry IInputObject.BuildShowProperty()
			{
				booleanProperty = new BooleanProperty(ResourceId, resourceIdValues);
				booleanProperty.BooleanChangedEvent += new BooleanChangedDelegate(BooleanChanged);
				booleanProperty.BooleanValue = boolval;
				booleanProperty.GetBooleanEvent += new CADability.UserInterface.BooleanProperty.GetBooleanDelegate(PropertyOnGetBoolean);
				booleanProperty.SetBooleanEvent += new CADability.UserInterface.BooleanProperty.SetBooleanDelegate(PropertyOnSetBoolean);
				return booleanProperty;
			}
			void IInputObject.OnMouse(MouseEventArgs e, MouseState mouseState, IView vw)
			{
				if (forwardMouseInputTo != null)
				{   // ggf. weiterleiten an einen anderen Input
					for (int i = 0; i < forwardMouseInputTo.Length; ++i)
					{
						if (!forwardMouseInputTo[i].IsFixed())
						{
							forwardMouseInputTo[i].OnMouse(e, mouseState, vw);
							return;
						}
					}
				}
			}
			void IInputObject.Activate(ActivationMode activationMode)
			{
				switch (activationMode)
				{
					case ActivationMode.Inactivate:
						break;
					case ActivationMode.BySelection:
						break;
					case ActivationMode.ByHotspot:
						break;
				}
			}
			bool IInputObject.AcceptInput(bool acceptOptional)
			{
				return false; // keinMaus Input für boolean
			}
			void IInputObject.MouseLeft() { }
			bool IInputObject.HasHotspot
			{
				get
				{
					return false;
				}
			}
			GeoPoint IInputObject.HotspotPosition
			{
				get
				{
					return GeoPoint.Origin;
				}
			}
			Image IInputObject.HotSpotIcon { get { return null; } }
			void IInputObject.OnActionDone() { }

			IPropertyEntry IInputObject.GetShowProperty()
			{
				return booleanProperty;
			}
			bool IInputObject.OnEnter(IPropertyEntry sender, KeyEventArgs args)
			{
				if (sender == booleanProperty && !args.Control)
				{
					(this as IInputObject).SetFixed(true); // als Reaktion auf Enter
					constructAction.SetNextInputIndex(true);
					return true;
				}
				return false;
			}
			bool IInputObject.IsFixed()
			{
				return Fixed;
			}
			void IInputObject.SetFixed(bool isFixed)
			{
				Fixed = isFixed;
			}
			public override void SetError(string errorMessage)
			{
			}
			#endregion
			private void BooleanChanged(object sender, bool NewValue)
			{   // Nachricht vom ShowProperty
				if (SetBooleanEvent != null) SetBooleanEvent(NewValue);
			}
			private bool PropertyOnGetBoolean()
			{
				return boolval;
			}
			private void PropertyOnSetBoolean(bool val)
			{
				if (defaultBoolean != null) defaultBoolean.Boolean = val;
				Value = val;
				if (SetBooleanEvent != null) SetBooleanEvent(val);
				constructAction.RefreshDependantProperties(this);
			}
		}
		/// <summary>
		/// Defines an input object for an action derived from ConstructAction.
		/// This input object extpects the input of a integer value. The integer value
		/// is entered in a editbox and/or with an up/down control.
		/// Pressing enter or TAB or clicking the mouse proceeds to the next input object. 
		/// </summary>
		public class MultipleChoiceInput : InputObject, IInputObject
		{
			private string resourceIdValues; // String-ID der Werte für die Auswahl
			private string[] directValues; // die Werte, direkt als array spezifiziert (nicht aus der Resource)
			/// <summary>
			/// Sets a DefaultInteger, which should be a static value, that carries the last
			/// input value of this vector to the next instantiation of the action. 
			/// </summary>
			public DefaultInteger DefaultChoice;
			private MultipleChoiceProperty multipleChoiceProperty;
			/// <summary>
			/// Delegate definition for <see cref="SetChoiceEvent"/>
			/// </summary>
			/// <param name="val">the user providet value</param>
			public delegate void SetChoiceDelegate(int val);
			/// <summary>
			/// Provide a method here to get the result of this input (and modify your object)
			/// </summary>
			public event SetChoiceDelegate SetChoiceEvent;
			/// <summary>
			/// delegate definition for the <see cref="GetChoiceEvent"/>
			/// </summary>
			/// <returns>the current value</returns>
			public delegate int GetChoiceDelegate();
			/// <summary>
			/// Provide a method here, if the integer value not only depends from this input, but is also
			/// modified by other means.
			/// </summary>
			public event GetChoiceDelegate GetChoiceEvent;
			/// <summary>
			/// Delegate definiation for <see cref="InputValueChangedEvent"/>
			/// </summary>
			/// <param name="length">New length value</param>
			public delegate void InputValueChangedDelegate(int newValue);
			/// <summary>
			/// Event will be rasied if the Length property changed
			/// </summary>
			public event InputValueChangedDelegate InputValueChangedEvent;
			int choice;
			public int Choice
			{
				get
				{
					return choice;
				}
				private set
				{
					if (choice == value)
						return;

					choice = value;
					InputValueChangedEvent?.Invoke(value);
				}
			}

			public void ForceValue(int val)
			{
				if (ReadOnly) return;
				SetChoiceEvent(val);
				Choice = val;
				constructAction.RefreshDependantProperties();
			}

			/// <summary>
			/// Constructs a MultipleChoiceInput object with no initial value
			/// </summary>
			/// <param name="resourceId">the resource id to specify a string from the StringTable.
			/// ResourceId+".Label": the Label left of the
			/// edit box. ResourceId+".ShortInfo": a short tooltip text ResourceId+"DetailedInfo":
			/// a longer tooltip text.
			/// </param>
			/// <param name="resourceIdValues">resource id of the strings for the values starting with
			/// a delimiter and seperated by the same delimiter e.g. "|first|second|third"</param>
			public MultipleChoiceInput(string resourceId, string resourceIdValues)
				: base(resourceId)
			{
				this.resourceIdValues = resourceIdValues;
			}
			/// <summary>
			/// Constructs a MultipleChoiceInput object with no initial value
			/// </summary>
			/// <param name="resourceId">the resource id to specify a string from the StringTable.
			/// ResourceId+".Label": the Label left of the
			/// edit box. ResourceId+".ShortInfo": a short tooltip text ResourceId+"DetailedInfo":
			/// a longer tooltip text.
			/// </param>
			/// <param name="resourceIdValues">resource id of the strings for the values starting with
			/// a delimiter and seperated by the same delimiter e.g. "|first|second|third"</param>
			/// <param name="StartValue">the initial value</param>
			public MultipleChoiceInput(string resourceId, string resourceIdValues, int StartValue)
				: this(resourceId, resourceIdValues)
			{
				Choice = StartValue;
			}
			public MultipleChoiceInput(string resourceId, string[] values, int StartValue)
				: base(resourceId)
			{
				directValues = values;
				Choice = StartValue;
			}
			private IInputObject[] forwardMouseInputTo;
			/// <summary>
			/// Mouse input should be forwarded to another input object and only processed
			/// by this input, when the other input object is fixed.
			/// </summary>
			public object ForwardMouseInputTo
			{
				set
				{
					if (value is IInputObject)
					{
						forwardMouseInputTo = new IInputObject[] { value as IInputObject };
					}
					else if (value is object[])
					{
						object[] v = value as object[];
						forwardMouseInputTo = new IInputObject[v.Length];
						for (int i = 0; i < v.Length; ++i)
						{
							forwardMouseInputTo[i] = v[i] as IInputObject;
						}
					}
				}
				get
				{
					return forwardMouseInputTo;
				}
			}
			void IInputObject.Init(ConstructAction a)
			{
				base.Init(a);
				if (DefaultChoice != null)
				{
					Choice = DefaultChoice;
				}
				if (GetChoiceEvent != null) Choice = GetChoiceEvent();
			}
			void IInputObject.Refresh()
			{
				if (GetChoiceEvent != null)
				{
					Choice = GetChoiceEvent();
					if (multipleChoiceProperty != null) multipleChoiceProperty.SetSelection(Choice);
				}
			}
			IPropertyEntry IInputObject.BuildShowProperty()
			{
				string[] values;
				if (directValues != null) values = directValues;
				else values = StringTable.GetSplittedStrings(resourceIdValues);
				multipleChoiceProperty = new MultipleChoiceProperty(ResourceId, values, values[Choice]);
				// multipleChoiceProperty.BooleanChanged += new BooleanChangedDelegate(BooleanChanged);
				// multipleChoiceProperty.SetSelection(Choice);
				multipleChoiceProperty.ValueChangedEvent += new ValueChangedDelegate(PropertyOnValueChanged);
				// multipleChoiceProperty.OnGetChoice += new Condor.UserInterface.multipleChoiceProperty.GetBooleanDelegate(PropertyOnGetChoice);
				// multipleChoiceProperty.OnSetBoolean += new Condor.UserInterface.BooleanProperty.SetBooleanDelegate(PropertyOnSetBoolean);
				multipleChoiceProperty.ReadOnly = ReadOnly;
				return multipleChoiceProperty;
			}
			void IInputObject.OnMouse(MouseEventArgs e, MouseState mouseState, IView vw)
			{
				if (forwardMouseInputTo != null)
				{   // ggf. weiterleiten an einen anderen Input
					for (int i = 0; i < forwardMouseInputTo.Length; ++i)
					{
						if (!forwardMouseInputTo[i].IsFixed())
						{
							forwardMouseInputTo[i].OnMouse(e, mouseState, vw);
							return;
						}
					}
				}
			}
			void IInputObject.Activate(ActivationMode activationMode)
			{
				switch (activationMode)
				{
					case ActivationMode.Inactivate:
						break;
					case ActivationMode.BySelection:
						break;
					case ActivationMode.ByHotspot:
						break;
				}
			}
			bool IInputObject.AcceptInput(bool acceptOptional)
			{
				return false; // keinMaus Input für MultipleChoice
			}
			void IInputObject.MouseLeft() { }
			bool IInputObject.HasHotspot
			{
				get
				{
					return false;
				}
			}
			GeoPoint IInputObject.HotspotPosition
			{
				get
				{
					return GeoPoint.Origin;
				}
			}
			Image IInputObject.HotSpotIcon { get { return null; } }
			void IInputObject.OnActionDone() { }
			IPropertyEntry IInputObject.GetShowProperty()
			{
				return multipleChoiceProperty;
			}
			bool IInputObject.OnEnter(IPropertyEntry sender, KeyEventArgs args)
			{
				if (sender == multipleChoiceProperty && !args.Control)
				{
					(this as IInputObject).SetFixed(true); // als Reaktion auf Enter
					constructAction.SetNextInputIndex(true);
					return true;
				}
				return false;
			}
			bool IInputObject.IsFixed()
			{
				return Fixed;
			}
			void IInputObject.SetFixed(bool isFixed)
			{
				Fixed = isFixed;
			}
			public override void SetError(string errorMessage)
			{
			}
			private void PropertyOnValueChanged(object sender, object NewValue)
			{
				int val = (sender as MultipleChoiceProperty).CurrentIndex;
				if (DefaultChoice != null) DefaultChoice.Integer = val;
				Choice = val;
				constructAction.RefreshDependantProperties();
				if (SetChoiceEvent != null) SetChoiceEvent(val);
			}
			public override bool ReadOnly
			{
				get
				{
					return base.ReadOnly;
				}
				set
				{
					if (multipleChoiceProperty != null) // was, wenn zu früh gesetzt?
						multipleChoiceProperty.ReadOnly = value;
					base.ReadOnly = value;
				}
			}
		}
		/// <summary>
		/// Defines an input object for an action derived from ConstructAction.
		/// This input object extpects the input of a list of GeoPoints. The user
		/// can define as many points as he or she wants.
		/// Pressing enter or TAB or clicking the mouse proceeds to the next input object. 
		/// </summary>
		public class MultiPointInput : InputObject, IInputObject, IIndexedGeoPoint
		{
			private IIndexedGeoPoint forwardTo; // hier werden die Ereignisse (Punktänderung, neuer Punkt u.s.w.) abgeliefert
			private int activeIndex; // dieser Punkt wird gerade bearbeitet
			private bool activeIndexIsNewPoint; // der Punkt, der gerade bearbeitet wird,
			internal bool contextMenuIsActive;
			// ist ein automatisch am Ende erzeugter neuer Punkt
			private bool lastPointIsHidden; // der letzte Punkt wird nicht dargestellt, da
											// die Maus das Fenster verlassen hat, und auch nicht eingetippt wurde
			private MultiGeoPointProperty multiGeoPointProperty; // die Darstellung der Punktliste
			/// <summary>
			/// Creates a MultiPointInput object that communicates with the provided
			/// <see cref="IIndexedGeoPoint"/> interface.
			/// </summary>
			/// <param name="forwardTo">interface usually implemented by the ConstructAction derived class</param>
			public MultiPointInput(IIndexedGeoPoint forwardTo)
				: base("")
			{
				this.forwardTo = forwardTo;
				activeIndex = 0;
			}
			/// <summary>
			/// Event that is fired when a mousclick happens and this input has the focus.
			/// </summary>
			public event MouseClickDelegate MouseClickEvent;
			/// <summary>
			/// Delegate definiation for <see cref="InputValueChangedEvent"/>
			/// </summary>
			/// <param name="index">Index of the element that was changed</param>
			/// <param name="thePoint">The point that was added or modified. In case of remove the point will be invalid as this point does not exist anymore</param>
			/// <param name="changeType">What kind of change was performed</param>
			public delegate void InputValueChangedDelegate(int index, GeoPoint thePoint, ChangeType changeType);
			/// <summary>
			/// Event will be rasied if the Length property changed
			/// </summary>
			public event InputValueChangedDelegate InputValueChangedEvent;
			/// <summary>
			/// What kind of change was performed in the <see cref="InputValueChangedDelegate"/>
			/// </summary>
			public enum ChangeType
			{
				Insert = 0,
				Remove = 1,
				Modify = 3
			}

			#region IInputObject
			void IInputObject.Refresh()
			{
			}
			void IInputObject.Init(ConstructAction a)
			{
				constructAction = a;
			}
			IPropertyEntry IInputObject.BuildShowProperty()
			{
				multiGeoPointProperty = new MultiGeoPointProperty(this, ResourceId, constructAction.Frame);
				multiGeoPointProperty.ModifyWithMouseEvent += new MultiGeoPointProperty.ModifyWithMouseIndexDelegate(OnModifyWithMouse);
				return multiGeoPointProperty;
			}
			void IInputObject.OnMouse(MouseEventArgs e, MouseState mouseState, IView vw)
			{
				// if (!multiGeoPointProperty.MouseEnabled(activeIndex)) return;
				GeoPoint p;
				SnapPointFinder.DidSnapModes DidSnap;
				if (constructAction.basePointIsValid) p = constructAction.SnapPoint(e, constructAction.basePoint, vw, out DidSnap);
				else p = constructAction.SnapPoint(e, vw, out DidSnap);
				if (MouseClickEvent != null)
				{
					if (mouseState == MouseState.ClickDown) MouseClickEvent(false, p, vw);
					if (mouseState == MouseState.ClickUp) MouseClickEvent(true, p, vw);
				}
				if (lastPointIsHidden && activeIndex == forwardTo.GetGeoPointCount())
				{
					lastPointIsHidden = false;
					forwardTo.InsertGeoPoint(-1, p);
				}
				if (forwardTo.GetGeoPointCount() == 0)
				{   // es gibt noch keinen Punkt
					multiGeoPointProperty.Append(p);
					multiGeoPointProperty.SetFocusToIndex(0);
					// multiGeoPointProperty.SetFocusToLastPoint(); funktioniert nicht
					activeIndex = 0;
				}
				forwardTo.SetGeoPoint(activeIndex, p);
				multiGeoPointProperty.Refresh(activeIndex);
				// im Unterschied zu den meisten anderen Inputs akzeptieren wir hier auch die rechte
				// Maustaste (RightUp) um den Punkt zu fixieren. 
				// Diese kommt nur, wenn der Anweder die Aktion beenden will
				if ((mouseState == MouseState.ClickUp || mouseState == MouseState.RightUp) && forwardTo.MayInsert(-1))
				{   // einen neuen Punkt hinzufügen
					// multiGeoPointProperty.SetFocus();
					multiGeoPointProperty.Append(p);
					constructAction.BasePoint = p;
					activeIndex = forwardTo.GetGeoPointCount() - 1;
					activeIndexIsNewPoint = true;
					multiGeoPointProperty.SetFocusToIndex(activeIndex);
					// multiGeoPointProperty.SetFocusToLastPoint(); funktioniert nicht
					// multiGeoPointProperty.EnableMouse(activeIndex);
				}
			}
			void IInputObject.Activate(ActivationMode activationMode)
			{
				switch (activationMode)
				{
					case ActivationMode.Inactivate:
						activeIndexIsNewPoint = false;
						break;
					case ActivationMode.BySelection:
						if (forwardTo.GetGeoPointCount() == 0)
						{
							forwardTo.InsertGeoPoint(-1, new GeoPoint(0.0, 0.0));
							activeIndexIsNewPoint = true;
						}
						activeIndex = forwardTo.GetGeoPointCount() - 1;
						multiGeoPointProperty.ShowOpen(true);
						multiGeoPointProperty.SetFocusToIndex(activeIndex);
						// multiGeoPointProperty.EnableMouse(activeIndex);
						break;
					case ActivationMode.ByHotspot:
						// ???
						break;
				}
			}
			bool IInputObject.AcceptInput(bool acceptOptional)
			{
				return !Fixed;
			}
			void IInputObject.MouseLeft()
			{
				if (activeIndexIsNewPoint && !contextMenuIsActive)
				{
					forwardTo.RemoveGeoPoint(-1);
					lastPointIsHidden = true;
				}
			}
			bool IInputObject.HasHotspot
			{
				get
				{
					return false;
				}
			}
			GeoPoint IInputObject.HotspotPosition
			{
				get
				{
					return GeoPoint.Origin;
				}
			}
			Image IInputObject.HotSpotIcon { get { return null; } }
			void IInputObject.OnActionDone()
			{   // der Anwender hat auf "fertig" gedrückt.
				// wenn der aktive Index ein neuer Punkt ist, dann müssen wir den entfernen
				// da nicht vom Anwender gewünscht (sonst hätte er ihn gefixed)
				if (activeIndexIsNewPoint && !lastPointIsHidden)
				{
					forwardTo.RemoveGeoPoint(-1);
				}
			}
			IPropertyEntry IInputObject.GetShowProperty()
			{
				return multiGeoPointProperty;
			}
			bool IInputObject.OnEnter(IPropertyEntry sender, KeyEventArgs args)
			{   // Enter im MultiPointInput in der Konstruktion: 
				(this as IInputObject).SetFixed(true); // als Reaktion auf Enter
				if (constructAction.propertyTreeView.GetCurrentSelection() == multiGeoPointProperty || constructAction.CurrentInput == multiGeoPointProperty || args.Shift || args.KeyData.HasFlag(Keys.Shift))
				// if (args.Shift)
				{   // Enter auf alles, also fertig
					constructAction.OnDone();
				}
				else
				{   // Enter auf einen Eintrag, also neuen machen
					if (forwardTo.GetGeoPointCount() <= activeIndex) --activeIndex;
					GeoPoint p = (this as IIndexedGeoPoint).GetGeoPoint(activeIndex);
					multiGeoPointProperty.Append(p);
					constructAction.BasePoint = p;
					activeIndex = forwardTo.GetGeoPointCount() - 1;
					multiGeoPointProperty.SetFocusToIndex(activeIndex);
					activeIndexIsNewPoint = true;
				}
				return true;
			}
			bool IInputObject.IsFixed()
			{
				return Fixed;
			}
			void IInputObject.SetFixed(bool isFixed)
			{
				Fixed = isFixed;
			}
			public override void SetError(string errorMessage)
			{
			}
			#endregion
			#region IIndexedGeoPoint Members
			void IIndexedGeoPoint.SetGeoPoint(int Index, GeoPoint ThePoint)
			{
				if (lastPointIsHidden && Index == forwardTo.GetGeoPointCount())
				{   // der Anwender tippt den letzten Punkt ein, nachdem die Maus 
					// die Fläche verlassen hat
					forwardTo.InsertGeoPoint(-1, ThePoint);
					lastPointIsHidden = false;
					InputValueChangedEvent?.Invoke(Index, ThePoint, ChangeType.Insert);
				}
				else
				{
					forwardTo.SetGeoPoint(Index, ThePoint);
					InputValueChangedEvent?.Invoke(Index, ThePoint, ChangeType.Modify);
				}
				activeIndexIsNewPoint = false;
			}
			GeoPoint IIndexedGeoPoint.GetGeoPoint(int Index)
			{
				return forwardTo.GetGeoPoint(Index);
			}
			void IIndexedGeoPoint.InsertGeoPoint(int Index, GeoPoint ThePoint)
			{
				forwardTo.InsertGeoPoint(Index, ThePoint);
				InputValueChangedEvent?.Invoke(Index, ThePoint, ChangeType.Insert);
			}
			void IIndexedGeoPoint.RemoveGeoPoint(int Index)
			{
				forwardTo.RemoveGeoPoint(Index);
				InputValueChangedEvent?.Invoke(Index, GeoPoint.Invalid, ChangeType.Remove);
			}
			int IIndexedGeoPoint.GetGeoPointCount()
			{
				return forwardTo.GetGeoPointCount();
			}
			bool IIndexedGeoPoint.MayInsert(int Index)
			{
				return forwardTo.MayInsert(Index);
			}
			bool IIndexedGeoPoint.MayDelete(int Index)
			{
				return forwardTo.MayDelete(Index);
			}
			#endregion
			private bool OnModifyWithMouse(IPropertyEntry sender, int index)
			{
				activeIndex = index;
				activeIndexIsNewPoint = false;
				if (lastPointIsHidden)
				{
					// TODO: hier noch naCHDENKEN
				}
				return true;
			}
			internal IPropertyEntry GetActiveEntry()
			{
				if (activeIndex >= 0 && activeIndex < multiGeoPointProperty.SubEntries.Length)
					return multiGeoPointProperty.SubItems[activeIndex];
				else return null;
			}
			internal void FixActivePoint()
			{
				if (activeIndex == forwardTo.GetGeoPointCount() - 1)
				{
					GeoPoint p = forwardTo.GetGeoPoint(forwardTo.GetGeoPointCount() - 1);
					multiGeoPointProperty.Append(p);
					constructAction.BasePoint = p;
					activeIndex = forwardTo.GetGeoPointCount() - 1;
					activeIndexIsNewPoint = true;
				}
			}
		}
		/// <summary>
		/// Defines an input object for an action derived from ConstructAction.
		/// This input object extpects the user to click on a curve (<see cref="IGeoObject"/> that also implements <see cref="ICurve"/>.
		/// Pressing enter or TAB or clicking the mouse proceeds to the next input object. 
		/// </summary>
		public class CurveInput : InputObject, IInputObject
		{
			private string hitCursor, failCursor;
			private CurvesProperty curvesProperty; // die Anzeige desselben
			/// <summary>
			/// Creates a Curveinput object.
			/// </summary>
			/// <param name="resourceId">the resource id to specify a string from the StringTable.
			/// ResourceId+".Label": the Label left of the
			/// edit box. ResourceId+".ShortInfo": a short tooltip text ResourceId+"DetailedInfo":
			/// a longer tooltip text.
			/// </param>
			public CurveInput(string resourceId)
				: base(resourceId)
			{
				hitCursor = "Hand";
				failCursor = "Arrow";
				PreferPath = false;
				ModifiableOnly = false;
			}
			/// <summary>
			/// Sets the cursor that will be displayed when a curve is hit
			/// </summary>
			public string HitCursor
			{
				set { hitCursor = value; }
			}
			/// <summary>
			/// Sets the cursor that will be displayed when no curve is hit
			/// </summary>
			public string FailCursor
			{
				set { failCursor = value; }
			}
			private bool decomposed;
			/// <summary>
			/// true: only decomposed curves are yielded,
			/// false: also path objects, that consist of several subcurves are yielded
			/// </summary>
			public bool Decomposed
			{
				get { return decomposed; }
				set
				{
					decomposed = value;
					if (decomposed) PreferPath = false;
				}
			}
			/// <summary>
			/// true: only curves that may be modified are yielded
			/// false: any kind of curves curves are yielded
			/// </summary>
			public bool ModifiableOnly;

			private ICurve[] curves;
			public ICurve[] Curves
			{
				get => curves;
				private set
				{
					if (curves == value)
						return;

					curves = value;
					InputValueCurvesChangedEvent?.Invoke(value);
				}
			}

			private ICurve selectedCurve;
			public ICurve SelectedCurve
			{
				get => selectedCurve;
				private set
				{
					if (selectedCurve == value)
						return;

					selectedCurve = value;
					InputValueSelectedCurveChangedEvent?.Invoke(value);
				}
			}
			/// <summary>
			/// Forces the given curves to be displayed
			/// </summary>
			/// <param name="curves">array of curves</param>
			/// <param name="selectedCurve">currently selected curve</param>
			public void SetCurves(ICurve[] curves, ICurve selectedCurve)
			{
				if (SelectedCurve != null) constructAction.feedBack.RemoveSelected(SelectedCurve as IGeoObject);
				Curves = curves;
				SelectedCurve = selectedCurve;
				if (SelectedCurve != null) constructAction.feedBack.AddSelected(SelectedCurve as IGeoObject);
				if (curvesProperty != null)
				{
					curvesProperty.SetCurves(curves, selectedCurve);
				}
			}
			/// <summary>
			/// Set the selected curve
			/// </summary>
			/// <param name="curve">this curve should be selected</param>
			public void SetSelectedCurve(ICurve curve)
			{
				curvesProperty.SetSelectedCurve(curve);
			}
			/// <summary>
			///  Gets the currently selected curves.
			/// </summary>
			/// <returns></returns>
			public ICurve[] GetCurves()
			{
				return Curves;
			}
			public ICurve GetSelectedCurve()
			{
				return SelectedCurve;
			}
			/// <summary>
			/// Delegate definition for <see cref="MouseOverCurvesEvent"/>
			/// </summary>
			/// <param name="sender">this object</param>
			/// <param name="TheCurves">curves under the cursor</param>
			/// <param name="up">mous was clicked</param>
			/// <returns>true, if you accept (on of the) curves</returns>
			public delegate bool MouseOverCurvesDelegate(CurveInput sender, ICurve[] TheCurves, bool up);
			/// <summary>
			/// Provide a method here to react on the user moving the cursor over curves.
			/// </summary>
			public event MouseOverCurvesDelegate MouseOverCurvesEvent;
			/// <summary>
			/// Delegate definition for <see cref="CurveSelectionChangedEvent"/>
			/// </summary>
			/// <param name="sender">this object</param>
			/// <param name="SelectedCurve">the usere selected curve</param>
			public delegate void CurveSelectionChangedDelegate(CurveInput sender, ICurve SelectedCurve);
			/// <summary>
			/// Provide a method here to react on the user selecting a different curve
			/// </summary>
			public event CurveSelectionChangedDelegate CurveSelectionChangedEvent;
			/// <summary>
			/// Delegate definiation for <see cref="InputValueCurvesChangedEvent"/>
			/// </summary>
			/// <param name="curves">New Curves</param>            
			public delegate void InputValueCurvesChangedDelegate(ICurve[] curves);
			/// <summary>
			/// Event will be rasied if the Curves changed
			/// </summary>
			public event InputValueCurvesChangedDelegate InputValueCurvesChangedEvent;
			/// <summary>
			/// Delegate definiation for <see cref="InputValueSelectedCurveChangedEvent"/>
			/// </summary>            
			/// <param name="selectedCurve">New selected Curve</param>
			public delegate void InputValueSelectedCurveChangedDelegate(ICurve selectedCurve);
			/// <summary>
			/// Event will be rasied if the SelectedCurve changed
			/// </summary>
			public event InputValueSelectedCurveChangedDelegate InputValueSelectedCurveChangedEvent;
			/// <summary>
			/// Returns two curves (Path), that represent the given curve, splitted at the vertex
			/// which is closest to the mouse position. The endpoint of the first curve is the
			/// startpoint of the second curve. If the curve is not closed and the startpoint or endpoint
			/// is closest to the mouse position, en empty array will be returned.
			/// </summary>
			/// <param name="composedCurve"></param>
			/// <returns></returns>
			public ICurve[] SplitAtMousePosition(ICurve composedCurve)
			{
				Plane pl = constructAction.Frame.ActiveView.Projection.ProjectionPlane;
				ICurve[] subCurves = composedCurve.SubCurves;
				int ind = -2;
				GeoPoint2D mousepos = pl.Project(constructAction.CurrentMousePosition);
				double mindist = Geometry.Dist(mousepos, pl.Project(subCurves[0].StartPoint));
				// der Startpunkt alleine darfs nicht sein, wenn geschlossen, dann wirds über
				// den Endpunkt geregelt.
				for (int i = 0; i < subCurves.Length; ++i)
				{
					GeoPoint2D endpoint = pl.Project(subCurves[i].EndPoint);
					double d = Geometry.Dist(mousepos, endpoint);
					if (d < mindist + Precision.eps) // eps, da bei identischem Start/Endpunkt es hier reingehen soll
					{
						ind = i;
						mindist = d;
					}
				}
				if (subCurves.Length > 1 && ind == subCurves.Length - 1 &&
					Precision.IsEqual(subCurves[0].StartPoint, subCurves[subCurves.Length - 1].EndPoint))
				{
					// Endpunkt der composedCurve getroffen und composedCurve ist geschlossen:
					// Zwei Pathes machen von der Mitte bis zum Ende und vom Ende bis zur Mitte
					ind = subCurves.Length / 2;
					ICurve[] c1 = new ICurve[subCurves.Length - ind];
					// Array.Copy(subCurves,ind,c1,0,subCurves.Length-ind);
					for (int i = 0; i < c1.Length; ++i)
					{
						c1[i] = subCurves[ind + i].Clone();
					}
					ICurve[] c2 = new ICurve[ind];
					// Array.Copy(subCurves,0,c2,0,ind);
					for (int i = 0; i < c2.Length; ++i)
					{
						c2[i] = subCurves[i].Clone();
					}
					Path p1 = Path.Construct();
					p1.Set(c1);
					p1.CopyAttributes(composedCurve as IGeoObject);
					Path p2 = Path.Construct();
					p2.Set(c2);
					p2.CopyAttributes(composedCurve as IGeoObject);
					return new ICurve[] { p1, p2 };
				}
				if (ind >= 0 && ind < subCurves.Length - 1)
				{   // nicht am Ende getroffen
					ICurve[] c1 = new ICurve[ind + 1];
					// Array.Copy(subCurves,0,c1,0,ind+1);
					for (int i = 0; i < c1.Length; ++i)
					{
						c1[i] = subCurves[i].Clone();
					}
					ICurve[] c2 = new ICurve[subCurves.Length - ind - 1];
					// Array.Copy(subCurves,ind+1,c2,0,subCurves.Length-ind-1);
					for (int i = 0; i < c2.Length; ++i)
					{
						c2[i] = subCurves[ind + 1 + i].Clone();
					}
					Path p1 = Path.Construct();
					p1.Set(c1);
					p1.CopyAttributes(composedCurve as IGeoObject);
					Path p2 = Path.Construct();
					p2.Set(c2);
					p2.CopyAttributes(composedCurve as IGeoObject);
					return new ICurve[] { p1, p2 };
				}
				return new ICurve[0];
			}
			/// <summary>
			/// Prefer path objects instead of single curves (when available)
			/// </summary>
			public bool PreferPath;
			private IInputObject[] forwardMouseInputTo;
			/// <summary>
			/// Mouse input should be forwarded to another input object and only processed
			/// by this input, when the other input object is fixed.
			/// </summary>
			public object ForwardMouseInputTo
			{
				set
				{
					if (value is IInputObject)
					{
						forwardMouseInputTo = new IInputObject[] { value as IInputObject };
					}
					else if (value is object[])
					{
						object[] v = value as object[];
						forwardMouseInputTo = new IInputObject[v.Length];
						for (int i = 0; i < v.Length; ++i)
						{
							forwardMouseInputTo[i] = v[i] as IInputObject;
						}
					}
				}
				get
				{
					return forwardMouseInputTo;
				}
			}
			#region IInputObject implementation
			void IInputObject.Init(ConstructAction a)
			{
				constructAction = a;
			}
			void IInputObject.Refresh()
			{
			}
			IPropertyEntry IInputObject.BuildShowProperty()
			{
				curvesProperty = new CurvesProperty(ResourceId, constructAction.Frame);
				curvesProperty.PropertyEntryChangedStateEvent += new PropertyEntryChangedStateDelegate(constructAction.ShowPropertyStateChanged);
				curvesProperty.SelectionChangedEvent += new CurvesProperty.SelectionChangedDelegate(OnSelectedCurveChanged);
				if (Curves != null) curvesProperty.SetCurves(Curves, SelectedCurve);
				if (!Optional && !Fixed)
				{
					curvesProperty.Highlight = true;
				}
				return curvesProperty;
			}
			void IInputObject.OnMouse(MouseEventArgs e, MouseState mouseState, IView vw)
			{
				if (forwardMouseInputTo != null)
				{   // ggf. weiterleiten an einen anderen Input
					for (int i = 0; i < forwardMouseInputTo.Length; ++i)
					{
						if (!forwardMouseInputTo[i].IsFixed())
						{
							forwardMouseInputTo[i].OnMouse(e, mouseState, vw);
							return;
						}
					}
				}
				Point MousePoint = new Point(e.X, e.Y);
				GeoObjectList l = vw.PickObjects(MousePoint, PickMode.blockchildren);
				ArrayList c = new ArrayList();
				for (int i = 0; i < l.Count; ++i)
				{
					ICurve cv;
					if (PreferPath)
					{
						if (l[i].Owner is Path) cv = l[i].Owner as ICurve;
						else cv = l[i] as ICurve;

					}
					else
					{
						cv = l[i] as ICurve;
					}
					if (ModifiableOnly && cv != null)
					{   // keine Teilobjekte von Blockrefs liefern
						IGeoObject go = cv as IGeoObject;
						if (IGeoObjectImpl.IsOwnedByBlockRef(go)) cv = null;
						if (go.Owner is Edge) cv = null;
					}
					if (cv != null && constructAction.UseFilter)
					{
						if (!constructAction.Frame.Project.FilterList.Accept(cv as IGeoObject))
							cv = null;
					}
					if (cv != null)
					{
						if (decomposed && cv.IsComposed)
						{
							Projection pr = constructAction.Frame.ActiveView.Projection;
							GeoPoint2D p0 = pr.PointWorld2D(MousePoint);
							double d = 5.0 * pr.DeviceToWorldFactor;
							BoundingRect pickrect = new BoundingRect(p0, d, d);
							ICurve[] subcv = cv.SubCurves;
							for (int j = 0; j < subcv.Length; ++j)
							{
								ICurve2D c2d = subcv[j].GetProjectedCurve(pr.ProjectionPlane);
								if (c2d.HitTest(ref pickrect, false))
								{
									c.Add(subcv[j]);
								}
							}
						}
						else
						{
							c.Add(cv);
						}
					}
				}
				if (MouseOverCurvesEvent != null)
				{
					bool next = MouseOverCurvesEvent(this, (ICurve[])c.ToArray(typeof(ICurve)), mouseState == MouseState.ClickUp);
					if (constructAction.AutoCursor)
					{
						if (next) vw.SetCursor(hitCursor);
						else vw.SetCursor(failCursor);
					}
					if (next && (mouseState == MouseState.ClickUp))
					{
						(this as IInputObject).SetFixed(true);
						constructAction.SetNextInputIndex(true);
					}
				}
				constructAction.RefreshDependantProperties();
			}
			void IInputObject.Activate(ActivationMode activationMode)
			{
				switch (activationMode)
				{
					case ActivationMode.Inactivate:
						break;
					case ActivationMode.BySelection:
						break;
					case ActivationMode.ByHotspot:
						break;
				}
			}
			bool IInputObject.AcceptInput(bool acceptOptional)
			{
				return base.AcceptInput(acceptOptional);
			}
			void IInputObject.MouseLeft() { }
			bool IInputObject.HasHotspot
			{
				get
				{
					return false;
				}
			}
			GeoPoint IInputObject.HotspotPosition
			{
				get
				{
					return GeoPoint.Origin;
				}
			}
			Image IInputObject.HotSpotIcon { get { return null; } }

			void IInputObject.OnActionDone()
			{
				if (SelectedCurve != null) constructAction.feedBack.RemoveSelected(SelectedCurve as IGeoObject);
			}
			IPropertyEntry IInputObject.GetShowProperty()
			{
				return curvesProperty;
			}
			bool IInputObject.OnEnter(IPropertyEntry sender, KeyEventArgs args)
			{
				if (MouseOverCurvesEvent != null)
				{
					if (Curves == null) Curves = new ICurve[0];
					return MouseOverCurvesEvent(this, Curves, true);
				}
				return false;
			}
			bool IInputObject.IsFixed()
			{
				return Fixed;
			}
			protected override void AdjustHighlight()
			{
				if (curvesProperty != null) curvesProperty.Highlight = !Optional && !Fixed && !ReadOnly;
			}
			void IInputObject.SetFixed(bool isFixed)
			{
				Fixed = isFixed;
			}
			public override void SetError(string errorMessage)
			{
			}
			private void OnSelectedCurveChanged(CurvesProperty cp, ICurve selectedCurve)
			{
				if (CurveSelectionChangedEvent != null) CurveSelectionChangedEvent(this, selectedCurve);
				if (SelectedCurve != null) constructAction.feedBack.RemoveSelected(SelectedCurve as IGeoObject);
				SelectedCurve = selectedCurve;
				if (SelectedCurve != null) constructAction.feedBack.AddSelected(SelectedCurve as IGeoObject);
				constructAction.RefreshDependantProperties();
			}
			public void SetContextMenu(string menuId, ICommandHandler handler)
			{
				curvesProperty.SetContextMenu(menuId, handler);
			}

		}
		/// <summary>
		/// Defines an input object for an action derived from ConstructAction.
		/// This input object extpects the user to click on a curve (<see cref="IGeoObject"/> that also implements <see cref="ICurve"/>.
		/// Pressing enter or TAB or clicking the mouse proceeds to the next input object. 
		/// </summary>
		public class GeoObjectInput : InputObject, IInputObject
		{
			private string hitCursor, failCursor;
			private GeoObjectProperty geoObjectProperty; // die Anzeige desselben
			/// <summary>
			/// Creates a GeoObjectInput object.
			/// </summary>
			/// <param name="resourceId">the resource id to specify a string from the StringTable.
			/// ResourceId+".Label": the Label left of the
			/// edit box. ResourceId+".ShortInfo": a short tooltip text ResourceId+"DetailedInfo":
			/// a longer tooltip text.
			/// </param>
			public GeoObjectInput(string resourceId)
				: base(resourceId)
			{
				hitCursor = "Hand";
				failCursor = "Arrow";
				ModifiableOnly = false;
				EdgesOnly = false;
				MultipleInput = false;
			}
			/// <summary>
			/// Sets the cursor that will be displayed when a curve is hit
			/// </summary>
			public string HitCursor
			{
				set { hitCursor = value; }
			}
			/// <summary>
			/// Sets the cursor that will be displayed when no curve is hit
			/// </summary>
			public string FailCursor
			{
				set { failCursor = value; }
			}
			private bool decomposed;
			/// <summary>
			/// true: only decomposed curves are yielded,
			/// false: also path objects, that consist of several subcurves are yielded
			/// </summary>
			public bool Decomposed
			{
				get { return decomposed; }
				set
				{
					decomposed = value;
				}
			}
			/// <summary>
			/// true: only curves that may be modified are yielded
			/// false: any kind of curves curves are yielded
			/// </summary>
			public bool ModifiableOnly;
			public bool EdgesOnly = false;
			public bool FacesOnly = false;
			public bool Points = false;
			public bool MultipleInput;
			public Point currentMousePoint;
			private IGeoObject[] geoObjects;
			private IGeoObject selectedGeoObject;
			/// <summary>
			/// Forces the given geoObjects to be displayed
			/// </summary>
			/// <param name="GeoObjects">array of GeoObjects</param>
			/// <param name="selectedGeoObject">currently selected GeoObject</param>
			public void SetGeoObject(IGeoObject[] GeoObjects, IGeoObject selectedGeoObject)
			{
				if (this.selectedGeoObject != null) constructAction.feedBack.RemoveSelected(this.selectedGeoObject);
				geoObjects = GeoObjects;
				this.selectedGeoObject = selectedGeoObject;
				if (this.selectedGeoObject != null) constructAction.feedBack.AddSelected(this.selectedGeoObject);
				if (geoObjectProperty != null)
				{
					geoObjectProperty.SetGeoObjects(GeoObjects, selectedGeoObject);
				}
			}
			/// <summary>
			/// Set the selected GeoObject
			/// </summary>
			/// <param name="curve">this curve should be selected</param>
			public void SetSelectedGeoObject(IGeoObject geoObject)
			{
				geoObjectProperty.SetSelectedGeoObject(geoObject);
			}
			/// <summary>
			///  Gets the currently selected curves.
			/// </summary>
			/// <returns></returns>
			public IGeoObject[] GetGeoObjects()
			{
				return geoObjects;
			}
			/// <summary>
			/// Delegate definition for <see cref="MouseOverGeoObjectsEvent"/>
			/// </summary>
			/// <param name="sender">this object</param>
			/// <param name="TheGeoObjects">GeoObjects under the cursor</param>
			/// <param name="up">mous was clicked</param>
			/// <returns>true, if you accept (one of the) GeoObjects</returns>
			public delegate bool MouseOverGeoObjectsDelegate(GeoObjectInput sender, IGeoObject[] TheGeoObjects, bool up);
			/// <summary>
			/// Provide a method here to react on the user moving the cursor over GeoObjects.
			/// </summary>
			public event MouseOverGeoObjectsDelegate MouseOverGeoObjectsEvent;
			/// <summary>
			/// Delegate definition for <see cref="CurveSelectionChangedEvent"/>
			/// </summary>
			/// <param name="sender">this object</param>
			/// <param name="SelectedGeoObject">the user selected GeoObject</param>
			public delegate void GeoObjectSelectionChangedDelegate(GeoObjectInput sender, IGeoObject SelectedGeoObject);
			/// <summary>
			/// Provide a method here to react on the user selecting a different curve
			/// </summary>
			public event GeoObjectSelectionChangedDelegate GeoObjectSelectionChangedEvent;
			private IInputObject[] forwardMouseInputTo;
			/// <summary>
			/// Mouse input should be forwarded to another input object and only processed
			/// by this input, when the other input object is fixed.
			/// </summary>
			public object ForwardMouseInputTo
			{
				set
				{
					if (value is IInputObject)
					{
						forwardMouseInputTo = new IInputObject[] { value as IInputObject };
					}
					else if (value is object[])
					{
						object[] v = value as object[];
						forwardMouseInputTo = new IInputObject[v.Length];
						for (int i = 0; i < v.Length; ++i)
						{
							forwardMouseInputTo[i] = v[i] as IInputObject;
						}
					}
				}
				get
				{
					return forwardMouseInputTo;
				}
			}
			#region IInputObject implementation
			void IInputObject.Init(ConstructAction a)
			{
				constructAction = a;
			}
			void IInputObject.Refresh()
			{
			}
			IPropertyEntry IInputObject.BuildShowProperty()
			{
				geoObjectProperty = new GeoObjectProperty(ResourceId, constructAction.Frame);
				geoObjectProperty.PropertyEntryChangedStateEvent += new PropertyEntryChangedStateDelegate(constructAction.ShowPropertyStateChanged);
				geoObjectProperty.SelectionChangedEvent += new GeoObjectProperty.SelectionChangedDelegate(OnSelectedGeoObjectChanged);
				if (geoObjects != null) geoObjectProperty.SetGeoObjects(geoObjects, selectedGeoObject);
				if (!Optional && !Fixed)
				{
					geoObjectProperty.Highlight = true;
				}
				return geoObjectProperty;
			}
			void IInputObject.OnMouse(MouseEventArgs e, MouseState mouseState, IView vw)
			{
				if (forwardMouseInputTo != null)
				{   // ggf. weiterleiten an einen anderen Input
					for (int i = 0; i < forwardMouseInputTo.Length; ++i)
					{
						if (!forwardMouseInputTo[i].IsFixed())
						{
							forwardMouseInputTo[i].OnMouse(e, mouseState, vw);
							return;
						}
					}
				}
				Point MousePoint = new Point(e.X, e.Y);
				currentMousePoint = MousePoint;
				GeoObjectList l = new GeoObjectList();
				if (EdgesOnly) l.AddRange(constructAction.Frame.ActiveView.PickObjects(MousePoint, PickMode.singleEdge));
				if (FacesOnly) l.AddRange(constructAction.Frame.ActiveView.PickObjects(MousePoint, PickMode.singleFace));
				if (!FacesOnly && !EdgesOnly) l = constructAction.Frame.ActiveView.PickObjects(MousePoint, PickMode.normal);
				if (Points)
				{
					SnapPointFinder.DidSnapModes DidSnap;
					GeoPoint p = constructAction.SnapPoint(e, vw, out DidSnap);
					if (DidSnap != SnapPointFinder.DidSnapModes.DidNotSnap)
					{
						GeoObject.Point pnt = GeoObject.Point.Construct();
						pnt.Location = p;
						pnt.Symbol = PointSymbol.Cross;
						l.Add(pnt);
					}
				}
				ArrayList c = new ArrayList();
				for (int i = 0; i < l.Count; ++i)
				{
					IGeoObject go; ;
					go = l[i];
					if (ModifiableOnly && go != null)
					{   // keine Teilobjekte von Blockrefs liefern
						if (IGeoObjectImpl.IsOwnedByBlockRef(go)) go = null;
					}
					if (go != null && constructAction.UseFilter)
					{
						if (!constructAction.Frame.Project.FilterList.Accept(go))
							go = null;
					}
					if (go != null)
					{
						c.Add(go);
					}
				}
				if (MouseOverGeoObjectsEvent != null)
				{
					IGeoObject[] underCursor = (IGeoObject[])c.ToArray(typeof(IGeoObject));
					bool next = MouseOverGeoObjectsEvent(this, underCursor, mouseState == MouseState.ClickUp);
					if (constructAction.AutoCursor)
					{
						if (next) vw.SetCursor(hitCursor);
						else vw.SetCursor(failCursor);
					}
					if (next && (mouseState == MouseState.ClickUp) && !MultipleInput)
					{
						(this as IInputObject).SetFixed(true);
						constructAction.SetNextInputIndex(true);
					}
				}
				constructAction.RefreshDependantProperties();
			}
			void IInputObject.Activate(ActivationMode activationMode)
			{
				switch (activationMode)
				{
					case ActivationMode.Inactivate:
						break;
					case ActivationMode.BySelection:
						break;
					case ActivationMode.ByHotspot:
						break;
				}
			}
			bool IInputObject.AcceptInput(bool acceptOptional)
			{
				return base.AcceptInput(acceptOptional);
			}
			void IInputObject.MouseLeft() { }
			bool IInputObject.HasHotspot
			{
				get
				{
					return false;
				}
			}
			GeoPoint IInputObject.HotspotPosition
			{
				get
				{
					return GeoPoint.Origin;
				}
			}
			Image IInputObject.HotSpotIcon { get { return null; } }
			void IInputObject.OnActionDone()
			{
				if (selectedGeoObject != null) constructAction.feedBack.RemoveSelected(selectedGeoObject);
			}
			IPropertyEntry IInputObject.GetShowProperty()
			{
				return geoObjectProperty;
			}
			bool IInputObject.OnEnter(IPropertyEntry sender, KeyEventArgs args)
			{
				if (MultipleInput)
				{
					(this as IInputObject).SetFixed(true);
					constructAction.SetNextInputIndex(true);
					return true;
				}
				return false;
			}
			bool IInputObject.IsFixed()
			{
				return Fixed;
			}
			protected override void AdjustHighlight()
			{
				if (geoObjectProperty != null) geoObjectProperty.Highlight = !Optional && !Fixed && !ReadOnly;
			}
			void IInputObject.SetFixed(bool isFixed)
			{
				Fixed = isFixed;
			}
			public override void SetError(string errorMessage)
			{
			}
			#endregion
			private void OnSelectedGeoObjectChanged(GeoObjectProperty cp, IGeoObject selectedGeoObject)
			{
				if (GeoObjectSelectionChangedEvent != null) GeoObjectSelectionChangedEvent(this, selectedGeoObject);
				if (this.selectedGeoObject != null) constructAction.feedBack.RemoveSelected(this.selectedGeoObject);
				this.selectedGeoObject = selectedGeoObject;
				if (this.selectedGeoObject != null) constructAction.feedBack.AddSelected(this.selectedGeoObject);
				constructAction.RefreshDependantProperties();
			}
		}
		public class BRepObjectInput : InputObject, IInputObject
		{
			private string hitCursor, failCursor;
			private BRepObjectProperty bRepObjectProperty; // the property grid display
			Dictionary<Vertex, GeoObject.Point> vertexRepresentations = new Dictionary<Vertex, GeoObject.Point>(); // to disply vertices as selected
			/// <summary>
			/// Creates a BRepObjectInput object.
			/// </summary>
			/// <param name="resourceId">the resource id to specify a string from the StringTable.
			/// ResourceId+".Label": the Label left of the
			/// edit box. ResourceId+".ShortInfo": a short tooltip text ResourceId+"DetailedInfo":
			/// a longer tooltip text.
			/// </param>
			public BRepObjectInput(string resourceId)
				: base(resourceId)
			{
				hitCursor = "Hand";
				failCursor = "Arrow";
				MultipleInput = false;
			}
			/// <summary>
			/// Sets the cursor that will be displayed when a curve is hit
			/// </summary>
			public string HitCursor
			{
				set { hitCursor = value; }
			}
			/// <summary>
			/// Sets the cursor that will be displayed when no curve is hit
			/// </summary>
			public string FailCursor
			{
				set { failCursor = value; }
			}
			public bool MultipleInput;
			public Point currentMousePoint;
			private object[] bRepObjects;
			private object selectedBRepObject;
			private IGeoObject BRepObjectToGeoObject(object brep)
			{
				if (selectedBRepObject is Vertex vtx)
				{
					if (!vertexRepresentations.TryGetValue(vtx, out GeoObject.Point actVertexDisplay)) vertexRepresentations[vtx] = actVertexDisplay = GeoObject.Point.MakePoint(vtx.Position);
					return actVertexDisplay;
				}
				if (brep is Face) return brep as IGeoObject;
				if (brep is Edge edge) return edge.Curve3D as IGeoObject;
				if (brep is Line line)
				{
					if (line.UserData.ContainsData("CADability.AxisOf")) return line; // axis are also allowed a brep objects
				}
				return null;
			}
			/// <summary>
			/// Forces the given geoObjects to be displayed
			/// </summary>
			/// <param name="GeoObjects">array of GeoObjects</param>
			/// <param name="selectedGeoObject">currently selected GeoObject</param>
			public void SetBRepObject(object[] objects, object selectedBRepObject)
			{
				if (this.selectedBRepObject != null) constructAction.feedBack.RemoveSelected(BRepObjectToGeoObject(this.selectedBRepObject));
				bRepObjects = objects;
				this.selectedBRepObject = selectedBRepObject;
				if (this.selectedBRepObject != null) constructAction.feedBack.AddSelected(BRepObjectToGeoObject(this.selectedBRepObject));
				if (bRepObjectProperty != null)
				{
					bRepObjectProperty.SetBRepObjects(objects, selectedBRepObject);
				}
			}
			/// <summary>
			/// Set the selected GeoObject
			/// </summary>
			/// <param name="curve">this curve should be selected</param>
			public void SetSelectedBRepObject(object bRepObject)
			{
				bRepObjectProperty.SetSelectedBRepObject(bRepObject);
			}
			/// <summary>
			///  Gets the currently selected brep objects.
			/// </summary>
			/// <returns></returns>
			public object[] GetBRepObjects()
			{
				return bRepObjects;
			}
			/// <summary>
			/// Delegate definition for <see cref="MouseOverGeoObjectsEvent"/>
			/// </summary>
			/// <param name="sender">this object</param>
			/// <param name="TheGeoObjects">GeoObjects under the cursor</param>
			/// <param name="up">mous was clicked</param>
			/// <returns>true, if you accept (one of the) GeoObjects</returns>
			public delegate bool MouseOverBRepObjectsDelegate(BRepObjectInput sender, object[] bRepObjects, bool up);
			/// <summary>
			/// Provide a method here to react on the user moving the cursor over GeoObjects.
			/// </summary>
			public event MouseOverBRepObjectsDelegate MouseOverBRepObjectsEvent;
			/// <summary>
			/// Delegate definition for <see cref="CurveSelectionChangedEvent"/>
			/// </summary>
			/// <param name="sender">this object</param>
			/// <param name="SelectedGeoObject">the user selected GeoObject</param>
			public delegate void BRepObjectSelectionChangedDelegate(BRepObjectInput sender, object SelectedBRepObject);
			/// <summary>
			/// Provide a method here to react on the user selecting a different curve
			/// </summary>
			public event BRepObjectSelectionChangedDelegate BRepObjectSelectionChangedEvent;
			private IInputObject[] forwardMouseInputTo;
			/// <summary>
			/// Mouse input should be forwarded to another input object and only processed
			/// by this input, when the other input object is fixed.
			/// </summary>
			public object ForwardMouseInputTo
			{
				set
				{
					if (value is IInputObject)
					{
						forwardMouseInputTo = new IInputObject[] { value as IInputObject };
					}
					else if (value is object[])
					{
						object[] v = value as object[];
						forwardMouseInputTo = new IInputObject[v.Length];
						for (int i = 0; i < v.Length; ++i)
						{
							forwardMouseInputTo[i] = v[i] as IInputObject;
						}
					}
				}
				get
				{
					return forwardMouseInputTo;
				}
			}
			#region IInputObject implementation
			void IInputObject.Init(ConstructAction a)
			{
				constructAction = a;
			}
			void IInputObject.Refresh()
			{
			}
			IPropertyEntry IInputObject.BuildShowProperty()
			{
				bRepObjectProperty = new BRepObjectProperty(ResourceId, constructAction.Frame);
				bRepObjectProperty.PropertyEntryChangedStateEvent += new PropertyEntryChangedStateDelegate(constructAction.ShowPropertyStateChanged);
				bRepObjectProperty.SelectionChangedEvent += new BRepObjectProperty.SelectionChangedDelegate(OnSelectedBRepObjectChanged);
				if (bRepObjects != null) bRepObjectProperty.SetBRepObjects(bRepObjects, selectedBRepObject);
				if (!Optional && !Fixed)
				{
					bRepObjectProperty.Highlight = true;
				}
				return bRepObjectProperty;
			}
			void IInputObject.OnMouse(MouseEventArgs e, MouseState mouseState, IView vw)
			{
				if (forwardMouseInputTo != null)
				{   // ggf. weiterleiten an einen anderen Input
					for (int i = 0; i < forwardMouseInputTo.Length; ++i)
					{
						if (!forwardMouseInputTo[i].IsFixed())
						{
							forwardMouseInputTo[i].OnMouse(e, mouseState, vw);
							return;
						}
					}
				}
				Point MousePoint = new Point(e.X, e.Y);
				currentMousePoint = MousePoint;
				GeoObjectList l = constructAction.Frame.ActiveView.PickObjects(MousePoint, PickMode.singleFaceAndCurve);
				List<object> bRepObjectsUnderCursor = new List<object>();
				for (int i = 0; i < l.Count; ++i)
				{
					if (l[i] is Face) bRepObjectsUnderCursor.Add(l[i]);
					if (l[i] is ICurve)
					{
						if ((l[i] as IGeoObject).Owner is Edge edge)
						{
							GeoPoint p = constructAction.SnapPoint(e, vw, out SnapPointFinder.DidSnapModes didSnap);
							int cnt = bRepObjectsUnderCursor.Count;
							if (didSnap == SnapPointFinder.DidSnapModes.DidSnapToObjectSnapPoint)
							{   // check, whether this is the start- or endpoint of an edge
								if (Precision.IsEqual(edge.Vertex1.Position, p)) bRepObjectsUnderCursor.Add(edge.Vertex1);
								if (Precision.IsEqual(edge.Vertex2.Position, p)) bRepObjectsUnderCursor.Add(edge.Vertex2);
							}
							if (cnt == bRepObjectsUnderCursor.Count) bRepObjectsUnderCursor.Add(edge); // no vertex added
						}
						if (l[i] is Line line)
						{
							if (line.UserData.ContainsData("CADability.AxisOf")) bRepObjectsUnderCursor.Add(line); // axis are also allowed a brep objects
						}
					}
				}
				if (MouseOverBRepObjectsEvent != null)
				{
					bool next = MouseOverBRepObjectsEvent(this, bRepObjectsUnderCursor.ToArray(), mouseState == MouseState.ClickUp);
					if (constructAction.AutoCursor)
					{
						if (next) vw.SetCursor(hitCursor);
						else vw.SetCursor(failCursor);
					}
					if (next && (mouseState == MouseState.ClickUp) && !MultipleInput)
					{
						(this as IInputObject).SetFixed(true);
						constructAction.SetNextInputIndex(true);
					}
				}
				constructAction.RefreshDependantProperties();
			}
			void IInputObject.Activate(ActivationMode activationMode)
			{
				switch (activationMode)
				{
					case ActivationMode.Inactivate:
						break;
					case ActivationMode.BySelection:
						break;
					case ActivationMode.ByHotspot:
						break;
				}
			}
			bool IInputObject.AcceptInput(bool acceptOptional)
			{
				return base.AcceptInput(acceptOptional);
			}
			void IInputObject.MouseLeft() { }
			bool IInputObject.HasHotspot
			{
				get
				{
					return false;
				}
			}
			GeoPoint IInputObject.HotspotPosition
			{
				get
				{
					return GeoPoint.Origin;
				}
			}
			Image IInputObject.HotSpotIcon { get { return null; } }
			void IInputObject.OnActionDone()
			{
				if (selectedBRepObject != null) constructAction.feedBack.RemoveSelected(BRepObjectToGeoObject(selectedBRepObject));
			}
			IPropertyEntry IInputObject.GetShowProperty()
			{
				return bRepObjectProperty;
			}
			bool IInputObject.OnEnter(IPropertyEntry sender, KeyEventArgs args)
			{
				if (MultipleInput)
				{
					(this as IInputObject).SetFixed(true);
					constructAction.SetNextInputIndex(true);
					return true;
				}
				return false;
			}
			bool IInputObject.IsFixed()
			{
				return Fixed;
			}
			protected override void AdjustHighlight()
			{
				if (bRepObjectProperty != null) bRepObjectProperty.Highlight = !Optional && !Fixed && !ReadOnly;
			}
			void IInputObject.SetFixed(bool isFixed)
			{
				Fixed = isFixed;
			}
			public override void SetError(string errorMessage)
			{
			}
			#endregion
			private void OnSelectedBRepObjectChanged(BRepObjectProperty cp, object selectedGeoObject)
			{
				if (BRepObjectSelectionChangedEvent != null) BRepObjectSelectionChangedEvent(this, selectedGeoObject);
				if (selectedBRepObject != null) constructAction.feedBack.RemoveSelected(BRepObjectToGeoObject(selectedBRepObject));
				selectedBRepObject = selectedGeoObject;
				if (selectedBRepObject != null) constructAction.feedBack.AddSelected(BRepObjectToGeoObject(selectedBRepObject));
				constructAction.RefreshDependantProperties();
			}
		}
		/// <summary>
		/// Defines an input object for an action derived from ConstructAction.
		/// This input object extpects the user to enter a string in a edit field.
		/// Pressing enter or TAB or clicking the mouse proceeds to the next input object. 
		/// </summary>
		public class StringInput : InputObject, IInputObject, ICommandHandler
		{
			private StringProperty stringProperty;
			/// <summary>
			/// Delegate definition for the <see cref="SetStringEvent"/>
			/// </summary>
			/// <param name="val">the entered value</param>
			public delegate void SetStringDelegate(string val);
			/// <summary>
			/// Provide a handler here if you want to be notified about input changes.
			/// </summary>
			public event SetStringDelegate SetStringEvent;
			/// <summary>
			/// Delegate definition for the <see cref="GetStringEvent"/>
			/// </summary>
			/// <returns>the current string</returns>
			public delegate string GetStringDelegate();
			/// <summary>
			/// Provide a method here when the string also changes by other means than user input.
			/// </summary>
			public event GetStringDelegate GetStringEvent;
			/// <summary>
			/// Delegate definiation for <see cref="InputValueChangedEvent"/>
			/// </summary>
			/// <param name="length">New length value</param>
			public delegate void InputValueChangedDelegate(string newValue);
			/// <summary>
			/// Event will be rasied if the Length property changed
			/// </summary>
			public event InputValueChangedDelegate InputValueChangedEvent;
			/// <summary>
			/// Connects this input filed with a <see cref="Text"/> object.
			/// The Text object displays the carret in the view so you can edit the text
			/// in the view directly
			/// </summary>
			/// <param name="text">the text object</param>
			/// <param name="vw">the view, in which the editing is supposed to happen</param>
			public void ConnectWithTextObject(Text text, IView vw)
			{
			}
			/// <summary>
			/// If true, this input can be used as a file open dialog input. In the corresponding context menu
			/// the file open dialog can be opened.
			/// </summary>
			public bool IsFileNameInput;
			/// <summary>
			/// If true there will be an file open dialog when this input becomes visible.
			/// </summary>
			public bool InitOpenFile;
			/// <summary>
			/// Only used when <see cref="IsFileNameInput"/> is true. This  is the filter for the
			/// file open dialog.
			/// </summary>
			public string FileNameFilter;
			public bool NotifyOnLostFocusOnly = false;
			string stringval;
			/// <summary>
			/// Creates a StringInput
			/// </summary>
			/// <param name="resourceId">the resource id to specify a string from the StringTable.
			/// ResourceId+".Label": the Label left of the
			/// edit box. ResourceId+".ShortInfo": a short tooltip text ResourceId+"DetailedInfo":
			/// a longer tooltip text.
			/// </param>
			public StringInput(string resourceId)
				: base(resourceId)
			{
				IsFileNameInput = false;
			}
			public string Content
			{
				get
				{
					return stringval;
				}
				private set
				{
					stringval = value;
					if (stringProperty != null) stringProperty.Refresh();
					InputValueChangedEvent?.Invoke(value);
				}
			}

			public void ForceValue(string val)
			{
				//No InternalSetString implemented
				//StringInput does not support bool SetStringEvent(string) pattern
				if (ReadOnly) return;
				Content = val;
				constructAction.RefreshDependantProperties();
				//StringInput does not support stringProperty.StringChanged()
			}

			private IInputObject[] forwardMouseInputTo;
			/// <summary>
			/// Mouse input should be forwarded to another input object and only processed
			/// by this input, when the other input object is fixed.
			/// </summary>
			public object ForwardMouseInputTo
			{
				set
				{
					if (value is IInputObject)
					{
						forwardMouseInputTo = new IInputObject[] { value as IInputObject };
					}
					else if (value is object[])
					{
						object[] v = value as object[];
						forwardMouseInputTo = new IInputObject[v.Length];
						for (int i = 0; i < v.Length; ++i)
						{
							forwardMouseInputTo[i] = v[i] as IInputObject;
						}
					}
				}
				get
				{
					return forwardMouseInputTo;
				}
			}
			#region IInputObject Members

			void IInputObject.Init(ConstructAction a)
			{
				base.Init(a);
			}

			void IInputObject.Refresh()
			{
				if (GetStringEvent != null)
					Content = GetStringEvent();
			}

			IPropertyEntry IInputObject.BuildShowProperty()
			{
				stringProperty = new StringProperty(Content, ResourceId);
				stringProperty.OnGetValue = () => {
					if (GetStringEvent != null)
						return GetStringEvent();
					return null;
				};
				stringProperty.OnSetValue = (string val) =>
				{
					if (SetStringEvent != null) SetStringEvent(val);
					Fixed = true;
					constructAction.RefreshDependantProperties();
				};
				stringProperty.PropertyEntryChangedStateEvent += new PropertyEntryChangedStateDelegate(OnStateChanged);
				if (IsFileNameInput)
				{
					stringProperty.SetContextMenu("MenuId.OpenFileDialog", this);
				}
				stringProperty.DeferUpdate = NotifyOnLostFocusOnly;
				return stringProperty;
			}
			void IInputObject.OnMouse(MouseEventArgs e, MouseState mouseState, IView vw)
			{
				if (forwardMouseInputTo != null)
				{   // ggf. weiterleiten an einen anderen Input
					for (int i = 0; i < forwardMouseInputTo.Length; ++i)
					{
						if (!forwardMouseInputTo[i].IsFixed())
						{
							forwardMouseInputTo[i].OnMouse(e, mouseState, vw);
							return;
						}
					}
				}
			}
			void IInputObject.Activate(ActivationMode activationMode)
			{
				if (InitOpenFile)
				{
					InitOpenFile = false;
					(this as ICommandHandler).OnCommand("MenuId.OpenFileDialog.Show");
				}
			}

			bool IInputObject.AcceptInput(bool acceptOptional)
			{
				return base.AcceptInput(acceptOptional);
			}

			void IInputObject.MouseLeft()
			{
				// ist das hier von Interesse?
			}

			void IInputObject.OnActionDone()
			{
			}

			IPropertyEntry IInputObject.GetShowProperty()
			{
				return stringProperty;
			}

			bool IInputObject.OnEnter(IPropertyEntry sender, KeyEventArgs args)
			{
				if (sender == stringProperty && !args.Control)
				{
					//if (NotifyOnLostFocusOnly) stringProperty.ForceUpdate();
					(this as IInputObject).SetFixed(true); // als Reaktion auf Enter
					constructAction.SetNextInputIndex(true);
					return true;
				}
				return false;
			}

			bool IInputObject.HasHotspot
			{
				get
				{
					return false;
				}
			}

			GeoPoint IInputObject.HotspotPosition
			{
				get
				{
					return new GeoPoint();
				}
			}

			Image IInputObject.HotSpotIcon
			{
				get
				{
					return null;
				}
			}

			bool IInputObject.IsFixed()
			{
				return Fixed;
			}
			void IInputObject.SetFixed(bool isFixed)
			{
				Fixed = isFixed;
			}
			public override void SetError(string errorMessage)
			{
				stringProperty.ErrorMessage = errorMessage;
			}
			#endregion
			private void OnStateChanged(IPropertyEntry sender, StateChangedArgs args)
			{
				if (constructAction.GetCurrentInputObject() == this)
				{
					if (args.EventState == StateChangedArgs.State.UnSelected)
					{
						constructAction.SetNextInputIndex(true);
					}
				}
			}
			#region ICommandHandler Members

			bool ICommandHandler.OnCommand(string MenuId)
			{
				switch (MenuId)
				{
					case "MenuId.OpenFileDialog.Show":
						int filterIndex = 0;
						if (constructAction.Frame.UIService.ShowOpenFileDlg(ResourceId, StringTable.GetString(ResourceId), FileNameFilter, ref filterIndex, out string fileName) == Substitutes.DialogResult.OK)
						{
							try
							{
								string fn = fileName;
								if (fn != null && fn.Length > 0)
								{
									stringProperty.SetString(fn);
									Fixed = true;
									constructAction.RefreshDependantProperties();
									constructAction.SetNextInputIndex(true);
								}
							}
							catch (Exception e)
							{
								if (e is ThreadAbortException) throw (e);
							}
						}
						return true;
					default:
						return false;
				}
			}

			bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
			{
				switch (MenuId)
				{
					case "MenuId.OpenFileDialog.Show":
						CommandState.Enabled = true;
						return true;
					default:
						return false;
				}
			}
			void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }

			#endregion
		}
		/// <summary>
		/// Defines an input object for an action derived from ConstructAction.
		/// This input object extpects the user to enter a string in a edit field and
		/// is connected to a Text object to provide wysiwyg editing.
		/// Pressing enter or TAB or clicking the mouse proceeds to the next input object. 
		/// </summary>
		public class EditInput : InputObject, IInputObject
		{
			//private TextEditor editor;
			private StringProperty editBox;
			private Text text;
			private bool isfixed;
			private ConstructAction action;

			/// <summary>
			/// Creates a EditInput object
			/// </summary>
			/// <param name="theText">the Text object it is connected with</param>
			public EditInput(Text theText) : base("Text.TextString")
			{
				text = theText;
				isfixed = false;
			}

			public string Value
			{
				get { return text.TextString; }
			}

			public void ForceValue(string val)
			{
				//No InternalSetEdit implemented
				//Editinput does not support string SetEditEvent(string) pattern
				if (ReadOnly) return;
				text.TextString = val;
				//constructAction is always null can not call RefreshDependantProperties();
				//EditInput does not support editProperty.EditChanged()                
			}

			private IInputObject[] forwardMouseInputTo;
			/// <summary>
			/// Mouse input should be forwarded to another input object and only processed
			/// by this input, when the other input object is fixed.
			/// </summary>
			public object ForwardMouseInputTo
			{
				set
				{
					if (value is IInputObject)
					{
						forwardMouseInputTo = new IInputObject[] { value as IInputObject };
					}
					else if (value is object[])
					{
						object[] v = value as object[];
						forwardMouseInputTo = new IInputObject[v.Length];
						for (int i = 0; i < v.Length; ++i)
						{
							forwardMouseInputTo[i] = v[i] as IInputObject;
						}
					}
				}
				get
				{
					return forwardMouseInputTo;
				}
			}
			/// <summary>
			/// Delegate definiation for <see cref="InputValueChangedEvent"/>
			/// </summary>
			/// <param name="length">New length value</param>
			public delegate void InputValueChangedDelegate(string newValue);
			/// <summary>
			/// Event will be rasied if the Length property changed
			/// </summary>
			public event InputValueChangedDelegate InputValueChangedEvent;

			#region IInputObject Members

			void IInputObject.Init(ConstructAction a)
			{
				action = a;
			}

			void IInputObject.Refresh()
			{
				// TODO:  Add EditInput.Refresh implementation
			}

			IPropertyEntry IInputObject.BuildShowProperty()
			{
				if (editBox == null)
				{
					editBox = new StringProperty(text.TextString, ResourceId);
					editBox.PropertyEntryChangedStateEvent += new PropertyEntryChangedStateDelegate(OnEditboxStateChanged);
					editBox.OnSetValue = OnEditBoxStringChanged;
					editBox.OnGetValue = delegate () { return text.TextString; };
					if (!isfixed)
					{
						editBox.Highlight = true;
					}
				}
				return editBox;
			}
			void OnEditBoxStringChanged(string val)
			{
				text.TextString = val;
				(this as IInputObject).SetFixed(true);
				InputValueChangedEvent?.Invoke(val);
			}
			void IInputObject.OnMouse(MouseEventArgs e, MouseState mouseState, IView vw)
			{
				if (forwardMouseInputTo != null)
				{   // ggf. weiterleiten an einen anderen Input
					for (int i = 0; i < forwardMouseInputTo.Length; ++i)
					{
						if (!forwardMouseInputTo[i].IsFixed())
						{
							forwardMouseInputTo[i].OnMouse(e, mouseState, vw);
							return;
						}
					}
				}
			}

			void IInputObject.Activate(ActivationMode activationMode)
			{
				switch (activationMode)
				{
					case ActivationMode.BySelection:
						if (editBox != null)
							OnEditboxStateChanged(editBox, new StateChangedArgs(StateChangedArgs.State.Selected));
						break;
					case ActivationMode.Inactivate:
						OnEditboxStateChanged(editBox, new StateChangedArgs(StateChangedArgs.State.UnSelected));
						break;
				}
			}

			bool IInputObject.AcceptInput(bool acceptOptional)
			{
				return !isfixed;
			}

			void IInputObject.MouseLeft()
			{
				// TODO:  Add EditInput.MouseLeft implementation
			}

			void IInputObject.OnActionDone()
			{
				editBox.PropertyEntryChangedStateEvent -= new PropertyEntryChangedStateDelegate(OnEditboxStateChanged);
				editBox = null;
			}

			IPropertyEntry IInputObject.GetShowProperty()
			{
				return editBox;
			}

			bool IInputObject.OnEnter(IPropertyEntry sender, KeyEventArgs args)
			{
				if (sender == editBox && !args.Control)
				{
					(this as IInputObject).SetFixed(true); // als Reaktion auf Enter
					action.SetNextInputIndex(true);
					return true;
				}
				return false;
			}

			bool IInputObject.HasHotspot
			{
				get
				{
					// TODO:  Add EditInput.HasHotspot getter implementation
					return false;
				}
			}

			GeoPoint IInputObject.HotspotPosition
			{
				get
				{
					// TODO:  Add EditInput.HotspotPosition getter implementation
					return new GeoPoint();
				}
			}

			Image IInputObject.HotSpotIcon
			{
				get
				{
					// TODO:  Add EditInput.HotSpotIcon getter implementation
					return null;
				}
			}
			bool IInputObject.IsFixed()
			{
				return isfixed;
			}
			void IInputObject.SetFixed(bool isFixed)
			{
				isfixed = isFixed;
				editBox.Highlight = !isFixed;
			}
			public override void SetError(string errorMessage)
			{
				editBox.ErrorMessage = errorMessage;
			}
			#endregion
			private void OnEditboxStateChanged(IPropertyEntry sender, StateChangedArgs args)
			{
				switch (args.EventState)
				{
					case StateChangedArgs.State.Selected:
						break;
					case StateChangedArgs.State.UnSelected:
						break;
				}
			}
		}
		/// <summary>
		/// Defines an input object for an action derived from ConstructAction.
		/// This input object extpects the user to enter a integer value in a edit field.
		/// Pressing enter or TAB or clicking the mouse proceeds to the next input object. 
		/// </summary>
		public class IntInput : InputObject, IInputObject
		{
			private IntegerProperty intProperty;
			/// <summary>
			/// Delegate definition of the <see cref="SetIntEvent"/>
			/// </summary>
			/// <param name="val">the user entered value</param>
			public delegate void SetIntDelegate(int val);
			/// <summary>
			/// Provide a handler here to be able to react on input changes
			/// </summary>
			public event SetIntDelegate SetIntEvent;
			/// <summary>
			/// Delegate definition for the <see cref="GetIntEvent"/>
			/// </summary>
			/// <returns></returns>
			public delegate int GetIntDelegate();
			/// <summary>
			/// Provide a handler here when the integer value is modified by other means than
			/// simple user input (e.g. by mouse movement)
			/// </summary>
			public event GetIntDelegate GetIntEvent;
			private int minVal;
			private int maxVal;
			private bool showUpDown;
			/// <summary>
			/// Delegate definition for the <see cref="CalculateIntEvent"/>
			/// </summary>
			/// <param name="MousePosition">current mouse position in model coordinates</param>
			/// <returns></returns>
			public delegate int CalculateIntDelegate(GeoPoint MousePosition);
			/// <summary>
			/// Provide a method here, if you want to calculate the int from a mouse position.
			/// </summary>
			public event CalculateIntDelegate CalculateIntEvent;
			/// <summary>
			/// Sets the limits for the input ad determins whether an up/down control is displayed
			/// </summary>
			/// <param name="min">minimum input value</param>
			/// <param name="max">maximum input value</param>
			/// <param name="showupdown">true: show up/down control</param>
			public void SetMinMax(int min, int max, bool showupdown)
			{
				minVal = min;
				maxVal = max;
				showUpDown = showupdown;
				if (intProperty != null) intProperty.SetMinMax(min, max, showupdown);
			}
			/// <summary>
			/// Delegate definiation for <see cref="InputValueChangedEvent"/>
			/// </summary>
			/// <param name="length">New length value</param>
			public delegate void InputValueChangedDelegate(int intValue);
			/// <summary>
			/// Event will be rasied if the Angle property changed
			/// </summary>
			public event InputValueChangedDelegate InputValueChangedEvent;

			int intval;
			public int IntValue
			{
				get
				{
					return intval;
				}
				set
				{
					if (intval == value)
						return;

					intval = value;
					if (intProperty != null)
						intProperty.Refresh();

					InputValueChangedEvent?.Invoke(value);
				}
			}

			/// <summary>
			/// Forces the input object to the specified value. The input filed is updated accordingly.
			/// </summary>
			/// <param name="val">the value to set</param>
			public void ForceValue(int val)
			{
				//No InternalSetInt implemented
				//IntInput does not support SetIntEvent(int) patttern
				if (ReadOnly) return;
				intval = val;
				constructAction.RefreshDependantProperties();
				intProperty.IntChanged();
			}

			/// <summary>
			/// Creates a IntInput field with no initial value
			/// </summary>
			/// <param name="resourceId">the resource id to specify a string from the StringTable.
			/// ResourceId+".Label": the Label left of the
			/// edit box. ResourceId+".ShortInfo": a short tooltip text ResourceId+"DetailedInfo":
			/// a longer tooltip text.
			/// </param>
			public IntInput(string resourceId)
				: base(resourceId)
			{
				intval = 0;
			}
			/// <summary>
			/// Creates a IntInput field with an initial value
			/// </summary>
			/// <param name="resourceId">the resource id to specify a string from the StringTable.
			/// ResourceId+".Label": the Label left of the
			/// edit box. ResourceId+".ShortInfo": a short tooltip text ResourceId+"DetailedInfo":
			/// a longer tooltip text.
			/// </param>
			/// <param name="StartValue">initial value</param>
			public IntInput(string resourceId, int StartValue)
				: base(resourceId)
			{
				intval = StartValue;
			}
			private IInputObject[] forwardMouseInputTo;
			/// <summary>
			/// Mouse input should be forwarded to another input object and only processed
			/// by this input, when the other input object is fixed.
			/// </summary>
			public object ForwardMouseInputTo
			{
				set
				{
					if (value is IInputObject)
					{
						forwardMouseInputTo = new IInputObject[] { value as IInputObject };
					}
					else if (value is object[])
					{
						object[] v = value as object[];
						forwardMouseInputTo = new IInputObject[v.Length];
						for (int i = 0; i < v.Length; ++i)
						{
							forwardMouseInputTo[i] = v[i] as IInputObject;
						}
					}
				}
				get
				{
					return forwardMouseInputTo;
				}
			}
			#region IInputObject Members
			void IInputObject.Init(ConstructAction a)
			{
				base.Init(a);
			}
			void IInputObject.Refresh()
			{
				if (GetIntEvent != null)
				{
					intval = GetIntEvent();
				}
				if (intProperty != null) intProperty.Refresh();
			}
			IPropertyEntry IInputObject.BuildShowProperty()
			{
				intProperty = new IntegerProperty(intval, ResourceId);
				intProperty.GetIntEvent += new IntegerProperty.GetIntDelegate(OnGetIntInput);
				intProperty.SetIntEvent += new IntegerProperty.SetIntDelegate(OnSetIntInput);
				if (minVal < maxVal) intProperty.SetMinMax(minVal, maxVal, showUpDown);
				if (!Optional && !Fixed)
				{
					intProperty.Highlight = true;
				}
				intProperty.Refresh();
				return intProperty;
			}
			void IInputObject.OnMouse(MouseEventArgs e, MouseState mouseState, IView vw)
			{
				if (forwardMouseInputTo != null)
				{   // ggf. weiterleiten an einen anderen Input
					for (int i = 0; i < forwardMouseInputTo.Length; ++i)
					{
						if (!forwardMouseInputTo[i].IsFixed())
						{
							forwardMouseInputTo[i].OnMouse(e, mouseState, vw);
							return;
						}
					}
				}
				if (CalculateIntEvent != null)
				{
					SnapPointFinder.DidSnapModes DidSnap;
					GeoPoint p = constructAction.SnapPoint(e, vw, out DidSnap);
					if (Fixed) return; // kein Mausinput wird hier akzeptiert
					try
					{
						intval = CalculateIntEvent(p);
						constructAction.RefreshDependantProperties();
						intProperty.Refresh();
						if (mouseState == MouseState.ClickUp)
						{
							(this as IInputObject).SetFixed(true);
							constructAction.SetNextInputIndex(true);
						}
					}
					catch (ActionException)
					{   // das Berechnen der Länge ging schief, meisten weil kein Basepoint gesetzt ist
						// und die deshalb nichts berechnet werden kann.
					}
				}
			}
			void IInputObject.Activate(ActivationMode activationMode)
			{
			}
			bool IInputObject.AcceptInput(bool acceptOptional)
			{
				return base.AcceptInput(acceptOptional);
			}
			void IInputObject.MouseLeft()
			{
				// ist das hier von Interesse?
			}
			void IInputObject.OnActionDone()
			{
			}
			IPropertyEntry IInputObject.GetShowProperty()
			{
				return intProperty;
			}
			bool IInputObject.OnEnter(IPropertyEntry sender, KeyEventArgs args)
			{
				if (sender == intProperty && !args.Control)
				{
					(this as IInputObject).SetFixed(true); // als Reaktion auf Enter
					constructAction.SetNextInputIndex(true);
					return true;
				}
				return false;
			}
			bool IInputObject.HasHotspot
			{
				get
				{
					return false;
				}
			}
			GeoPoint IInputObject.HotspotPosition
			{
				get
				{
					return new GeoPoint();
				}
			}
			Image IInputObject.HotSpotIcon
			{
				get
				{
					return null;
				}
			}
			bool IInputObject.IsFixed()
			{
				return Fixed;
			}
			protected override void AdjustHighlight()
			{
				if (intProperty != null) intProperty.Highlight = !Optional && !Fixed && !ReadOnly;
			}
			void IInputObject.SetFixed(bool isFixed)
			{
				Fixed = isFixed;
			}
			public override void SetError(string errorMessage)
			{
				intProperty.ErrorMessage = errorMessage;
			}
			#endregion

			//			private void OnStateChanged(IShowProperty sender, StateChangedArgs args)
			//			{
			//				if (constructAction.GetCurrentInputObject()==this) 
			//				{
			//					if (args.EventState==StateChangedArgs.State.UnSelected)
			//					{
			//						constructAction.SetNextInputIndex(true);
			//					}
			//				}
			//			}

			private int OnGetIntInput(IntegerProperty sender)
			{
				if (GetIntEvent != null) intval = GetIntEvent();
				return intval;
			}

			private void OnSetIntInput(IntegerProperty sender, int newValue)
			{
				if (constructAction.GetCurrentInputObject() != this)
				{   // wenn man auf die UpDown Felder tippt, dann klappt das mit dem
					// Input setzen nicht
					//					constructAction.SetCurrentInputIndex(this,false);
				}
				intval = newValue;
				(this as IInputObject).SetFixed(true);
				if (SetIntEvent != null) SetIntEvent(newValue);
				constructAction.RefreshDependantProperties();
			}
		}
		#endregion
		private IInputObject[] InputDefinitions; // das sind die Eingabemöglichkeiten der Aktion (IInputObject)
		public List<InputObject> GetInputObjects()
		{
			List<InputObject> listInputObjectsInternal = new List<InputObject>(InputDefinitions.Length);
			foreach (InputObject item in InputDefinitions)
				listInputObjectsInternal.Add(item);

			return listInputObjectsInternal;
		}

		private IPropertyPage propertyTreeView;
		private IPropertyEntry[] SubProperties;
		// Für ShowProperty
		//private ShowPropertyButton okButton; // die Schaltfläche "fertig"
		//private ShowPropertyButton escButton; // die Schaltfläche "abbrechen"
		private GeoPoint lastWorldPoint; // Position der Maus im Weltkoordinatensystem
		private ActionFeedBack feedBack; // sonstiger Krimskrams wie Hilfslinien und so
		private bool autoRepeat; // wenn true, dann wird automatisch wiederholt (falls nicht AutoRepeat() überschrieben wird)
		/// <summary>
		/// The title id (resource id) for the title of the action in the control center
		/// </summary>
		public string TitleId; // der Titel der Aktion (für ShowProperty)
		/// <summary>
		/// The current mouse position for this action
		/// </summary>
		public Point CurrentMousePoint; // dort ist die Maus gerade
		/// <summary>
		/// Display the attributes of the active object. 
		/// </summary>
		public bool ShowAttributes;
		/// <summary>
		/// Gets the container that handles feedback objects (see <see cref="ActionFeedBack"/>).
		/// </summary>
		public new ActionFeedBack FeedBack
		{
			get { return feedBack; }
		}
		/// <summary>
		/// Delegate for <see cref="ActionDoneEvent"/>.
		/// </summary>
		/// <param name="ca">this ConstructAction</param>
		/// <param name="success">true: an object was constructed and added to the model. false: action was aborted or object not fully defined</param>
		public delegate void ActionDoneDelegate(ConstructAction ca, bool success);
		/// <summary>
		/// Event that is fired when this action is done. If your class is derived from ConstructAction you
		/// better override <see cref="OnDone"/>.
		/// </summary>
		public event ActionDoneDelegate ActionDoneEvent;
		private IGeoObject activeObject; // aktives Objekt während der Konstruktion
		private bool showActiveObject;  // aktives Objekt wird dargestellt
		private bool disassembleBlock;  // aktives Objekt wird beim Einfügen in Einzelteile zerlegt
		private GeoPoint basePoint; // Ausgangspunkt für gewisse Fangmethoden
		private bool basePointIsValid; // Ausgangspunkt ist gültig
		private int currentInputIndex; // dieser Input wird gerade von der Maus bearbeitet
		private ModOp ToView // Abbildung Weltkoordinaten -> View
		{
			get
			{
				throw new NotImplementedException();
				// return base.DrwView.DrawingToView*base.DrwView.Drawing.GetWorldToDrawing(base.DrwView.Drawing.ActiveGroup);
			}
		}
		/// <summary>
		/// Initializes some properties. Set the properties you need in your constructor.
		/// </summary>
		protected ConstructAction()
		{
			basePointIsValid = false;
			currentInputIndex = -1;
			ShowAttributes = false;
			feedBack = new ActionFeedBack();
			showActiveObject = true; // Voreinstellung
			disassembleBlock = false;
			autoRepeat = false;
			ViewType = typeof(IActionInputView); // gilt nur für ModelViews
													  // nach dem ersten Input muss auch das Modell festgelegt werden
			UseFilter = false; // must be explicitely set to true, if you want to show the Filter property in this action
		}
		event PropertyEntryChangedStateDelegate IPropertyEntry.PropertyEntryChangedStateEvent
		{
			add
			{
				throw new NotImplementedException();
			}

			remove
			{
				throw new NotImplementedException();
			}
		}
		private void OnRepaintActive(Rectangle Extent, IView View, IPaintTo3D PaintToActive)
		{
			if (activeObject != null && showActiveObject)
			{
				activeObject.PrePaintTo3D(PaintToActive);
				activeObject.PaintTo3D(PaintToActive);
			}
			feedBack.Repaint(Extent, View, PaintToActive);
		}
		private void OnActiveObjectChange(IGeoObject Sender)
		{   // das Gleiche für Will und DidChange
			foreach (IView vw in Frame.AllViews)
			{
				if (vw is IActionInputView)
				{
					try
					{
						(vw as IActionInputView).SetAdditionalExtent(activeObject.GetExtent(0.0));
					}
					catch { } // maybe object not completely constructed
					vw.Invalidate(PaintBuffer.DrawingAspect.Active, vw.DisplayRectangle);
				}
			}
		}
		private void OnActiveObjectWillChange(IGeoObject Sender, GeoObjectChange Change)
		{
			OnActiveObjectChange(Sender);
		}
		private void OnActiveObjectDidChange(IGeoObject Sender, GeoObjectChange Change)
		{
			OnActiveObjectChange(Sender);
			// wenn der Stil geändert wurde, dann als aktiven Stil merken
			if (Change != null && Change.OnlyAttributeChanged && Change.MethodOrPropertyName == "Style")
			{
				IStyle iStyle = Sender as IStyle;
				if (iStyle != null)
				{
					Frame.Project.StyleList.Current = iStyle.Style;
				}
			}
		}
		private void RefreshDependantProperties()
		{
			for (int i = 0; i < InputDefinitions.Length; ++i)
			{
				if (i != currentInputIndex)
				{
					InputDefinitions[i].Refresh(); // dort muss "NeedsRefresh" überprüft werden
				}
			}
		}
		private void RefreshDependantProperties(IInputObject notThis)
		{
			for (int i = 0; i < InputDefinitions.Length; ++i)
			{
				if (InputDefinitions[i] != notThis)
				{
					InputDefinitions[i].Refresh(); // dort muss "NeedsRefresh" überprüft werden
				}
			}
		}
		/// <summary>
		/// Dieses Objekt wird über der Zeichnung dargestellt. Kann auch auf null gesetzt werden
		/// um kein Objekt (mehr) darzustallen. Mehrere Objekte können nur als Block dargestellt 
		/// werden.
		/// </summary>
		public IGeoObject ActiveObject
		{
			get { return activeObject; }
			set
			{
				if (activeObject != null)
				{   // ein neues Objekt wird gesetzt, das alte muß verschwinden
					foreach (IView vw in Frame.AllViews)
					{
						//if (vw.ProjectedModel != null)
						//{
						//    if (vw is ModelView)
						//    {
						//Rectangle viewext = vw.ProjectedModel.GetDeviceExtent(activeObject);
						//viewext.Inflate(1, 1);
						vw.Invalidate(PaintBuffer.DrawingAspect.Active, vw.DisplayRectangle);
						//    }
						//}
					}
					// damit wird neu gezeichnet und das activeObject ist bis dahin umgemapped
					// keine Änderungsmeldungen vom alten Objekt mehr erwünscht
					activeObject.DidChangeEvent -= new ChangeDelegate(OnActiveObjectDidChange);
					activeObject.WillChangeEvent -= new ChangeDelegate(OnActiveObjectWillChange);
				}
				IGeoObject wasActive = activeObject;
				activeObject = value;
				if (activeObject != null)
				{
					// if (ShowAttributes) Frame.Project.SetDefaults(activeObject);
					// hier nicht mit default Attributen versehen, das wird erstmalig bei OnSetAction gemacht
					// und ist hier kontraproduktiv, denn das activeObject hat schon die richtigen Attribute
					// neues aktives Objekt muss mit den Attributeingaben verbunden werden
					if (SubProperties != null)
					{
						foreach (IShowProperty sp in SubProperties)
						{
							if (sp is StyleSelectionProperty)
							{
								if ((sp as StyleSelectionProperty).Connected == wasActive)
								{
									(sp as StyleSelectionProperty).Connected = activeObject;
								}
							}
							if (sp is LayerSelectionProperty)
							{
								if ((sp as LayerSelectionProperty).Connected == wasActive)
								{
									(sp as LayerSelectionProperty).Connected = activeObject;
								}
							}
							if (sp is ColorSelectionProperty)
							{
								if ((sp as ColorSelectionProperty).Connected == wasActive)
								{
									(sp as ColorSelectionProperty).Connected = activeObject;
								}
							}
							if (sp is LineWidthSelectionProperty)
							{
								if ((sp as LineWidthSelectionProperty).Connected == wasActive)
								{
									(sp as LineWidthSelectionProperty).Connected = activeObject;
								}
							}
							if (sp is LinePatternSelectionProperty)
							{
								if ((sp as LinePatternSelectionProperty).Connected == wasActive)
								{
									(sp as LinePatternSelectionProperty).Connected = activeObject;
								}
							}
						}
					}
					activeObject.DidChangeEvent += new ChangeDelegate(OnActiveObjectDidChange);
					activeObject.WillChangeEvent += new ChangeDelegate(OnActiveObjectWillChange);
					foreach (IView vw in Frame.AllViews)
					{
						if (vw is IActionInputView)
						{
							//Rectangle viewext = vw.ProjectedModel.GetDeviceExtent(activeObject);
							//viewext.Inflate(1, 1);
							vw.Invalidate(PaintBuffer.DrawingAspect.Active, vw.DisplayRectangle);
						}
					}
				}
			}
		}
		private static Dictionary<Style.EDefaultFor, Dictionary<Type, string>> lastStyle = new Dictionary<Style.EDefaultFor, Dictionary<Type, string>>(); // hiermit gehts weiter...
		internal static void ClearLastStyles()
		{   // probably not necessary
			lastStyle.Clear();
		}
		/// <summary>
		/// Switches the display of the <see cref="ActiveObject"/> on and off.
		/// </summary>
		public bool ShowActiveObject
		{
			get { return showActiveObject; }
			set
			{
				showActiveObject = value;
				// neuzeichnen auslösen:
				if (activeObject != null) OnActiveObjectChange(activeObject);
			}
		}
		public bool DisassembleBlock
		{
			get
			{
				return disassembleBlock;
			}
			set
			{
				disassembleBlock = value;
			}
		}
		/// <summary>
		/// Tries to find a <see cref="CompoundShape"/> that encloses the point <paramref name="p"/> on the plane <paramref name="plane"/>.
		/// 
		/// </summary>
		/// <param name="p">Point in the inside of the requested shape</param>
		/// <param name="plane">Plane in which the search should happen</param>
		/// <returns>A shape or null</returns>
		public CompoundShape FindShape(GeoPoint p, Plane plane)
		{
			//GeoPoint p = CurrentMousePosition;
			//Plane foundOnPlane = CurrentMouseView.Projection.ProjectionPlane; // bei mehreren Fenstern so nicht gut!!!
			GeoPoint2D onPlane = plane.Project(p);
			// hier müsste man irgendwie erst wenig picken und wenn nix geht dann immer mehr
			BoundingRect pickrect = new BoundingRect(onPlane, WorldViewSize, WorldViewSize);
			//GeoObjectList l = Frame.ActiveView.ProjectedModel.GetObjectsFromRect(pickrect, null);
			GeoObjectList l = Frame.ActiveView.Model.GetObjectsFromRect(pickrect, Frame.ActiveView.Projection, null, PickMode.normal, Frame.Project.FilterList);
			l.DecomposeBlocks();
			//l.Reduce(Frame.Project.FilterList);
			CurveGraph cg = CurveGraph.CrackCurves(l, plane, Precision.eps * 10.0); // gap eine Ordnung größer als Precision
			if (cg != null)
			{
				onPlane = plane.Project(p);
				return cg.CreateCompoundShape(true, onPlane, ConstrHatchInside.HatchMode.excludeHoles, false);
			}
			return null;
		}
		/// <summary>
		/// Sets the <see cref="Style"/>of the provided GeoObject <paramref name="go"/> as the last style. 
		/// If new objects are created with construct method and last style is used, this style will be used.
		/// </summary>
		/// <param name="go">Object to get the style from</param>
		protected virtual void KeepAsLastStyle(IGeoObject go)
		{
			lastStyle[activeObject.PreferredStyle] = AttributeNamesFromGeoObject(go);
		}

		private Dictionary<Type, string> AttributeNamesFromGeoObject(IGeoObject go)
		{
			// wird nur verwendet um bei Konstruktionsaktionen den zuletzt verwendeten Stil zu merken
			// if (go.Style != null && go.Style.Check(go)) return go.Style; // der Stil stimmt
			// die Abfrage war schlecht: der Stil stimmte, die konkrete Ausführung an dem Objekt war aber anders
			Dictionary<Type, string> res = new Dictionary<Type, string>();
			if (go.Layer != null) res[typeof(Layer)] = go.Layer.Name;
			IColorDef icolorDef = go as IColorDef;
			if (icolorDef != null)
			{
				res[typeof(ColorDef)] = icolorDef.ColorDef.Name;
			}
			ILineWidth ilineWidth = go as ILineWidth;
			if (ilineWidth != null)
			{
				res[typeof(LineWidth)] = ilineWidth.LineWidth.Name;
			}
			ILinePattern ilinePattern = go as ILinePattern;
			if (ilinePattern != null)
			{
				res[typeof(LinePattern)] = ilinePattern.LinePattern.Name;
			}
			IHatchStyle ihatchStyle = go as IHatchStyle;
			if (ihatchStyle != null)
			{
				res[typeof(HatchStyle)] = ihatchStyle.HatchStyle.Name;
			}
			IDimensionStyle idimensionStyle = go as IDimensionStyle;
			if (idimensionStyle != null)
			{
				res[typeof(DimensionStyle)] = idimensionStyle.DimensionStyle.Name;
			}
			return res;

		}
		private void SetAttribute(IGeoObject go, Dictionary<Type, string> attributeNames)
		{
			if (attributeNames.TryGetValue(typeof(Layer), out string name) && go is ILayer layer) layer.Layer = Frame.Project.LayerList.Find(name);
			if (attributeNames.TryGetValue(typeof(ColorDef), out name) && go is IColorDef colorDef) colorDef.ColorDef = Frame.Project.ColorList.Find(name);
			if (attributeNames.TryGetValue(typeof(LineWidth), out name) && go is ILineWidth lineWidth) lineWidth.LineWidth = Frame.Project.LineWidthList.Find(name);
			if (attributeNames.TryGetValue(typeof(LinePattern), out name) && go is ILinePattern linePattern) linePattern.LinePattern = Frame.Project.LinePatternList.Find(name);
			if (attributeNames.TryGetValue(typeof(HatchStyle), out name) && go is IHatchStyle hatchStyle) hatchStyle.HatchStyle = Frame.Project.HatchStyleList.Find(name);
			if (attributeNames.TryGetValue(typeof(DimensionStyle), out name) && go is IDimensionStyle dimensionStyle) dimensionStyle.DimensionStyle = Frame.Project.DimensionStyleList.Find(name);
		}

		/// <summary>
		/// Call this method in an override to <see cref="OnSetAction"/> to specify the input
		/// parameters of this action. Objects given as parameters may be any of <see cref="AngleInput"/>,
		/// <see cref="LengthInput"/>, <see cref="DoubleInput"/>, <see cref="GeoPointInput"/>, <see cref="GeoVectorInput"/>, <see cref="BooleanInput"/>,
		/// <see cref="MultiPointInput"/>, <see cref="GeoObjectInput"/>, <see cref="PlaneInput"/>.
		/// </summary>
		/// <param name="TheInput">Objects specifying the input parameters of this action</param>
		public void SetInput(params object[] TheInput)
		{
			InputDefinitions = new IInputObject[TheInput.Length];
			for (int i = 0; i < TheInput.Length; ++i)
			{
				InputDefinitions[i] = TheInput[i] as IInputObject;
			}
		}
		/// <summary>
		/// Sets the BasePoint. This point is needed for some snap modes like the orthogonal snap mode or snap to tangent point. As long as this
		/// point is not set, these snap modes don't work.
		/// </summary>
		public GeoPoint BasePoint
		{
			get { return basePoint; }
			set
			{
				basePoint = value;
				basePointIsValid = true;
			}
		}
		/// <summary>
		/// Clears the <see cref="BasePoint"/>.
		/// </summary>
		public void ClearBasePoint()
		{
			basePointIsValid = false;
		}
		/// <summary>
		/// returns the last mouse position in model coordinates.
		/// </summary>
		public new GeoPoint CurrentMousePosition
		{
			get { return lastWorldPoint; }
		}
		/// <summary>
		/// Sets the input focus to the given input object. The input object must be one of those
		/// given in the <see cref="SetInput"/> method.
		/// </summary>
		/// <param name="InputObject">Input object which should become active</param>
		/// <param name="ActivateMouse">Mouse actions also apply to this input object</param>
		public void SetFocus(object InputObject, bool ActivateMouse)
		{
			IInputObject io = InputObject as IInputObject;
			if (io != null) SetCurrentInputIndex(io, ActivateMouse);
		}
		#region von der Anwenderklasse zu überschreibende Methoden
		/// <summary>
		/// Called, when the ConstructAction is done, i.e. all non-optional inputs
		/// have been fixed. The default implementation adds the active object (if it exists)
		/// to the active model and removes this action. Override this method if you need
		/// a different behaviour.
		/// </summary>
		public virtual void OnDone()
		{
			for (int i = 0; i < InputDefinitions.Length; ++i) // nach vorne geholt wg. Undo bei Polyline
			{
				InputDefinitions[i].OnActionDone();
			}
			if (activeObject != null && showActiveObject) // showActiveObject hinzugefügt, ist das OK?
			{
				if (disassembleBlock && activeObject is Block)
				{
					Block blk = activeObject as Block;
					if (CurrentMouseView == null)
					{   // kommt vor, wenn die Maus noch nicht über einem Fenster war
						// wenn man z.B. alles mit der Tastatur definiert ohne dass die Maus aufs Fenster kommt
						Frame.Project.GetActiveModel().Add(blk.Clear());
					}
					else
					{
						CurrentMouseView.Model.Add(blk.Clear());
					}
				}
				else
				{
					KeepAsLastStyle(activeObject);
					if (CurrentMouseView == null)
					{   // kommt vor, wenn die Maus noch nicht über einem Fenster war
						// wenn man z.B. alles mit der Tastatur definiert ohne dass die Maus aufs Fenster kommt
						Frame.Project.GetActiveModel().Add(activeObject);
					}
					else
					{
						if (Frame.ActiveView.Model != null) Frame.ActiveView.Model.Add(activeObject);
						else CurrentMouseView.Model.Add(activeObject);
					}
				}
			}
			try
			{
				autoRepeat = Frame.GetBooleanSetting("Action.RepeatConstruct", false);
			}
			catch (Exception e)
			{
				if (e is ThreadAbortException) throw (e);
			}
			RemoveThisAction();
			foreach (IView vw in Frame.AllViews)
			{
				if (vw is IActionInputView)
				{
					(vw as IActionInputView).SetAdditionalExtent(BoundingCube.EmptyBoundingCube);
				}
			}

			// der ActionDoneEvent wird erst gefeurt nachdem die Aktion vom Stack ist, damit die darunterliegende
			// schon wieder aktiviert ist, wenn sie den Event empfängt und damit auch ordentlich
			// beenden kann
			if (ActionDoneEvent != null)
			{
				ActionDoneEvent(this, true);
			}
		}
		/// <summary>
		/// The ConstructAction may have several solutions. If set to true, the PgUp/PgDown
		/// key will call OnDifferentSolution and the context menue will have appropriate entries.
		/// </summary>
		public bool MultiSolution;
		/// <summary>
		/// TheConstruction may have several solutions. If set to a value !=0 the ContextMenu will have
		/// that number of entries and OnSolution() will be called if the user selects one of these
		/// </summary>
		public int MultiSolutionCount;
		/// <summary>
		/// Will be called when MultiSolution is true and the user presses the PgUp/PgDown key
		/// or selects the appropriate entries of the context menu. Override this
		/// method to implement a multi-solution construction. Default implementation
		/// is empty.
		/// </summary>
		/// <param name="next">true: forward, false: backward</param>
		public virtual void OnDifferentSolution(bool next) { }
		/// <summary>
		/// Will be called when MultiSolution is true and the user presses the PgUp/PgDown key
		/// or selects the appropriate entries of the context menu. Override this
		/// method to implement a multi-solution construction. Default implementation
		/// is empty.
		/// </summary>
		/// <param name="solutionNumber">the 0-based number of solution</param>
		public virtual void OnSolution(int solutionNumber) { }
		#endregion
		private void OnMouse(MouseEventArgs e, MouseState mouseState, IView vw)
		{
			if (currentInputIndex >= 0)
			{
				lastWorldPoint = WorldPoint(e, vw);
				InputDefinitions[currentInputIndex].OnMouse(e, mouseState, vw);
			}
		}
		protected object CurrentInput
		{
			get
			{
				if (currentInputIndex >= 0)
				{
					return InputDefinitions[currentInputIndex];
				}
				return null;
			}
		}
		private IInputObject CheckHotSpots(MouseEventArgs e, IView vw)
		{
			for (int i = 0; i < InputDefinitions.Length; ++i)
			{
				if (InputDefinitions[i].HasHotspot)
				{
					GeoPoint p = InputDefinitions[i].HotspotPosition;
					PointF pf = vw.Projection.ProjectF(p);
					if (Math.Abs(e.X - pf.X) < 4 && Math.Abs(e.Y - pf.Y) < 4)
					{
						return InputDefinitions[i];
					}
				}
			}
			return null;
		}
		/// <summary>
		/// Detremins whether this action supports autorepeat. Autorepeat is enabled
		/// in the <see cref="Settings.GlobalSettings"/>.
		/// </summary>
		/// <returns></returns>
		public override bool AutoRepeat()
		{
			return autoRepeat;
		}
		/// <summary>
		/// Implements <see cref="Action.OnMouseMove"/>. If you override this method
		/// don't forget to call the bas implementation.
		/// </summary>
		/// <param name="e"><paramref name="Action.OnMouseMove.e"/></param>
		/// <param name="vw"><paramref name="Action.OnMouseMove.vw"/></param>
		public override void OnMouseMove(MouseEventArgs e, IView vw)
		{
			CurrentMousePoint = new Point(e.X, e.Y);
			if ((e.Button == MouseButtons.None) && (CheckHotSpots(e, vw) != null))
			{
				string hspcur = "HandArrow";
				vw.SetCursor(hspcur);
			}
			else
			{
				MouseState mouseState;
				if (e.Button == MouseButtons.Left) mouseState = MouseState.MoveDown;
				else mouseState = MouseState.MoveUp;
				OnMouse(e, mouseState, vw);
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
			CurrentMousePoint = new Point(e.X, e.Y);
			if (e.Button == MouseButtons.Right)
			{
				bool useContextMenue = true;
				if (vw is IActionInputView && !(vw as IActionInputView).AllowContextMenu) useContextMenue = false;
				IInputObject io = GetCurrentInputObject();
				if (io != null)
				{
					IPropertyEntry sp = io.GetShowProperty();
					if (io is MultiPointInput)
					{
						sp = (io as MultiPointInput).GetActiveEntry();
					}
					if (sp != null)
					{
						if (io is MultiPointInput)
						{
							(io as MultiPointInput).contextMenuIsActive = true;
						}
						if (sp.Flags.HasFlag(PropertyEntryType.ContextMenu))
						{
							MenuWithHandler[] cm = sp.ContextMenu;
							List<MenuWithHandler> ConstructRightButton = new List<MenuWithHandler>(MenuResource.LoadMenuDefinition("MenuId.ConstructRightButton", false, this));
							for (int i = 0; i < MultiSolutionCount; ++i)
							{   // für jede Lösung einen Eintrag
								string menutext = StringTable.GetFormattedString("Construct.Solution", i + 1);
								string menuid = "Construct.Solution." + i.ToString();
								MenuWithHandler menuitem = new MenuWithHandler();
								menuitem.ID = menuid;
								menuitem.Text = menutext;
								menuitem.Target = this;
								menuitem.OnSelected = OnMenuItemSelected;
								ConstructRightButton.Add(menuitem);
							}
							ConstructRightButton.AddRange(cm);
							if (useContextMenue) vw.Canvas.ShowContextMenu(ConstructRightButton.ToArray(), e.Location);
						}
						else
						{
							// need to implement?
							List<MenuWithHandler> ConstructRightButton = new List<MenuWithHandler>(MenuResource.LoadMenuDefinition("MenuId.ConstructRightButton", false, this));
							for (int i = 0; i < MultiSolutionCount; ++i)
							{   // für jede Lösung einen Eintrag
								string menutext = StringTable.GetFormattedString("Construct.Solution", i + 1);
								string menuid = "Construct.Solution." + i.ToString();
								MenuWithHandler mh = new MenuWithHandler(menuid);
								mh.Text = menutext;
								mh.Target = this;
								mh.OnSelected = OnMenuItemSelected; // also sets the target to this
								ConstructRightButton.Add(mh);
							}
							if (useContextMenue) vw.Canvas.ShowContextMenu(ConstructRightButton.ToArray(), e.Location);
						}
					}
				}
			}
			else if (e.Button == MouseButtons.Left)
			{
				OnMouse(e, MouseState.ClickUp, vw);
			}
		}

		private void OnMenuItemSelected(MenuWithHandler mh, bool selected)
		{
			if (selected && mh.ID.StartsWith("Construct.Solution."))
			{
				string sub = mh.ID.Substring("Construct.Solution.".Length);
				int ind = int.Parse(sub);
				OnSolution(ind);
			}
		}

		// need to implement:
		//void OnMenuItemSelected(object sender, EventArgs e)
		//{
		//    MenuItemWithID mid = sender as MenuItemWithID;
		//    if (mid != null)
		//    {
		//        if (mid.ID.StartsWith("Construct.Solution."))
		//        {
		//            string sub = mid.ID.Substring("Construct.Solution.".Length);
		//            int ind = int.Parse(sub);
		//            OnSolution(ind);
		//        }
		//    }
		//}
		protected virtual bool MayFinish()
		{
			for (int i = 0; i < InputDefinitions.Length; ++i)
			{
				if (i != currentInputIndex && InputDefinitions[i].AcceptInput(false))
				{
					return false; // es sind noch andere Inputs offen außer dem gerade aktiven
				}
			}
			if (activeObject != null)
			{
				return activeObject.HasValidData();
			}
			return false;
		}
		/// <summary>
		/// Implements <see cref="Action.OnMouseDown"/>. If you override this method
		/// don't forget to call the bas implementation.
		/// </summary>
		/// <param name="e"><paramref name="Action.OnMouseDown.e"/></param>
		/// <param name="vw"><paramref name="Action.OnMouseDown.vw"/></param>
		public override void OnMouseDown(MouseEventArgs e, IView vw)
		{
			CurrentMousePoint = new Point(e.X, e.Y);
			if ((e.Button == MouseButtons.Left))
			{
				IInputObject io = CheckHotSpots(e, vw);
				if (io != null)
				{
					string hspcur = "HandArrow";
					vw.SetCursor(hspcur);
					io.Activate(ActivationMode.ByHotspot);
					io.GetShowProperty().Select();
					OnMouse(e, MouseState.ClickDown, vw);
				}
				else
				{
					OnMouse(e, MouseState.ClickDown, vw);
				}
			}
		}
		/// <summary>
		/// Overrides <see cref="Action.OnMouseLeave"/>. Usually you don't override this
		/// method, but handle the appropriate events of the input objects.
		/// </summary>
		/// <param name="e"></param>
		/// <param name="vw"></param>
		public override void OnMouseLeave(EventArgs e, IView vw)
		{
			if (currentInputIndex >= 0)
			{
				InputDefinitions[currentInputIndex].MouseLeft();
			}
		}
		/// <summary>
		/// Overrides <see cref="Action.OnEscape"/>. Usually you don't override this
		/// method, but handle the appropriate events of the input objects.
		/// </summary>
		public override bool OnEscape(object sender)
		{
			if (ActionDoneEvent != null)
			{
				ActionDoneEvent(this, false);
			}
			RemoveThisAction();
			return true;
		}
		/// <summary>
		/// Overrides <see cref="CADability.Actions.Action.OnEscape ()"/>
		/// </summary>
		public override bool OnEscape()
		{
			return OnEscape(null);
		}
		/// <summary>
		/// Overrides <see cref="Action.OnEnter"/>. Usually you don't override this
		/// method, but handle the appropriate events of the input objects.
		/// </summary>
		public override bool OnEnter(object sender)
		{
			IInputObject io = GetCurrentInputObject();
			if (io != null)
			{
				KeyEventArgs kea;
				if (sender is Keys)
				{
					kea = new KeyEventArgs((Keys)sender);
				}
				else
				{
					// kommt vom Button im Controlcenter
					kea = new KeyEventArgs(Keys.Enter | Keys.Shift);
				}
				if ((Frame.UIService.ModifierKeys & Keys.Control) == 0)
				{   // nur wenn nicht Control, dann weitergeben
					if (io.OnEnter(io.GetShowProperty(), kea))
					{   // versuchsweise Ctrl+Enter nicht weiterleiten, da es den TreeView aufklappt
						return true;
					}
				}
			}
			if ((Frame.UIService.ModifierKeys & Keys.Control) == 0)
			{
				OnDone();
				return true;
			}
			else
			{
				int ci = currentInputIndex;

				if (InputDefinitions[ci] != null && InputDefinitions[ci].GetShowProperty() != null && InputDefinitions[ci].GetShowProperty().Flags.HasFlag(PropertyEntryType.HasSubEntries))
				{
					propertyTreeView.OpenSubEntries(InputDefinitions[ci].GetShowProperty(), !InputDefinitions[ci].GetShowProperty().IsOpen);
					return true;
				}
				return false;
			}
		}
		/// <summary>
		/// Overrides <see cref="CADability.Actions.Action.OnEnter ()"/>
		/// </summary>
		public override bool OnEnter()
		{
			return OnEnter(null);
		}
		/// <summary>
		/// The tab key was pressed: select the next input field, also accept optional fields
		/// </summary>
		/// <param name="sender"></param>
		/// <returns></returns>
		public override bool OnTab(object sender)
		{
			int ci = currentInputIndex;
			if (ci >= 0)
			{
				if (InputDefinitions[ci] != null && InputDefinitions[ci].GetShowProperty() != null && InputDefinitions[ci].GetShowProperty().IsOpen) return false;
				SetNextInputIndex(true, true);
			}
			return ci != currentInputIndex; // true (handled), if the input field changed to the next input
		}

		#region Implementierung von IShowProperty
		#endregion
		private IInputObject GetCurrentInputObject()
		{
			if (currentInputIndex >= 0) return InputDefinitions[currentInputIndex];
			else return null;
		}
		private IInputObject FindInputObject(IPropertyEntry sp)
		{
			for (int i = 0; i < InputDefinitions.Length; ++i)
			{
				IPropertyEntry ip = InputDefinitions[i].GetShowProperty();
				if (ip == sp)
				{
					return InputDefinitions[i];
				}
				if (ip.SubItems != null)
				{
					for (int j = 0; j < ip.SubItems.Length; ++j)
					{
						if (sp == ip.SubItems[j])
						{
							return InputDefinitions[i];
						}
					}
				}
			}
			return null;
		}
		internal virtual void InputChanged(object activeInput) { }
		private void SetCurrentInputIndex(IInputObject io, bool ActivateMouse)
		{
			for (int i = 0; i < InputDefinitions.Length; ++i)
			{
				if (InputDefinitions[i] == io)
				{
					SetCurrentInputIndex(i, ActivateMouse);
					break;
				}
			}
		}
		private void SetCurrentInputIndex(int Index, bool ActivateMouse)
		{
			// diese Abfrage ist wichtig, sonst gibt es eine Endlosschleife
			if (currentInputIndex == Index) return;
			currentInputIndex = Index;
			for (int i = 0; i < InputDefinitions.Length; ++i)
			{
				if (i == currentInputIndex)
				{
					InputDefinitions[i].Activate(ActivationMode.BySelection);
					InputChanged(InputDefinitions[i]);
					if (propertyTreeView != null) propertyTreeView.SelectEntry(InputDefinitions[i].GetShowProperty());
				}
				else
				{
					InputDefinitions[i].Activate(ActivationMode.Inactivate);
				}
			}
			if (Index == -1) InputChanged(null);
		}
		private void SetNextInputIndex(bool ActivateMouse, bool acceptOptional = false)
		{
			for (int i = currentInputIndex + 1; i < InputDefinitions.Length; ++i)
			{
				if (InputDefinitions[i].AcceptInput(acceptOptional))
				{
					SetCurrentInputIndex(i, ActivateMouse);
					return;
				}
			}
			// wenn nur noch "currentInputIndex" übrigbleibt, so wird dieser wieder gesetzt
			// wenn es z.B. nur noch ein Feld gibt, und der Anwender mit TAB weitergehen will,
			// dann soll nicht beendet werden.
			for (int i = 0; i <= currentInputIndex; ++i)
			{
				if (InputDefinitions[i].AcceptInput(acceptOptional))
				{
					SetCurrentInputIndex(i, ActivateMouse);
					return;
				}
			}
			SetCurrentInputIndex(-1, false);
			Frame.Project.Undo.ClearContext(); // the clear context call is necessary here to end a continuous change before updates to the model are made
			OnDone();
		}
		private void ShowPropertyStateChanged(IPropertyEntry sender, StateChangedArgs args)
		{
			if (args.EventState == StateChangedArgs.State.Selected)
			{
				IInputObject io = FindInputObject(sender);
				// ReadOnly wurde früher abgecheckt, wie komme ich hier dran?
				if (io != null) SetCurrentInputIndex(io, true);
			}
		}
		/// <summary>
		/// Implements <see cref="Action.OnSetAction"/>. If you override this method
		/// don't forget to call the base implementation.
		/// </summary>
		public override void OnSetAction()
		{   // hier müssen alle InputDefinitions fertig sein!
			feedBack.Frame = Frame;
			if (InputDefinitions == null || InputDefinitions.Length <= 0) throw new ConstructActionException("ConstructAction: no input fields defined, call 'SetInput'");
			if (activeObject != null && ShowAttributes)
			{
				Frame.Project.SetDefaults(activeObject); // wird möglicherweise gleich überschrieben, aber damit ist wenigstens sicher gesetzt
														 // das lastStyle Konzept ist schlecht, der lastStyle sollte nur dann
														 // gesetzt werden, wenn der Anwender aktiv einen Stil auswählt, sonst wird es zu verwirrend
														 // insbesondere wenn man mit Programmtext den Stil und die Farben setzt kommt man nicht durch
				if (Settings.GlobalSettings.GetBoolValue("Action.KeepStyle", false))
				{
					if (lastStyle.TryGetValue(activeObject.PreferredStyle, out Dictionary<Type, string> attributeNames))
					{
						SetAttribute(activeObject, attributeNames);
					}
				}
			}
			ArrayList allProperties = new ArrayList();
			for (int i = 0; i < InputDefinitions.Length; ++i)
			{
				InputDefinitions[i].Init(this);
				IPropertyEntry isp = InputDefinitions[i].BuildShowProperty();
				if (isp != null) allProperties.Add(isp);
			}
			if (activeObject != null && ShowAttributes)
			{
				allProperties.AddRange(activeObject.GetAttributeProperties(Frame));
			}
			if (UseFilter)
			{
				allProperties.Add(Frame.Project.FilterList);
			}
			SubProperties = (IPropertyEntry[])allProperties.ToArray(typeof(IPropertyEntry));
			base.OnSetAction();
			for (int i = 0; i < InputDefinitions.Length; ++i)
			{
				if (InputDefinitions[i].AcceptInput(false))
				{
					SetCurrentInputIndex(i, true);
					break;
				}
			}
			// da OnActivate noch nicht dran war
			foreach (IView vw in Frame.AllViews)
			{
				if (vw is IActionInputView)
				{
					// vw.SetPaintHandler(PaintBuffer.DrawingAspect.Active, new RepaintView(OnRepaintActive));
					vw.SetPaintHandler(PaintBuffer.DrawingAspect.Active, new PaintView(OnRepaintActive));
					if (vw is ModelView mv) mv.DisplayChangedEvent += ProjectionChanged;
				}
			}
		}

		private void ProjectionChanged(object sender, DisplayChangeArg displayChangeArg)
		{
			OnDisplayChanged(displayChangeArg);
		}

		/// <summary>
		/// Can be overridden to react on zooming and scrolling
		/// </summary>
		/// <param name="d"></param>
		public override void OnDisplayChanged(DisplayChangeArg d)
		{
			base.OnDisplayChanged(d);
		}

		/// <summary>
		/// Implements <see cref="Action.OnActivate"/>. If you override this method
		/// don't forget to call the base implementation.
		/// </summary>
		public override void OnActivate(Action OldActiveAction, bool SettingAction)
		{
			IPropertyPage pd = Frame.ControlCenter.GetPropertyPage("Action");
			pd.Add(this, true);
			if (Frame.GetBooleanSetting("Action.PopProperties", true))
			{
				Frame.SetControlCenterFocus("Action", null, false, false);
			}
			base.OnActivate(OldActiveAction, SettingAction);
			if (OldActiveAction is IIntermediateConstruction)
			{
				IIntermediateConstruction ic = OldActiveAction as IIntermediateConstruction;
				pd.MakeVisible(ic.GetHandledProperty());
				IInputObject io = FindInputObject(ic.GetHandledProperty());
				if (io is MultiPointInput)
				{
					pd.SelectEntry(io.GetShowProperty());
					(io as MultiPointInput).contextMenuIsActive = false;
					(io as MultiPointInput).FixActivePoint();
				}
				else if (io != null)
				{
					pd.SelectEntry(ic.GetHandledProperty());
					io.SetFixed(ic.Succeeded);
					SetNextInputIndex(ic.Succeeded);
				}
			}
			else
			{
				IInputObject io = GetCurrentInputObject();
				if (io != null)
				{
					io.Activate(ActivationMode.BySelection); // damit der Fokus gesetzt wird (ActivateMouse ??)
					if (propertyTreeView != null) propertyTreeView.SelectEntry(io.GetShowProperty());
				}
			}
			// das mehrfache Einfügen macht nichts
			foreach (IView vw in Frame.AllViews)
			{
				if (vw is IActionInputView)
				{
					//vw.SetPaintHandler(PaintBuffer.DrawingAspect.Select,new RepaintView(OnRepaintHotspots));
					vw.SetPaintHandler(PaintBuffer.DrawingAspect.Select, new PaintView(OnRepaintHotspots));
				}
			}
			if (Frame.GetBooleanSetting("Action.AlwaysOpenPointInputs", false))
			{
				IPropertyEntry[] se = SubProperties;
				for (int i = 0; i < se.Length; i++)
				{
					if (se[i] is GeoPointProperty)
					{
						pd.OpenSubEntries(se[i], true);
					}
				}
			}
		}
		/// <summary>
		/// Implements <see cref="Action.OnInactivate"/>. If you override this method
		/// don't forget to call the bas implementation.
		/// </summary>
		/// <param name="NewActiveAction"><paramref name="Action.OnInactivate.NewActiveAction"/></param>
		/// <param name="RemovingAction"><paramref name="Action.OnInactivate.RemovingAction"/></param>
		public override void OnInactivate(Action NewActiveAction, bool RemovingAction)
		{
			if (!RemovingAction)
			{
				if (NewActiveAction is IIntermediateConstruction)
				{
					Frame.GetPropertyDisplay("Action").Remove(this);
					base.OnInactivate(NewActiveAction, RemovingAction);
				}
				else
				{
					RemoveThisAction(); // das führt zu einem erneuten Aufruf von OnInactivate
				}
			}
			else
			{
				Frame.GetPropertyDisplay("Action").Remove(this);
				base.OnInactivate(NewActiveAction, RemovingAction);
			}
			foreach (IView vw in Frame.AllViews)
			{
				if (vw is IActionInputView)
				{
					vw.Invalidate(PaintBuffer.DrawingAspect.Select, vw.DisplayRectangle);
					vw.RemovePaintHandler(PaintBuffer.DrawingAspect.Select, new PaintView(OnRepaintHotspots));
				}
			}
		}
		/// <summary>
		/// Implements <see cref="Action.OnRemoveAction"/>. If you override this method
		/// don't forget to call the bas implementation.
		/// </summary>
		public override void OnRemoveAction()
		{
			if (backgroundTask != null) backgroundTask.Abort();
			ActiveObject = null; // damit alles abgemeldet wird
								 // die InputDefinitions enthalten möglicherweise Referenzen auf DefaultValues,
								 // welche Ihrerseits die letzte Aktion kennnen. Diese sind global und sollen hier
								 // die Finger von der Aktion nehmen.
			for (int i = 0; i < InputDefinitions.Length; ++i)
			{
				InputDefinitions[i].Activate(ActivationMode.Inactivate);
			}
			feedBack.ClearAll();
			foreach (IView vw in Frame.AllViews)
			{
				if (vw is IActionInputView)
				{
					//vw.RemovePaintHandler(PaintBuffer.DrawingAspect.Active, new RepaintView(OnRepaintActive));
					vw.RemovePaintHandler(PaintBuffer.DrawingAspect.Active, new PaintView(OnRepaintActive));
					if (vw is ModelView mv) mv.DisplayChangedEvent -= ProjectionChanged;
				}
			}
			base.OnRemoveAction();
		}
		/// <summary>
		/// Implements <see cref="Action.OnViewsChanged"/>. This implementation
		/// aborts the action.
		/// </summary>
		public override void OnViewsChanged()
		{
			RemoveThisAction(); // das machen wir nicht mit
		}
		/// <summary>
		/// Additional posibility for the derived action to specify a tangential point
		/// besides the tangential point from the base point
		/// </summary>
		/// <param name="e"></param>
		/// <param name="vw"></param>
		/// <param name="found"></param>
		/// <returns></returns>
		protected virtual bool FindTangentialPoint(MouseEventArgs e, IView vw, out GeoPoint found)
		{
			found = GeoPoint.Origin;
			return false;
		}
		/// <summary>
		/// Gets a point from the MouseEventArgs and the view. The point will be in the appropriate
		/// model and will be snapped according to the views snap setting.
		/// </summary>
		/// <param name="e">MouseEventArgs with the mouse position</param>
		/// <param name="vw">the view</param>
		/// <returns>point in model space</returns>
		public virtual new GeoPoint SnapPoint(MouseEventArgs e, IView vw, out SnapPointFinder.DidSnapModes DidSnap)
		{
			GeoPoint res;
			if (basePointIsValid) res = base.SnapPoint(e, basePoint, vw, out DidSnap);
			else res = base.SnapPoint(e, vw, out DidSnap);
			GeoPoint tan;
			if (((Frame.SnapMode & SnapPointFinder.SnapModes.SnapToTangentPoint) != 0) && FindTangentialPoint(e, vw, out tan))
			{
				if (DidSnap == SnapPointFinder.DidSnapModes.DidNotSnap || (res | WorldPoint(e.Location)) > (tan | WorldPoint(e.Location)))
				{
					DidSnap = SnapPointFinder.DidSnapModes.DidSnapToTangentPoint;
					SetCursor(DidSnap, vw);
					return tan;
				}
			}
			return res;
		}
		public virtual new GeoPoint SnapPoint(MouseEventArgs e, GeoPoint basePoint, IView vw, out SnapPointFinder.DidSnapModes DidSnap)
		{
			GeoPoint res;
			res = base.SnapPoint(e, basePoint, vw, out DidSnap);
			GeoPoint tan;
			if (((Frame.SnapMode & SnapPointFinder.SnapModes.SnapToTangentPoint) != 0) && FindTangentialPoint(e, vw, out tan))
			{
				if (DidSnap == SnapPointFinder.DidSnapModes.DidNotSnap || (res | WorldPoint(e.Location)) > (tan | WorldPoint(e.Location)))
				{
					DidSnap = SnapPointFinder.DidSnapModes.DidSnapToTangentPoint;
					SetCursor(DidSnap, vw);
					return tan;
				}
			}
			return res;
		}
		/// <summary>
		/// Returns the GeoObject which was involved in the last snap operation. May be null;
		/// </summary>
		public IGeoObject LastSnapObject
		{
			get
			{
				return CurrentMouseView.LastSnapObject;
			}
		}
		/// <summary>
		/// Returns the last snap mode, i.e. the reason why the snap took place. E.g. did snap to an endpoint or did snap to 
		/// the surface of a face.
		/// </summary>
		public SnapPointFinder.DidSnapModes LastSnapMode
		{
			get
			{
				return CurrentMouseView.LastSnapMode;
			}
		}
		/// <summary>
		/// Handles a few context menu commands. Don't forget to call the base implementation
		/// if you override this method. See <see cref="ICommandHandler"/>.
		/// </summary>
		/// <param name="MenuId"></param>
		/// <returns></returns>
		public override bool OnCommand(string MenuId)
		{
			switch (MenuId)
			{
				case "MenuId.MultiSolution.Next":
					OnDifferentSolution(true);
					RefreshDependantProperties();
					return true;
				case "MenuId.MultiSolution.Previous":
					OnDifferentSolution(false);
					RefreshDependantProperties();
					return true;
				case "MenuId.Construct.Finish":
					OnEnter();
					return true;
				case "MenuId.Construct.Abort":
					OnEscape();
					return true;
			}
			if (MenuId.StartsWith("Construct.Solution."))
			{
				string sub = MenuId.Substring("Construct.Solution.".Length);
				int ind = int.Parse(sub);
				OnSolution(ind);
				// OnEnter();
				return true;
			}
			return base.OnCommand(MenuId);
		}
		/// <summary>
		/// Overrides <see cref="CADability.Actions.Action.OnUpdateCommand (string, CommandState)"/>
		/// </summary>
		/// <param name="MenuId"></param>
		/// <param name="CommandState"></param>
		/// <returns></returns>
		public override bool OnUpdateCommand(string MenuId, CommandState CommandState)
		{
			switch (MenuId)
			{
				case "MenuId.Construct.Finish":
					return true;
				case "MenuId.Construct.Abort":
					return true;
			}
			return base.OnUpdateCommand(MenuId, CommandState);
		}
		private void OnRepaintHotspots(Rectangle IsInvalid, IView View, IPaintTo3D paintTo3D)
		{
			Color bckgnd = Frame.GetColorSetting("Colors.Background", Color.AliceBlue);
			Color infocolor;
			if (bckgnd.GetBrightness() > 0.5) infocolor = Color.Black;
			else infocolor = Color.White;
			for (int i = 0; i < InputDefinitions.Length; ++i)
			{
				if (InputDefinitions[i].HasHotspot)
				{
					GeoPoint p = InputDefinitions[i].HotspotPosition;
					PointF pf = View.Projection.ProjectF(p);
					Image hsi = InputDefinitions[i].HotSpotIcon;
					if (hsi != null)
					{
						paintTo3D.SetColor(infocolor);
						paintTo3D.PrepareIcon(hsi as Bitmap); // das ist ja nicht in einer Displayliste
						paintTo3D.DisplayIcon(p, hsi as Bitmap);
					}
					else
					{
						PaintTo3D.PaintHandle(paintTo3D, pf, 3, infocolor);
					}
				}
			}
		}
		/// <summary>
		/// Explicitly finish this action. OnDone will be called.
		/// </summary>
		protected void Finish()
		{
			OnDone();
		}

		#region Background Task
		/*  Aufgabe und Funktionsweise:
         * eine Aktion möchte etwas im Hintergrund berechnen (z.B. ein neues aktives Objekt) und wenn die Berechnung
         * fertig ist dieses Objekt auch darstellen. Das Darstellen selbst darf aber nur im thread context der eigentlichen Aktion stattfinden.
         * Es gibt eine Methode, mit der die Berechnung gestartet wird und eine methode, die aufgerufen wird, wenn die Berechnung zu Ende ist.
         * Letztere wird im Kontext des Controls des Frames aufgerufen, so dass man dort auch z.B. das aktive Objekt setzen kann oder Feedback Objekte
         * manipulieren kann.
         * Wenn neue Daten kommen und das alte Objekt noch nicht fertig ist, dann kann man die Berechnung abbrechen und eine neue starten
         * 
         * ERWEITERUNGEN:
         * mit diesem Konzept geht nur ein Backgroundthread pro Aktion, will man mehrere, so muss man das herauslösen
         * und in ein eigenes Objekt packen. Dann kann man z.B. ein einfaches Feedback Objekt berechnen und die aufwendige
         * komplette darstellung. Wenn das Feedback Objekt fertig ist wird es als feedback dargestellt, wenn die ganze Berechnung fertig ist,
         * dann wird Feedback entfernt und alles dargestellt.
         * Die Aktion kann bei OnDone im Falle noch nicht fertig berechneter Ergebnisse den base Aufruf von OnDone aufschieben und diesen erst
         * in der Callback Methode ausführen. Dann kann man immer noch Escape drücken, wenns zu lange dauart (Vervielfältigen, Schraffur)
         */
		Thread backgroundTask;
		bool finishedBackgroundTask;
		Delegate CallbackOnDone;
		private void StartThread(object pars)
		{   // das läuft im background thread
			//System.Diagnostics.Trace.WriteLine("StartThread");
			try
			{
				finishedBackgroundTask = false;
				Delegate MethodToInvoke = (pars as object[])[0] as Delegate;
				CallbackOnDone = (pars as object[])[1] as Delegate;
				object[] invokeParameters = (pars as object[])[2] as object[];
				MethodToInvoke.DynamicInvoke(invokeParameters);
				lock (this)
				{   // das ist der kritische Punkt, man muss bei WaitForBackgroundTask entscheiden können
					// ob wir noch davor oder schon danach sind
					finishedBackgroundTask = true;
				}
				// wenn MethodToInvoke fertig ist, dann wird im thread des Frames CallbackOnDone aufgerufen
				CallbackOnDone.DynamicInvoke();
			}
			catch (ThreadAbortException) { } // hat nichts gemacht, wieder weg damit
			backgroundTask = null; // um anzuzeigen, dass wir fertig sind
								   //System.Diagnostics.Trace.WriteLine("StartThread-Done");
		}
		protected void StartBackgroundTask(Delegate MethodToInvoke, Delegate CallbackOnDone, params object[] invokeParameters)
		{
			//System.Diagnostics.Trace.WriteLine("StartBackgroundTask");
			backgroundTask = new Thread(new ParameterizedThreadStart(StartThread));
			backgroundTask.Start(new object[] { MethodToInvoke, CallbackOnDone, invokeParameters });
			//System.Diagnostics.Trace.WriteLine("StartBackgroundTask-Done");
		}
		protected bool IsBackgroundTaskActive
		{
			get
			{
				return backgroundTask != null;
			}
		}
		protected void CancelBackgroundTask()
		{
			//System.Diagnostics.Trace.WriteLine("CancelBackgroundTask");
			Thread tmp = backgroundTask; // backgroundTask wird null bei Abort
			if (tmp != null)
			{
				tmp.Abort();
				tmp.Join();
			}
			//System.Diagnostics.Trace.WriteLine("CancelBackgroundTask-Done");
		}
		protected void WaitForBackgroundTask()
		{
			//System.Diagnostics.Trace.WriteLine("WaitForBackgroundTask");
			if (backgroundTask != null)
			{
				CurrentMouseView.Canvas.Cursor = "WaitCursor";
				bool stop = false;
				lock (this)
				{   // sind wir schon über den kritischen Punkt in der Ausführung, d.h. ist die Berechnung schon fertig
					if (finishedBackgroundTask)
						stop = true;
				}
				if (stop)
				{   // hier abbrechen, da wir schon über den kritischen Punkt sind und syncCallBack nicht mehr rechtzeitig gesetzt werden konnte
					backgroundTask.Abort();
					CallbackOnDone.DynamicInvoke();
				}
				else
				{   // hier sind wir sicher, dass vor dem kritischen Punkt syncCallBack auf false gesetzt wurde
					// also können wir Join aufrufen. Join funktioniert nicht mit BeginInvoke/EndInvoke, da
					// mit Join dieser Thread blockiert wird und EndInvoke diesen Thread braucht
					backgroundTask.Join();
				}
			}
			//System.Diagnostics.Trace.WriteLine("WaitForBackgroundTask-Done");
		}
		#endregion

		bool IPropertyEntry.IsOpen { get; set; }
		public event PropertyEntryChangedStateDelegate StateChangedEvent;
		void IPropertyEntry.Opened(bool isOpen)
		{
			if (StateChangedEvent != null)
			{
				if (isOpen)
					StateChangedEvent(this, new StateChangedArgs(StateChangedArgs.State.OpenSubEntries));
				else
					StateChangedEvent(this, new StateChangedArgs(StateChangedArgs.State.CollapseSubEntries));
			}
		}
		public bool HideButtons { get; set; } = false;
		PropertyEntryType IPropertyEntry.Flags
		{
			get
			{
				PropertyEntryType flags = PropertyEntryType.Selectable | PropertyEntryType.HasSubEntries;
				if (!HideButtons)
				{
					flags |= PropertyEntryType.OKButton | PropertyEntryType.CancelButton;
				}

				return flags;
			}
		}
		bool IPropertyEntry.ReadOnly { get; set; }
		string IPropertyEntry.Label => StringTable.GetString(TitleId + ".Label");
		string IPropertyEntry.Value => null;
		string IPropertyEntry.ResourceId => StringTable.GetString(TitleId);
		object IPropertyEntry.Parent { get; set; }
		int IPropertyEntry.Index { get; set; }
		int IPropertyEntry.IndentLevel { get; set; }
		IPropertyEntry[] IPropertyEntry.SubItems => SubProperties;
		int IPropertyEntry.OpenOrCloseSubEntries()
		{
			if ((this as IPropertyEntry).IsOpen)
			{
				int n = SubProperties.Length;
				(this as IPropertyEntry).IsOpen = false;
				return -n;
			}
			else
			{
				(this as IPropertyEntry).IsOpen = true;
				return SubProperties.Length;
			}
		}

		void IPropertyEntry.ButtonClicked(PropertyEntryButton button)
		{
			switch (button)
			{
				case PropertyEntryButton.ok:

					OnEnter(); // checks whether the action can be finished and if so, calls OnOk()
					break;
				case PropertyEntryButton.cancel:
					OnEscape();
					break;
			}
		}

		void IPropertyEntry.Added(IPropertyPage pp)
		{
			propertyTreeView = pp;
			SetCurrentInputIndex(currentInputIndex, true);
		}

		void IPropertyEntry.Removed(IPropertyPage pp)
		{
			propertyTreeView = null;
		}

		MenuWithHandler[] IPropertyEntry.ContextMenu
		{
			get
			{   // there is no context menu
				throw new NotImplementedException();
			}
		}

		string[] IPropertyEntry.GetDropDownList()
		{   // there is no drop down list
			throw new NotImplementedException();
		}

		void IPropertyEntry.StartEdit(bool editValue)
		{   // cannot be edited
			throw new NotImplementedException();
		}

		void IPropertyEntry.EndEdit(bool aborted, bool modified, string newValue)
		{   // cannot be edited
			throw new NotImplementedException();
		}

		bool IPropertyEntry.EditTextChanged(string newValue)
		{   // cannot be edited
			throw new NotImplementedException();
		}
		string IPropertyEntry.GetErrorText()
		{   // cannot be edited
			throw new NotImplementedException();
		}
		void IPropertyEntry.Selected(IPropertyEntry previousSelected)
		{
			// nothing to do
		}
		void IPropertyEntry.UnSelected(IPropertyEntry nowSelected)
		{
			// nothing to do
		}

		void IPropertyEntry.Select()
		{
			// nothing to do
		}
		void IPropertyEntry.ListBoxSelected(int selectedIndex)
		{
			// nothing to do
		}

		IPropertyEntry IPropertyEntry.FindSubItem(string helpResourceID)
		{
			if ((this as IPropertyEntry).ResourceId == helpResourceID) return this;
			foreach (IPropertyEntry propertyEntry in SubProperties)
			{
				IPropertyEntry found = propertyEntry.FindSubItem(helpResourceID);
				if (found != null) return found;
			}
			return null;
		}
		bool IPropertyEntry.IsLocked
		{
			get
			{
				return false;
			}
			set { }
		}
		bool IPropertyEntry.DeferUpdate
		{
			get
			{
				return false;
			}
			set { }
		}
	}
}
