// #undef DEBUG // zum Performancetest

using CADability.Curve2D;
using CADability.GeoObject;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using Wintellect.PowerCollections;

namespace CADability.Shapes
{

	public class BorderException : System.ApplicationException
	{
		public enum BorderExceptionType { General, AreaOpen, InternalError, PointNotOnBorder, NotConnected };
		public BorderExceptionType ExceptionType;
		public BorderException(string message, BorderExceptionType tp)
			: base(message)
		{
			ExceptionType = tp;
		}
	}
	/// <summary>
	/// A simple border composed of one ore more ICurve2D objects.
	/// A Border is always invariant, i.e. you annot change it (like System.String).
	/// If a border is closed, then it is oriented counterclockwise. A border may be
	/// produced by the BorderBuilder object (or by its constructors).
	/// </summary>   
	[Serializable()]
	public class Border : ISerializable, IDeserializationCallback, IJsonSerialize, IJsonSerializeDone
	{
		static private int idCounter = 0;
		private int id;
		private ICurve2D[] segment;
		private QuadTree quadTree;
		private bool isClosed;
		private BoundingRect extent;
		private double area;
		private Hashtable segmentToIndex; // Zuordnung von Segment-Kurven auf ihren Index
										  // Orientierung für Schnitte bei Äquidistanten, wenn die Schnittwinkel tangential sind, dann wird es unknown
		internal enum Orientation { unknown, negative, positive }
		internal Orientation orientation;
		internal void forceConnect(bool close = false)
		{   // das ist noch nicht ganz spruchreif!
			for (int i = 0; i < segment.Length; ++i)
			{
				int next = i + 1;
				if (next >= segment.Length) next = 0;
				if (segment[i].EndPoint != segment[next].StartPoint)
				{
					segment[next].StartPoint = segment[i].EndPoint;
				}
			}
			if (close)
			{
				if (segment[0].StartPoint != segment[segment.Length - 1].EndPoint)
				{
					segment[0].StartPoint = segment[segment.Length - 1].EndPoint;
				}
				isClosed = true;
			}
		}
		internal void flatten()
		{
			List<ICurve2D> subCurves = new List<ICurve2D>();
			bool flattened = false;
			for (int i = 0; i < segment.Length; ++i)
			{
				if (segment[i] is Path2D)
				{
					Path2D p2d = segment[i] as Path2D;
					for (int j = 0; j < p2d.SubCurves.Length; ++j)
					{
						if (p2d.SubCurves[j].Length > 0)
						{
							subCurves.Add(p2d.SubCurves[j]);
							if (j == 0) p2d.SubCurves[j].UserData.CloneFrom(p2d.UserData); // es geht um "3d" von CurveGraph
						}
					}
					flattened = true;
				}
				else if (segment[i] is Polyline2D)
				{
					Polyline2D p2d = segment[i] as Polyline2D;
					ICurve2D[] sub = p2d.GetSubCurves();
					for (int j = 0; j < sub.Length; ++j)
					{
						if (sub[j].Length > 0)
						{
							subCurves.Add(sub[j]);
							if (j == 0) sub[j].UserData.CloneFrom(p2d.UserData); // es geht um "3d" von CurveGraph
						}
					}
					flattened = true;
				}
				else
				{
					subCurves.Add(segment[i]);
				}
			}
			if (flattened)
			{
				Segments = subCurves.ToArray();
				flatten(); // might still contain paths or polylines
			}
		}
		protected Border()
		{
			id = ++idCounter;
		}
		internal int Id
		{
			get
			{
				return id;
			}
		}
		internal Border(ArrayList SegemntsToAdd, BoundingRect ext, bool IsClosed)
			: this()
		{
			isClosed = IsClosed;
			Segments = (ICurve2D[])SegemntsToAdd.ToArray(typeof(ICurve2D));
			flatten();
			extent = ext;
			area = 0.0;
			if (isClosed)
			{
				double d = 0.0; // die akkumulierte Winkeldifferenz
				GeoVector2D lastDir = segment[segment.Length - 1].EndDirection;
				for (int i = 0; i < segment.Length; ++i)
				{
					SweepAngle vertexdiff = new SweepAngle(lastDir, segment[i].StartDirection);
					d += vertexdiff;
					d += segment[i].Sweep;
					lastDir = segment[i].EndDirection;
				}
				// d muss +2*pi oder -2*pi sein, alles andere deutet auf eine innere Schleife hin
				// leider ist es nicht so, dass wenn d==+-2*pi ist es keine Selbstüberschneidung gibt.
				if (d < 0)
				{   // umdrehen
					Array.Reverse(segment);
					for (int i = 0; i < segment.Length; ++i)
					{
						segment[i].Reverse();
					}
				}
			}
			segmentToIndex = new Hashtable(segment.Length);
			for (int i = 0; i < segment.Length; ++i)
			{
				segmentToIndex.Add(segment[i], i);
			}
			if (isClosed) area = RecalcArea();
		}
		/// <summary>
		/// Constructs a Border from a list of ICurve2D objects. The objects must be in the
		/// correct order and must be continous, i.e. EndPoint of ICurve2D[i] must be
		/// equal to Startpoint of ICurve2D[i+1]. Equality refers to Precision.IsEqual().
		/// It will be checked automatically, whether the border is closed and if so,
		/// it will be oriented counterclockwise.
		/// </summary>
		/// <param name="segments">list of curves to make the border</param>
		public Border(ICurve2D[] segments)
			: this()
		{
			Segments = (ICurve2D[])segments.Clone();
			flatten();
			bool reversed;
			Recalc(out reversed);
		}
		public Border(out bool reversed, ICurve2D[] segments)
			: this()
		{
			Segments = (ICurve2D[])segments.Clone();
			flatten();
			Recalc(out reversed);
		}
		/// <summary>
		/// Create a border with the provided segments assumed to be in correct order and orientation. If <paramref name="forceConnected"/>
		/// is true, the border will be closed.
		/// </summary>
		/// <param name="segments">The oriented and ordered segments</param>
		/// <param name="forceConnected">True: border will be closed even if segments aren't connected at the end</param>
		public Border(ICurve2D[] segments, bool forceConnected, bool flatten = true)
			: this()
		{
			Segments = (ICurve2D[])segments.Clone();
			if (flatten) this.flatten();
			if (forceConnected) forceConnect();
			bool reversed;
			Recalc(out reversed);
		}
		public Border(ICurve2D onlySegment)
			: this()
		{
			Segments = new ICurve2D[] { onlySegment };
			flatten();
			bool reversed;
			Recalc(out reversed);
		}
		public Border(GeoPoint2D[] polyline)
			: this()
		{
			GeoPoint2D[] closed = new GeoPoint2D[polyline.Length + 1];
			Array.Copy(polyline, closed, polyline.Length);
			closed[polyline.Length] = polyline[0];
			Polyline2D p2d = new Polyline2D(closed);
			Segments = new ICurve2D[] { p2d };
			flatten();
			bool reversed;
			Recalc(out reversed);
		}

		private QuadTree QuadTree
		{   // the QuadTree is only calculated when needed
			get
			{
				if (quadTree == null)
				{
					quadTree = new QuadTree(Extent);
					quadTree.MaxDeepth = -1; // dynamic quadTree
					for (int i = 0; i < segment.Length; ++i) quadTree.AddObject(segment[i]);
				}
				return quadTree;
			}
		}
		internal static Border FromUnorientedList(ICurve2D[] segments, bool forceClosed, double uperiod, double vperiod, double umin, double umax, double vmin, double vmax)
		{   // Sonderfall von FromUnorientedList: wenn die Kurven periodisch um einen offset versetzt werden
			// können, ohne ihre Bedeutung zu verändern, dann gibt es noch viel mehr zu testen.
			// Vorlauf: die einzelnen Segmente werden so verschoben, dass wenigstens ein Punkt im Standard Bereich für periodische
			// Kurven liegt. Damit sind spiralförmige Zylinderoberflächen nicht möglich, aber periodisch ist halt sowieso Mist!
			// Wir sollten ohnehin beim Import alles periodische durch nichtperiodisches ersetzen und beim Export NURBS
			// daraus machen. Durch MakeRegularSurface beim Import würden dann wieder die echten aber nichtperiodischen Flächen entstehen
			bool moved = false;
			for (int i = 0; i < segments.Length; i++)
			{
				if (uperiod != 0.0)
				{
					while (segments[i].StartPoint.x > uperiod && segments[i].EndPoint.x > uperiod)
					{
						segments[i].Move(-uperiod, 0.0);
						moved = true;
					}
					while (segments[i].StartPoint.x < 0.0 && segments[i].EndPoint.x < 0.0)
					{
						segments[i].Move(uperiod, 0.0);
						moved = true;
					}
				}
				if (vperiod != 0.0)
				{
					while (segments[i].StartPoint.y > vperiod && segments[i].EndPoint.y > vperiod)
					{
						segments[i].Move(0.0, -vperiod);
						moved = true;
					}
					while (segments[i].StartPoint.y < 0.0 && segments[i].EndPoint.y < 0.0)
					{
						segments[i].Move(0.0, vperiod);
						moved = true;
					}
				}
			}
			if (moved)
			{
				BoundingRect ext = BoundingRect.EmptyBoundingRect;
				for (int i = 0; i < segments.Length; i++)
				{
					ext.MinMax(segments[i].StartPoint);
					ext.MinMax(segments[i].EndPoint);
				}
				umin = ext.Left;
				umax = ext.Right;
				vmin = ext.Bottom;
				vmax = ext.Top;
			}
#if DEBUG
			DebuggerContainer dc = new DebuggerContainer();
			for (int i = 0; i < segments.Length; i++)
			{
				dc.Add(segments[i], System.Drawing.Color.Red, i);
			}
#endif
			// hier war uperiod/2.0 u.s.w. wg. NEED_REGULARIZATION.stp auf uperiod geändert
			BoundingRect brect = new BoundingRect(umin - uperiod / 2.0, vmin - vperiod / 2.0, umax + uperiod / 2.0, vmax + vperiod / 2.0);
			BoundingRect innerbounds = new BoundingRect(umin, vmin, umax, vmax);
			if (segments.Length >= 2)
			{   // für die ersten beiden gibt es 4 Möglichkeiten
				// eine gleichzeitige Verschiebung von u und v ist noch nicht berücksichtigt
				SortedList<double, int> dist = new SortedList<double, int>(4);
				dist[Geometry.Dist(segments[0].StartPoint, segments[1].StartPoint)] = 1;
				dist[Geometry.Dist(segments[0].EndPoint, segments[1].StartPoint)] = 2;
				dist[Geometry.Dist(segments[0].StartPoint, segments[1].EndPoint)] = 3;
				dist[Geometry.Dist(segments[0].EndPoint, segments[1].EndPoint)] = 4;
				if (uperiod != 0.0)
				{   // die Frage bei den ersten beiden Objekten ist hier: welche Verschiebung ist besser: 
					// die von segment[0] oder die von segment[1]. Wenn man nämlich falsch anfängt, dann gehts am Ende nicht gut aus
					GeoVector2D offset = new GeoVector2D(uperiod, 0.0);
					double extra = 0.0;
					if (segments[1].StartPoint + offset <= innerbounds && segments[1].EndPoint + offset <= innerbounds) extra = Precision.eps;
					if (segments[1].StartPoint + offset <= brect && segments[1].EndPoint + offset <= brect)
					{
						dist[Geometry.Dist(segments[0].StartPoint, segments[1].StartPoint + offset) - extra] = 5;
						dist[Geometry.Dist(segments[0].EndPoint, segments[1].StartPoint + offset) - extra] = 6;
						dist[Geometry.Dist(segments[0].StartPoint, segments[1].EndPoint + offset) - extra] = 7;
						dist[Geometry.Dist(segments[0].EndPoint, segments[1].EndPoint + offset) - extra] = 8;
					}
					offset = new GeoVector2D(-uperiod, 0.0);
					extra = 0.0;
					if (segments[1].StartPoint + offset <= innerbounds && segments[1].EndPoint + offset <= innerbounds) extra = Precision.eps;
					if (segments[1].StartPoint + offset <= brect && segments[1].EndPoint + offset <= brect)
					{
						dist[Geometry.Dist(segments[0].StartPoint, segments[1].StartPoint + offset) - extra] = 9;
						dist[Geometry.Dist(segments[0].EndPoint, segments[1].StartPoint + offset) - extra] = 10;
						dist[Geometry.Dist(segments[0].StartPoint, segments[1].EndPoint + offset) - extra] = 11;
						dist[Geometry.Dist(segments[0].EndPoint, segments[1].EndPoint + offset) - extra] = 12;
					}
					offset = new GeoVector2D(uperiod, 0.0);
					extra = 0.0;
					if (segments[0].StartPoint + offset <= innerbounds && segments[0].EndPoint + offset <= innerbounds) extra = Precision.eps;
					if (segments[0].StartPoint + offset <= brect && segments[0].EndPoint + offset <= brect)
					{
						dist[Geometry.Dist(segments[0].StartPoint + offset, segments[1].StartPoint) - extra] = 21;
						dist[Geometry.Dist(segments[0].EndPoint + offset, segments[1].StartPoint) - extra] = 22;
						dist[Geometry.Dist(segments[0].StartPoint + offset, segments[1].EndPoint) - extra] = 23;
						dist[Geometry.Dist(segments[0].EndPoint + offset, segments[1].EndPoint) - extra] = 24;
					}
					offset = new GeoVector2D(-uperiod, 0.0);
					extra = 0.0;
					if (segments[0].StartPoint + offset <= innerbounds && segments[0].EndPoint + offset <= innerbounds) extra = Precision.eps;
					if (segments[0].StartPoint + offset <= brect && segments[0].EndPoint + offset <= brect)
					{
						dist[Geometry.Dist(segments[0].StartPoint + offset, segments[1].StartPoint) - extra] = 25;
						dist[Geometry.Dist(segments[0].EndPoint + offset, segments[1].StartPoint) - extra] = 26;
						dist[Geometry.Dist(segments[0].StartPoint + offset, segments[1].EndPoint) - extra] = 27;
						dist[Geometry.Dist(segments[0].EndPoint + offset, segments[1].EndPoint) - extra] = 28;
					}
				}
				if (vperiod != 0.0)
				{
					GeoVector2D offset = new GeoVector2D(0.0, vperiod);
					double extra = 0.0;
					if (segments[1].StartPoint + offset <= innerbounds && segments[1].EndPoint + offset <= innerbounds) extra = Precision.eps;
					if (segments[1].StartPoint + offset <= brect && segments[1].EndPoint + offset <= brect)
					{
						dist[Geometry.Dist(segments[0].StartPoint, segments[1].StartPoint + offset) - extra] = 13;
						dist[Geometry.Dist(segments[0].EndPoint, segments[1].StartPoint + offset) - extra] = 14;
						dist[Geometry.Dist(segments[0].StartPoint, segments[1].EndPoint + offset) - extra] = 15;
						dist[Geometry.Dist(segments[0].EndPoint, segments[1].EndPoint + offset) - extra] = 16;
					}
					offset = new GeoVector2D(0.0, -vperiod);
					extra = 0.0;
					if (segments[1].StartPoint + offset <= innerbounds && segments[1].EndPoint + offset <= innerbounds) extra = Precision.eps;
					if (segments[1].StartPoint + offset <= brect && segments[1].EndPoint + offset <= brect)
					{
						dist[Geometry.Dist(segments[0].StartPoint, segments[1].StartPoint + offset) - extra] = 17;
						dist[Geometry.Dist(segments[0].EndPoint, segments[1].StartPoint + offset) - extra] = 18;
						dist[Geometry.Dist(segments[0].StartPoint, segments[1].EndPoint + offset) - extra] = 19;
						dist[Geometry.Dist(segments[0].EndPoint, segments[1].EndPoint + offset) - extra] = 20;
					}
					offset = new GeoVector2D(0.0, vperiod);
					extra = 0.0;
					if (segments[0].StartPoint + offset <= innerbounds && segments[0].EndPoint + offset <= innerbounds) extra = Precision.eps;
					if (segments[0].StartPoint + offset <= brect && segments[0].EndPoint + offset <= brect)
					{
						dist[Geometry.Dist(segments[0].StartPoint + offset, segments[1].StartPoint) - extra] = 29;
						dist[Geometry.Dist(segments[0].EndPoint + offset, segments[1].StartPoint) - extra] = 30;
						dist[Geometry.Dist(segments[0].StartPoint + offset, segments[1].EndPoint) - extra] = 31;
						dist[Geometry.Dist(segments[0].EndPoint + offset, segments[1].EndPoint) - extra] = 32;
					}
					offset = new GeoVector2D(0.0, -vperiod);
					extra = 0.0;
					if (segments[0].StartPoint + offset <= innerbounds && segments[0].EndPoint + offset <= innerbounds) extra = Precision.eps;
					if (segments[0].StartPoint + offset <= brect && segments[0].EndPoint + offset <= brect)
					{
						dist[Geometry.Dist(segments[0].StartPoint + offset, segments[1].StartPoint) - extra] = 33;
						dist[Geometry.Dist(segments[0].EndPoint + offset, segments[1].StartPoint) - extra] = 34;
						dist[Geometry.Dist(segments[0].StartPoint + offset, segments[1].EndPoint) - extra] = 35;
						dist[Geometry.Dist(segments[0].EndPoint + offset, segments[1].EndPoint) - extra] = 36;
					}
				}
				if (dist.Values[0] >= 5 && dist.Values[0] <= 8)
				{
					segments[1].Move(uperiod, 0.0);
				}
				else if (dist.Values[0] >= 9 && dist.Values[0] <= 12)
				{
					segments[1].Move(-uperiod, 0.0);
				}
				else if (dist.Values[0] >= 13 && dist.Values[0] <= 16)
				{
					segments[1].Move(0.0, vperiod);
				}
				else if (dist.Values[0] >= 17 && dist.Values[0] <= 20)
				{
					segments[1].Move(0.0, -vperiod);
				}
				else if (dist.Values[0] >= 21 && dist.Values[0] <= 24)
				{
					segments[0].Move(uperiod, 0.0);
				}
				else if (dist.Values[0] >= 25 && dist.Values[0] <= 28)
				{
					segments[0].Move(-uperiod, 0.0);
				}
				else if (dist.Values[0] >= 29 && dist.Values[0] <= 32)
				{
					segments[0].Move(0.0, vperiod);
				}
				else if (dist.Values[0] >= 33 && dist.Values[0] <= 36)
				{
					segments[0].Move(0.0, -vperiod);
				}
				switch (dist.Values[0]) // das ist der Fall für den kleinsten Abstand
				{
					case 1:
					case 5:
					case 9:
					case 13:
					case 17:
					case 21:
					case 25:
					case 29:
					case 33:
						segments[0].Reverse();
						break;
					case 2: // beide richtig rum
					case 6:
					case 10:
					case 14:
					case 18:
					case 22:
					case 26:
					case 30:
					case 34:
						break;
					case 3:
					case 7:
					case 11:
					case 15:
					case 19:
					case 23:
					case 27:
					case 31:
					case 35:
						segments[0].Reverse();
						segments[1].Reverse();
						break;
					case 4:
					case 8:
					case 12:
					case 16:
					case 20:
					case 24:
					case 28:
					case 32:
					case 36:
						segments[1].Reverse();
						break;
				}
				segments[1].StartPoint = segments[0].EndPoint;
				for (int i = 2; i < segments.Length; ++i)
				{
					dist.Clear();
					dist[Geometry.Dist(segments[i - 1].EndPoint, segments[i].StartPoint)] = 1;
					dist[Geometry.Dist(segments[i - 1].EndPoint, segments[i].EndPoint)] = 2;
					if (uperiod != 0.0)
					{
						GeoVector2D offset = new GeoVector2D(uperiod, 0.0);
						if (segments[i].StartPoint + offset <= brect && segments[i].EndPoint + offset <= brect)
						{
							dist[Geometry.Dist(segments[i - 1].EndPoint, segments[i].StartPoint + offset)] = 3;
							dist[Geometry.Dist(segments[i - 1].EndPoint, segments[i].EndPoint + offset)] = 4;
						}
						offset = new GeoVector2D(-uperiod, 0.0);
						if (segments[i].StartPoint + offset <= brect && segments[i].EndPoint + offset <= brect)
						{
							dist[Geometry.Dist(segments[i - 1].EndPoint, segments[i].StartPoint + offset)] = 5;
							dist[Geometry.Dist(segments[i - 1].EndPoint, segments[i].EndPoint + offset)] = 6;
						}
					}
					if (vperiod != 0.0)
					{
						GeoVector2D offset = new GeoVector2D(0.0, vperiod);
						if (segments[i].StartPoint + offset <= brect && segments[i].EndPoint + offset <= brect)
						{
							dist[Geometry.Dist(segments[i - 1].EndPoint, segments[i].StartPoint + offset)] = 7;
							dist[Geometry.Dist(segments[i - 1].EndPoint, segments[i].EndPoint + offset)] = 8;
						}
						offset = new GeoVector2D(0.0, -vperiod);
						if (segments[i].StartPoint + offset <= brect && segments[i].EndPoint + offset <= brect)
						{
							dist[Geometry.Dist(segments[i - 1].EndPoint, segments[i].StartPoint + offset)] = 9;
							dist[Geometry.Dist(segments[i - 1].EndPoint, segments[i].EndPoint + offset)] = 10;
						}
					}
					// hier wird nicht berücksichtigt, dass beide (u und v) verschoben sein können
					if (uperiod != 0.0 && vperiod != 0.0 && dist.Values[0] > Math.Min(uperiod, vperiod) / 2.0)
					{
						GeoVector2D offset = new GeoVector2D(uperiod, vperiod);
						if (segments[i].StartPoint + offset <= brect && segments[i].EndPoint + offset <= brect)
						{
							dist[Geometry.Dist(segments[i - 1].EndPoint, segments[i].StartPoint + offset)] = 11;
							dist[Geometry.Dist(segments[i - 1].EndPoint, segments[i].EndPoint + offset)] = 12;
						}
						offset = new GeoVector2D(uperiod, -vperiod);
						if (segments[i].StartPoint + offset <= brect && segments[i].EndPoint + offset <= brect)
						{
							dist[Geometry.Dist(segments[i - 1].EndPoint, segments[i].StartPoint + offset)] = 13;
							dist[Geometry.Dist(segments[i - 1].EndPoint, segments[i].EndPoint + offset)] = 14;
						}
						offset = new GeoVector2D(-uperiod, vperiod);
						if (segments[i].StartPoint + offset <= brect && segments[i].EndPoint + offset <= brect)
						{
							dist[Geometry.Dist(segments[i - 1].EndPoint, segments[i].StartPoint + offset)] = 15;
							dist[Geometry.Dist(segments[i - 1].EndPoint, segments[i].EndPoint + offset)] = 16;
						}
						offset = new GeoVector2D(-uperiod, -vperiod);
						if (segments[i].StartPoint + offset <= brect && segments[i].EndPoint + offset <= brect)
						{
							dist[Geometry.Dist(segments[i - 1].EndPoint, segments[i].StartPoint + offset)] = 17;
							dist[Geometry.Dist(segments[i - 1].EndPoint, segments[i].EndPoint + offset)] = 18;
						}
					}
					switch (dist.Values[0]) // das ist der Fall für den kleinsten Abstand
					{
						case 1: break;
						case 2:
							segments[i].Reverse();
							break;
						case 3:
							segments[i].Move(uperiod, 0.0);
							break;
						case 4:
							segments[i].Move(uperiod, 0.0);
							segments[i].Reverse();
							break;
						case 5:
							segments[i].Move(-uperiod, 0.0);
							break;
						case 6:
							segments[i].Move(-uperiod, 0.0);
							segments[i].Reverse();
							break;
						case 7:
							segments[i].Move(0.0, vperiod);
							break;
						case 8:
							segments[i].Move(0.0, vperiod);
							segments[i].Reverse();
							break;
						case 9:
							segments[i].Move(0.0, -vperiod);
							break;
						case 10:
							segments[i].Move(0.0, -vperiod);
							segments[i].Reverse();
							break;
						case 11:
							segments[i].Move(uperiod, vperiod);
							break;
						case 12:
							segments[i].Move(uperiod, vperiod);
							segments[i].Reverse();
							break;
						case 13:
							segments[i].Move(uperiod, -vperiod);
							break;
						case 14:
							segments[i].Move(uperiod, -vperiod);
							segments[i].Reverse();
							break;
						case 15:
							segments[i].Move(-uperiod, vperiod);
							break;
						case 16:
							segments[i].Move(-uperiod, vperiod);
							segments[i].Reverse();
							break;
						case 17:
							segments[i].Move(-uperiod, -vperiod);
							break;
						case 18:
							segments[i].Move(-uperiod, -vperiod);
							segments[i].Reverse();
							break;
					}
					segments[i].StartPoint = segments[i - 1].EndPoint;
				}
			}
			if (forceClosed) segments[0].StartPoint = segments[segments.Length - 1].EndPoint;
			return new Border(segments);
		}
		/// <summary>
		/// segments enthält die Kurven zwar in richtiger Reohenfolge doch u.U. in falscher Richtung.
		/// Hier wird also vor dem Erzeugen der Border ggf. umorientiert
		/// </summary>
		/// <param name="segments"></param>
		/// <returns></returns>
		public static Border FromUnorientedList(ICurve2D[] segments, bool forceClosed)
		{
#if DEBUG
			DebuggerContainer dc = new DebuggerContainer();
			for (int i = 0; i < segments.Length; ++i)
			{
				dc.Add(segments[i], System.Drawing.Color.Red, i);
			}
#endif
			if (segments.Length >= 2)
			{   // für die ersten beiden gibt es 4 Möglichkeiten
				SortedList<double, int> dist = new SortedList<double, int>(4);
				dist[Geometry.Dist(segments[0].StartPoint, segments[1].EndPoint)] = 3;
				dist[Geometry.Dist(segments[0].EndPoint, segments[1].EndPoint)] = 4;
				dist[Geometry.Dist(segments[0].StartPoint, segments[1].StartPoint)] = 1;
				dist[Geometry.Dist(segments[0].EndPoint, segments[1].StartPoint)] = 2;
				// der letzte Fall ist bei bereits richtig orientierten Segmenten der Standard und wenn der abstand 0 hat, 
				// dann soll er vorherige mit Abstand 0 überschreiben. Problemfall: Kreisfläche mit zwei Halbkreisen: die einmal vorhandene
				// Reihenfolge der outline soll erhalten bleiben
				switch (dist.Values[0]) // das ist der Fall für den kleinsten Abstand
				{
					case 1:
						segments[0].Reverse();
						break;
					case 2: // beide richtig rum
						break;
					case 3:
						segments[0].Reverse();
						segments[1].Reverse();
						break;
					case 4:
						segments[1].Reverse();
						break;
				}
				if (!Precision.IsEqual(segments[1].StartPoint, segments[0].EndPoint))
					segments[1].StartPoint = segments[0].EndPoint;
			}
			for (int i = 2; i < segments.Length; ++i)
			{
				if (Geometry.Dist(segments[i - 1].EndPoint, segments[i].EndPoint) < Geometry.Dist(segments[i - 1].EndPoint, segments[i].StartPoint))
				{
					segments[i].Reverse();
				}
				if (!Precision.IsEqual(segments[i].StartPoint, segments[i - 1].EndPoint))
					segments[i].StartPoint = segments[i - 1].EndPoint;
			}
			if (forceClosed && segments.Length > 1)
			{
				if (!Precision.IsEqual(segments[0].StartPoint, segments[segments.Length - 1].EndPoint))
					segments[0].StartPoint = segments[segments.Length - 1].EndPoint;
			}
			List<ICurve2D> cleanSegments = new List<ICurve2D>();
			for (int i = 0; i < segments.Length; i++)
			{
				if (segments[i].Length > Precision.eps) cleanSegments.Add(segments[i]);
			}
			//for (int i = 0; i < cleanSegments.Count; ++i)
			//{
			//    int j = (i + 1) % cleanSegments.Count;
			//    if ((cleanSegments[i].EndPoint | cleanSegments[j].StartPoint) > 0.0)
			//    {
			//        GeoPoint2DWithParameter[] ips = cleanSegments[i].Intersect(cleanSegments[j]);
			//        for (int k = 0; k < ips.Length; ++k)
			//        {
			//            if ((Math.Abs(1 - ips[k].par1) + Math.Abs(ips[k].par2)) < 1e-3)
			//            {
			//                cleanSegments[i].EndPoint = ips[k].p;
			//                cleanSegments[j].StartPoint = ips[k].p;
			//            }
			//        }
			//    }
			//}
			if (cleanSegments.Count == 0) return null;
			return new Border(cleanSegments.ToArray(), forceClosed);
		}
		internal static Border FromOrientedList(ICurve2D[] segments)
		{
			if (segments.Length == 0) return null;
#if DEBUG
			//DebuggerContainer dc = new DebuggerContainer();
			//for (int i = 0; i < segments.Length; ++i)
			//{
			//    dc.Add(segments[i], System.Drawing.Color.Red, i);
			//}
#endif
			for (int i = 0; i < segments.Length - 1; ++i)
			{
				if (!Precision.IsEqual(segments[i + 1].StartPoint, segments[i].EndPoint)) return null;
			}
			if (!Precision.IsEqual(segments[0].StartPoint, segments[segments.Length - 1].EndPoint)) return null;
			return new Border(segments, true, false); // don't flatten, because of Area calculation in Face
		}
		/// <summary>
		/// Für BrepIntersection brauchen wir unveränderte Orientierung und einen entsprechenden "Innen-/Außen-begriff"
		/// </summary>
		/// <param name="segments"></param>
		/// <param name="keepOrientation"></param>
		/// <returns></returns>
		internal static Border FromOrientedList(ICurve2D[] segments, bool keepOrientation)
		{
			if (segments.Length == 0) return null;
			Border res = new Border();
			res.Segments = (ICurve2D[])segments.Clone();
			res.orientation = Orientation.unknown;
			res.isClosed = Precision.IsEqual(res.segment[res.segment.Length - 1].EndPoint, res.segment[0].StartPoint);

			return res;
		}
		internal static Border FromOrientedListPeriodic(ICurve2D[] segments, double uPeriod, double vPeriod)
		{
			if (segments.Length == 0) return null;
#if DEBUG
			DebuggerContainer dc = new DebuggerContainer();
			for (int i = 0; i < segments.Length; ++i)
			{
				dc.Add(segments[i], System.Drawing.Color.Red, i);
			}
#endif
			for (int i = 1; i < segments.Length; ++i)
			{
				if (uPeriod > 0.0)
				{
					double du = segments[i - 1].EndPoint.x - segments[i].StartPoint.x;
					if (du > uPeriod / 2) segments[i].Move(uPeriod, 0.0);
					if (du < -uPeriod / 2) segments[i].Move(-uPeriod, 0.0);
				}
				if (vPeriod > 0.0)
				{
					double dv = segments[i - 1].EndPoint.y - segments[i].StartPoint.y;
					if (dv > vPeriod / 2) segments[i].Move(0.0, vPeriod);
					if (dv < -vPeriod / 2) segments[i].Move(0.0, -vPeriod);
				}
			}
#if DEBUG
			DebuggerContainer dc1 = new DebuggerContainer();
			for (int i = 0; i < segments.Length; ++i)
			{
				dc1.Add(segments[i], System.Drawing.Color.Red, i);
			}
#endif
			if (!Precision.IsEqual(segments[0].StartPoint, segments[segments.Length - 1].EndPoint)) return null;
			return new Border(segments, true);
		}
		internal ICurve2D[] Segments
		{
			get
			{
				return segment;
			}
			private set
			{
				if (segment == value)
					return;

				segment = value;

				//Reset UnsplittedOutline to null if the segments changed
				UnsplittedOutline = null;
			}
		}
		internal GeoPoint2D[] Vertices
		{
			get
			{
				List<GeoPoint2D> res = new List<GeoPoint2D>();
				for (int i = 0; i < segment.Length; ++i)
				{
					res.Add(segment[i].StartPoint);
					if (segment[i] is Polyline2D)
					{
						Polyline2D p2d = segment[i] as Polyline2D;
						for (int j = 1; j < p2d.VertexCount - 1; ++j)
						{   // der 1. Punkt ist ja schon dabei, der letzte soll nicht dazu
							res.Add(p2d.GetVertex(j));
						}
					}
				}
				return res.ToArray();
			}
		}
		internal void RemoveSpikes(double precision)
		{
			List<ICurve2D> s = new List<ICurve2D>(segment);
			for (int i = s.Count - 2; i >= 0; --i)
			{
				if ((s[i].StartPoint | s[i + 1].EndPoint) < precision && s[i] is Line2D && s[i + 1] is Line2D)
				{
					int before = i - 1;
					int after = i + 1;
					if (before < 0) before = s.Count - 1;
					if (after >= s.Count) after = 0;
					GeoPoint2D c = new GeoPoint2D(s[i].StartPoint, s[i + 1].EndPoint);
					s[before].EndPoint = c;
					s[after].StartPoint = c;
					s.RemoveRange(i, 2);
				}
			}
			if ((s[s.Count - 1].StartPoint | s[0].EndPoint) < precision && s[s.Count - 1] is Line2D && s[0] is Line2D)
			{
				int before = s.Count - 2;
				int after = 1;
				GeoPoint2D c = new GeoPoint2D(s[s.Count - 1].StartPoint, s[0].EndPoint);
				s[before].EndPoint = c;
				s[after].StartPoint = c;
				s.RemoveAt(s.Count - 1);
				s.RemoveAt(0);
			}
			if (s.Count < segment.Length)
			{
				Segments = s.ToArray();
				bool reversed;
				Recalc(out reversed);
			}
		}
		internal bool RemoveOverlap()
		{   // nicht geschlossene Borders, die sich am Anfang/Ende überlappen werden so geschlossen
			int n = segment.Length - 1;
			double prec = this.Extent.Size * 1e-8;
			int startAt = -1;
			int endAt = -1;
			for (int i = 0; i < segment.Length / 2 - 1; ++i)
			{
				ICollection col = QuadTree.GetObjectsFromRect(new BoundingRect(segment[i].StartPoint, prec, prec));
				foreach (ICurve2D curve in col)
				{
					if (curve != segment[i] && (i == 0 || curve != segment[i - 1]))
					{
						if ((segment[i].StartPoint | curve.EndPoint) < prec)
						{
							startAt = i;
							for (int j = 0; j < segment.Length / 2 - 1; ++j)
							{
								if (curve == segment[n - j])
								{
									endAt = n - j;
									break;
								}
							}
							break;
						}
					}
				}
				if (startAt >= 0) break;
			}
			if (startAt >= 0 && endAt >= 0)
			{
				ICurve2D[] ns = new ICurve2D[endAt - startAt + 1];
				Array.Copy(segment, startAt, ns, 0, ns.Length);
				bool reversed;
				Segments = ns;
				Recalc(out reversed);
				return true;
			}
			else
			{
				return false;
			}
		}
		protected double RecalcArea()
		{
			double a = 0.0;
			for (int i = 0; i < segment.Length; ++i)
			{
				a += segment[i].GetArea();
			}
			return a;
		}

		protected double getRoughArea()
		{
			double a = 0.0;
			ArrayList al = new ArrayList();
			for (int i = 0; i < segment.Length; ++i)
			{
				if (segment[i] is BSpline2D)
				{
					double[] knots = (segment[i] as BSpline2D).Knots;
					GeoPoint2D[] pp = new GeoPoint2D[knots.Length];
					for (int j = 0; j < pp.Length; j++)
					{
						pp[j] = (segment[i] as BSpline2D).PointAtParam(knots[j]);
					}
					Polyline2D p2d = new Polyline2D(pp);
					return p2d.GetArea();
				}
				else
				{
					a += segment[i].GetArea();
				}
			}
			return a;
		}
		protected void Recalc(out bool reversed)
		{   // Berechnet QuadTree, segmentToIndex und extent. Erwartet wird das segment Array
			// in korrekter Reihenfolge. Bei geschlossenen Borders wird ggf. umgedreht
			reversed = false;
			quadTree = null;
			BoundingRect ext = new BoundingRect(System.Double.MaxValue, System.Double.MaxValue, System.Double.MinValue, System.Double.MinValue);
			// BoundingRect.EmptyBoundingRect;
			segmentToIndex = new Hashtable(segment.Length);
			for (int i = 0; i < segment.Length; ++i)
			{
				if (i > 0)
				{
					if (!Precision.IsEqual(segment[i - 1].EndPoint, segment[i].StartPoint))
					{
						segment[i - 1].EndPoint = segment[i].StartPoint;
						// throw new BorderException("Border: curves not connected", BorderException.BorderExceptionType.NotConnected);
					}
				}
				ext.MinMax(segment[i].GetExtent());
				segmentToIndex.Add(segment[i], i);
			}
			if (isClosed)
			{
				if (!Precision.IsEqual(segment[segment.Length - 1].EndPoint, segment[0].StartPoint))
				{
					segment[segment.Length - 1].EndPoint = segment[0].StartPoint;
				}
			}
			extent = ext;
			if (segment.Length > 1)
			{
				isClosed = Precision.IsEqual(segment[segment.Length - 1].EndPoint, segment[0].StartPoint);
			}
			else
			{
				isClosed = segment[0].IsClosed;
			}
			if (isClosed)
			{
				// area = RecalcArea();
				// die Berechnung von area braucht enorm viel Zeit bei NURBS Kurven, 
				// daher ist sie bei Sichtbarkeitsprüfungen nicht zu gebrauchen
				double d = 0.0; // die akkumulierte Winkeldifferenz
				GeoVector2D lastDir = segment[segment.Length - 1].EndDirection;
				int lastind = segment.Length - 1;
				bool unknown = false;
				for (int i = 0; i < segment.Length; ++i)
				{
					SweepAngle vertexdiff = new SweepAngle(lastDir, segment[i].StartDirection);
					// leider ein Problem bei sehr spitzen Winkeln (Kreisbogen an Linie)
					// dort ist das Ergebnis zufällig +180° oder -180°. Deshalb folgender test:
					if (Math.Abs(Math.Abs(vertexdiff.Radian) - Math.PI) < 0.01)
					{   // das ist natürlich ein Hauruckverfahren: 10% auf beiden Kurven nach innen gehen
						// bei Bögen und bei Ellipsen ist das kein Problem, aber bei
						// oszillierenden Splines könnte es eines sein...
						vertexdiff = new SweepAngle(segment[lastind].DirectionAt(0.9), segment[i].DirectionAt(0.1));
						if (vertexdiff < 0.0) vertexdiff = -Math.PI;
						else vertexdiff = Math.PI;
						unknown = true;
					}
					d += vertexdiff; // der ist immer zwischen -pi und +pi
					d += segment[i].Sweep;
					lastDir = segment[i].EndDirection;
					lastind = i;
				}
				// d muss +2*pi oder -2*pi sein, alles andere deutet auf eine innere Schleife hin
				// leider ist es nicht so, dass wenn d==+-2*pi ist es keine Selbstüberschneidung gibt.
				if (Math.Abs(d) < Math.PI || Math.Abs(d) > 3.0 * Math.PI || unknown)
				{   // in diesem Fall ist obiger Algorithmus nicht eindeutig, man muss genauer rechnen.
					// Das kommt z.B. vor, wenn es kleine Überschneidungen an sehr spitzen Ecken gibt.
					// Die Flächenberechnung dauert zwar lange, kommt aber fast nie dran
					area = RecalcArea();
					if (area < 0.0)
					{
						area = -area;
						d = -1.0; // damit folgende Bedingung greift
					}
					else d = 1.0; // damit nicht umgedreht wird
				}
				if (d < 0)
				{   // umdrehen
					reversed = true;
					Array.Reverse(segment);
					segmentToIndex.Clear();
					for (int i = 0; i < segment.Length; ++i)
					{
						segment[i].Reverse();
						segmentToIndex.Add(segment[i], i);
					}
				}
			}
		}
		public void ChangeCyclicalStart(int newStartIndex)
		{
			ICurve2D[] newsegment = new ICurve2D[segment.Length];
			Array.Copy(segment, newStartIndex, newsegment, 0, segment.Length - newStartIndex);
			Array.Copy(segment, 0, newsegment, segment.Length - newStartIndex, newStartIndex);
			Segments = newsegment;
			segmentToIndex.Clear();
			for (int i = 0; i < segment.Length; ++i)
			{
				segmentToIndex.Add(segment[i], i);
			}
			bool reversed;
			Recalc(out reversed);
		}
		public Border Clone()
		{
			ICurve2D[] cloned = new ICurve2D[segment.Length];
			for (int i = 0; i < segment.Length; ++i)
			{
				cloned[i] = segment[i].Clone();
			}
			return new Border(cloned);
		}
		public ICurve2D[] GetClonedSegments()
		{
			ICurve2D[] cloned = new ICurve2D[segment.Length];
			for (int i = 0; i < segment.Length; ++i)
			{
				cloned[i] = segment[i].Clone();
			}
			return cloned;
		}

		internal void Reduce()
		{
			double prec = (Extent.Width + Extent.Height) * 1e-8;
			Reduce(prec);
		}
		internal void Reduce(double prec)
		{
			List<ICurve2D> red = new List<ICurve2D>(segment);
			for (int i = red.Count - 1; i >= 0; --i)
			{
				if (!(red[i] is Line2D) && !(red[i] is Arc2D) && !(red[i] is Circle2D))
				{
					ICurve2D app = red[i].Approximate(false, prec); // in Linien und Bögen annähern. Das könnte man noch über einen 2. Parameter steuern
					if (app is Path2D)
					{
						red.RemoveAt(i);
						red.InsertRange(i, (app as Path2D).SubCurves);
					}
					else
					{
						red[i] = app;
					}
				}
			}
			for (int i = red.Count - 1; i >= 1; --i)
			{
				ICurve2D fused = red[i - 1].GetFused(red[i], prec);
				if (fused != null)
				{
					if ((fused.StartPoint | red[i - 1].StartPoint) < prec && (fused.EndPoint | red[i].EndPoint) < prec)
					{   // zusätzliche Bedingung, die das Verschmelzen von in sich zurücklaufenden Linien verhindert,
						// da dadurch gegen den Zusammenhang der Kurven verstoßen wird
						fused.UserData.CloneFrom(red[i].UserData); // Userdata wird u.a. für die Spiralfüllung gebraucht, hier natürlich nicht eindeutig
						red[i - 1] = fused;
						red.RemoveAt(i);
					}
				}
				else if (red[i].Length < prec)
				{
					red[i - 1].EndPoint = red[i].EndPoint;
					red.RemoveAt(i); // neu
				}
			}
			if (red.Count > 1)
			{   // letzte mit erster testen
				ICurve2D fused = red[red.Count - 1].GetFused(red[0], prec);
				if (fused != null)
				{
					if ((fused.StartPoint | red[red.Count - 1].StartPoint) < prec && (fused.EndPoint | red[0].EndPoint) < prec)
					{   // zusätzliche Bedingung, die das Verschmelzen von in sich zurücklaufenden Linien verhindert,
						// da dadurch gegen den Zusammenhang der Kurven verstoßen wird
						fused.UserData.CloneFrom(red[0].UserData); // Userdata wird u.a. für die Spiralfüllung gebraucht, hier natürlich nicht eindeutig
						red[red.Count - 1] = fused;
						red.RemoveAt(0);
					}
				}
				else if (red[0].Length < prec)
				{
					if (!red[red.Count - 1].IsClosed) red[red.Count - 1].EndPoint = red[0].EndPoint;
					red.RemoveAt(0); // neu
				}
			}
			bool wasClosed = isClosed;
			Segments = red.ToArray();
			if (wasClosed) forceClosed();
			bool dumy;
			Recalc(out dumy);
		}
		internal bool ReduceDeadEnd()
		{
			double prec = (Extent.Width + Extent.Height) * 1e-8;
			return ReduceDeadEnd(prec);
		}
		internal bool ReduceDeadEnd(double prec)
		{
			List<ICurve2D> red = new List<ICurve2D>(segment);
			bool removed = true;
			while (removed)
			{
				removed = false;
				for (int i = 0; i < red.Count; ++i)
				{
					// entferne in sich zurücklaufende Kurven
					int before = i - 1;
					if (i == 0) before = red.Count - 1;
					if (Precision.OppositeDirection(red[i].StartDirection, red[before].EndDirection))
					{
						if ((red[i].EndPoint | red[before].StartPoint) < prec)
						{
							red.RemoveAt(i);
							if (i == 0) red.RemoveAt(red.Count - 1);
							else red.RemoveAt(i - 1);
							removed = true;
							break;
						}
					}
					if (red.Count > 2)
					{   // wenn die beiden sich nur teilweise überdecken
						double dist = Math.Abs(red[before].MinDistance(red[i].EndPoint));
						if (dist < prec)
						{   // i liegt mit seinem Endpunkt auf before
							double oldlen = red[before].Length; // Problem war: Kreisbogen läuft in sich zurück und wird gegenläufiger fast Vollkreis
							ICurve2D clone = red[before].Clone();
							red[before].EndPoint = red[i].EndPoint;
							if (red[before].Length > oldlen) red[before].Copy(clone); // es ist länger geworden, da stimmt was nicht
							else
							{
								red.RemoveAt(i);
								removed = true;
							}
							break;
						}
						dist = Math.Abs(red[i].MinDistance(red[before].StartPoint));
						if (dist < prec)
						{
							double oldlen = red[i].Length; // Problem war: Kreisbogen läuft in sich zurück und wird gegenläufiger fast Vollkreis
							ICurve2D clone = red[i].Clone();
							red[i].StartPoint = red[before].StartPoint;
							if (red[i].Length > oldlen) red[i].Copy(clone);
							else
							{
								red.RemoveAt(before);
								removed = true;
							}
							break;
						}
					}
				}
			}
			Segments = red.ToArray();
			if (segment.Length == 0)
			{
				area = 0.0;
				return false;
			}
			bool dumy;
			Recalc(out dumy);
			return true;
		}
		internal void SplitSingleCurve()
		{   // avoid borders with single outline, like circles or closed bsplines
			// they make problems in BorderOperation since the position of the start/endpoint 0.0 ad 1.0 is ambiguous
			if (segment.Length == 1)
			{
				ICurve2D orgSegment = segment[0].Clone();
				Segments = segment[0].Split(0.5);

				//Save the userdata to the splitted
				foreach (ICurve2D item in Segments)
					item.UserData.Add(orgSegment.UserData);

				Recalc(out _);
				UnsplittedOutline = orgSegment;
			}
		}

		/// <summary>
		/// In case of a border with a single outline like circles or closed bsplines
		/// this property will contain a cloned unsplitted outline. While GetClonedSegments() will return the splitted one.
		/// </summary>
		public ICurve2D UnsplittedOutline { get; private set; }

		//public void RemoveSegmentsSmaller(double prec)
		//{
		//    List<ICurve2D> red = new List<ICurve2D>();
		//    for (int i = 0; i < segment.Length; ++i)
		//    {
		//        if (segment[i].Length < prec)
		//        {
		//            GeoPoint2D c = segment[i].PointAt(0.5);
		//            int before, after;
		//            if (i == 0) before = segment.Length - 1;
		//            else before = i - 1;
		//            if (i == segment.Length - 1) after = 0;
		//            else after = i + 1;
		//            segment[before].EndPoint = c;
		//            segment[after].StartPoint = c;
		//        }
		//        else
		//        {
		//            red.Add(segment[i]);
		//        }
		//    }
		//    segment = red.ToArray();
		//    bool dumy;
		//    Recalc(out dumy);
		//}
		//internal static Border FromWire2D(CndOCas.Wire2D w2d)
		//{
		//    BorderBuilder bb = new BorderBuilder();
		//    bb.AddWire2D(w2d);
		//    return bb.BuildBorder();
		//}
		public static bool CounterClockwise(ICurve2D[] curves)
		{   // Kurven müssen zusammenhängend und nicht selbstüberschneidnd sein
			double d = 0.0; // die akkumulierte Winkeldifferenz
			GeoVector2D lastDir = curves[curves.Length - 1].EndDirection;
			int lastind = curves.Length - 1;
			for (int i = 0; i < curves.Length; ++i)
			{
				SweepAngle vertexdiff = new SweepAngle(lastDir, curves[i].StartDirection);
				// leider ein Problem bei sehr spitzen Winkeln (Kreisbogen an Linie)
				// dort ist das Ergebnis zufällig +180° oder -180°. Deshalb folgender test:
				if (Math.Abs(Math.Abs(vertexdiff.Radian) - Math.PI) < 0.01)
				{   // das ist natürlich ein Hauruckverfahren: 10% auf beiden Kurven nach innen gehen
					// bei Bögen und bei Ellipsen ist das kein Problem, aber bei
					// oszillierenden Splines könnte es eines sein...
					vertexdiff = new SweepAngle(curves[lastind].DirectionAt(0.9), curves[i].DirectionAt(0.1));
					if (vertexdiff < 0.0) vertexdiff = -Math.PI;
					else vertexdiff = Math.PI;
				}
				d += vertexdiff; // der ist immer zwischen -pi und +pi
				d += curves[i].Sweep;
				lastDir = curves[i].EndDirection;
				lastind = i;
			}
			// d muss +2*pi oder -2*pi sein, alles andere deutet auf eine innere Schleife hin
			return d > 0.0;
		}
		public static bool CounterClockwise(List<ICurve2D> curves)
		{   // Kurven müssen zusammenhängend und nicht selbstüberschneidnd sein
			double d = 0.0; // die akkumulierte Winkeldifferenz
			GeoVector2D lastDir = curves[curves.Count - 1].EndDirection;
			int lastind = curves.Count - 1;
			for (int i = 0; i < curves.Count; ++i)
			{
				SweepAngle vertexdiff = new SweepAngle(lastDir, curves[i].StartDirection);
				// leider ein Problem bei sehr spitzen Winkeln (Kreisbogen an Linie)
				// dort ist das Ergebnis zufällig +180° oder -180°. Deshalb folgender test:
				if (Math.Abs(Math.Abs(vertexdiff.Radian) - Math.PI) < 0.01)
				{   // das ist natürlich ein Hauruckverfahren: 10% auf beiden Kurven nach innen gehen
					// bei Bögen und bei Ellipsen ist das kein Problem, aber bei
					// oszillierenden Splines könnte es eines sein...
					vertexdiff = new SweepAngle(curves[lastind].DirectionAt(0.9), curves[i].DirectionAt(0.1));
					if (vertexdiff < 0.0) vertexdiff = -Math.PI;
					else vertexdiff = Math.PI;
				}
				d += vertexdiff; // der ist immer zwischen -pi und +pi
				d += curves[i].Sweep;
				lastDir = curves[i].EndDirection;
				lastind = i;
			}
			// d muss +2*pi oder -2*pi sein, alles andere deutet auf eine innere Schleife hin
			return d > 0.0;
		}
		internal static double SignedArea(List<ICurve2D> curves)
		{
			return SignedArea(curves.ToArray());
		}

		internal static double SignedArea(ICurve2D[] curves)
		{
			double a = 0.0;
			double maxa = double.MinValue;
			for (int i = 0; i < curves.Length; ++i)
			{
				double aa = curves[i].GetArea();
				a += aa;
				maxa = Math.Max(maxa, Math.Abs(aa));
			}
			if (Math.Abs(a) < maxa * 1e-5)
			{   // very small area very far away from origin: result is accumulation of round of errors
				// most callers only need the sign of the result
				double x = 0.0, y = 0.0;
				for (int i = 0; i < curves.Length; ++i)
				{
					x += curves[i].StartPoint.x;
					y += curves[i].StartPoint.y;
				}
				x /= curves.Length;
				y /= curves.Length;
				GeoPoint2D p = new GeoPoint2D(x, y);
				a = 0.0;
				for (int i = 0; i < curves.Length; ++i)
				{
					a += curves[i].GetAreaFromPoint(p);
				}
			}
			return a;
		}

		public static bool IsPointOnOutline(ICurve2D[] curves, GeoPoint2D toTest, double precision)
		{
			for (int i = 0; i < curves.Length; i++)
			{
				if (Math.Abs(curves[i].MinDistance(toTest)) < precision) return true;
			}
			return false;
		}
		public static Border MakeRectangle(double left, double right, double bottom, double top)
		{
			ICurve2D[] segments = new ICurve2D[4];
			segments[0] = new Line2D(new GeoPoint2D(left, bottom), new GeoPoint2D(right, bottom));
			segments[1] = new Line2D(new GeoPoint2D(right, bottom), new GeoPoint2D(right, top));
			segments[2] = new Line2D(new GeoPoint2D(right, top), new GeoPoint2D(left, top));
			segments[3] = new Line2D(new GeoPoint2D(left, top), new GeoPoint2D(left, bottom));
			return new Border(segments);
		}
		internal static Border MakeRectangle(BoundingRect bnd)
		{
			return MakeRectangle(bnd.Left, bnd.Right, bnd.Bottom, bnd.Top);
		}
		internal static Border MakeRectangle(GeoPoint2D startPoint, GeoPoint2D endPoint, double halfWidth)
		{
			ICurve2D[] segments = new ICurve2D[4];
			GeoVector2D perp = (endPoint - startPoint).ToRight().Normalized;
			perp = halfWidth * perp;
			GeoPoint2D p1 = startPoint + perp;
			GeoPoint2D p2 = endPoint + perp;
			GeoPoint2D p3 = endPoint - perp;
			GeoPoint2D p4 = startPoint - perp;
			segments[0] = new Line2D(p1, p2);
			segments[1] = new Line2D(p2, p3);
			segments[2] = new Line2D(p3, p4);
			segments[3] = new Line2D(p4, p1);
			return new Border(segments);
		}
		public static Border MakeCircle(GeoPoint2D center, double radius)
		{
			ICurve2D[] segments = new ICurve2D[1];
			segments[0] = new Circle2D(center, radius);
			return new Border(segments);
		}
		public static Border MakePolygon(GeoPoint2D center, double radius, int numberOfSides)
		{
			ICurve2D[] segments = new ICurve2D[numberOfSides];
			double step = Math.PI * 2.0 / numberOfSides;
			GeoPoint2D lastPoint = center + radius * GeoVector2D.XAxis;
			for (int i = 0; i < numberOfSides; ++i)
			{
				segments[i] = new Line2D(lastPoint, center + radius * GeoVector2D.FromAngle(i * step));
				lastPoint = segments[i].EndPoint;
			}
			return new Border(segments);
		}
		internal static Border MakeHole(GeoPoint2D center, double radius)
		{
			ICurve2D[] segments = new ICurve2D[2];
			segments[0] = new Arc2D(center, radius, Angle.A0, SweepAngle.Opposite);
			segments[1] = new Arc2D(center, radius, Angle.A180, SweepAngle.Opposite);
			return new Border(segments);
		}
		public static Border MakeLongHole(ICurve2D centerCurve, double radius)
		{
			ICurve2D[] segments = new ICurve2D[4];
			segments[0] = new Arc2D(centerCurve.EndPoint, radius, new Angle(centerCurve.EndDirection) - Angle.A90, SweepAngle.Opposite);
			segments[2] = new Arc2D(centerCurve.StartPoint, radius, new Angle(centerCurve.StartDirection) + Angle.A90, SweepAngle.Opposite);
			Border bdr = new Border(centerCurve);
			Border[] par1 = bdr.GetParallel(-radius, true, 0.0, 0.0);
			for (int i = 0; i < par1.Length; i++)
			{
				if (!par1[i].IsClosed)
				{
					segments[1] = par1[i].AsPath();
					break;
				}
			}
			//segments[1] = centerCurve.Parallel(-radius, true, Precision.eps, 0.0);
			segments[1].Reverse();
			//segments[3] = centerCurve.Parallel(radius, true, Precision.eps, 0.0);
			Border[] par2 = bdr.GetParallel(radius, true, 0.0, 0.0);
			for (int i = 0; i < par2.Length; i++)
			{
				if (!par2[i].IsClosed)
				{
					segments[3] = par2[i].AsPath();
					break;
				}
			}
			return new Border(segments);
		}
		public static Border MakeLongHole(GeoPoint2D startPoint, GeoPoint2D endPoint, double startRadius, double endRadius)
		{
			if (endRadius <= 0.0)
			{
				Circle2D cs = new Circle2D(startPoint, startRadius);
				GeoPoint2D[] p = Geometry.IntersectCC(startPoint, startRadius, new GeoPoint2D(startPoint, endPoint), (startPoint | endPoint) / 2.0);
				GeoPoint2D ssa, esa; // StartPoint of Start Arc u.s.w, Kreisbögen gegen den Uhrzeigersinn
				ssa = esa = GeoPoint2D.Origin;
				if (p.Length == 2) // sollte immer der Fall sein
				{
					if (Geometry.OnLeftSide(p[0], startPoint, endPoint))
					{
						ssa = p[0];
						esa = p[1];
					}
					else
					{
						ssa = p[1];
						esa = p[0];
					}
				}
				Line2D l1 = new Line2D(endPoint, ssa);
				Arc2D a1 = new Arc2D(startPoint, startRadius, ssa, esa, true);
				Line2D l2 = new Line2D(esa, endPoint);
				return new Border(new ICurve2D[] { l1, a1, l2 });
			}
			else if (startRadius <= 0.0)
			{
				// die beiden Tangentenpunkte
				GeoPoint2D[] p = Geometry.IntersectCC(endPoint, endRadius, new GeoPoint2D(startPoint, endPoint), (startPoint | endPoint) / 2.0);
				GeoPoint2D ssa, esa; // StartPoint of Start Arc u.s.w, Kreisbögen gegen den Uhrzeigersinn
				ssa = esa = GeoPoint2D.Origin;
				if (p.Length == 2) // sollte immer der Fall sein
				{
					if (Geometry.OnLeftSide(p[0], startPoint, endPoint))
					{
						ssa = p[1];
						esa = p[0];
					}
					else
					{
						ssa = p[0];
						esa = p[1];
					}
				}
				Line2D l1 = new Line2D(startPoint, ssa);
				Arc2D a1 = new Arc2D(endPoint, endRadius, ssa, esa, true);
				Line2D l2 = new Line2D(esa, startPoint);
				return new Border(new ICurve2D[] { l1, a1, l2 });
			}
			else
			{
				Circle2D cs = new Circle2D(startPoint, startRadius);
				Circle2D ce = new Circle2D(endPoint, endRadius);
				GeoPoint2D[] p = Curves2D.TangentLines(cs, ce);
				GeoPoint2D ssa, esa, sea, eea; // StartPoint of Start Arc u.s.w, Kreisbögen gegen den Uhrzeigersinn
				ssa = esa = sea = eea = GeoPoint2D.Origin;
				for (int i = 0; i < p.Length; i += 2)
				{
					if (Geometry.OnLeftSide(p[i], startPoint, endPoint) && Geometry.OnLeftSide(p[i + 1], startPoint, endPoint))
					{
						if ((p[i] | startPoint) < (p[i] | endPoint))
						{
							ssa = p[i];
							eea = p[i + 1];
						}
						else
						{
							ssa = p[i];
							eea = p[i + 1];
						}
					}
					else if (!Geometry.OnLeftSide(p[i], startPoint, endPoint) && !Geometry.OnLeftSide(p[i + 1], startPoint, endPoint))
					{
						if ((p[i] | startPoint) < (p[i] | endPoint))
						{
							esa = p[i];
							sea = p[i + 1];
						}
						else
						{
							esa = p[i];
							sea = p[i + 1];
						}
					}
				}
				Line2D l1 = new Line2D(eea, ssa);
				Arc2D a1 = new Arc2D(startPoint, startRadius, ssa, esa, true);
				Line2D l2 = new Line2D(esa, sea);
				Arc2D a2 = new Arc2D(endPoint, endRadius, sea, eea, true);
				return new Border(new ICurve2D[] { l1, a1, l2, a2 });
			}
		}
		internal static Border MakeLongRectHole(ICurve2D centerCurve, double halfWidth)
		{
			ICurve2D[] segments = new ICurve2D[4];
			segments[1] = centerCurve.Parallel(-halfWidth, true, Precision.eps, 0.0);
			segments[1].Reverse();
			segments[3] = centerCurve.Parallel(halfWidth, true, Precision.eps, 0.0);
			segments[0] = new Line2D(segments[3].EndPoint, segments[1].StartPoint);
			segments[2] = new Line2D(segments[1].EndPoint, segments[3].StartPoint);
			if (segments[1].Length < Precision.eps)
			{
				return new Border(new ICurve2D[] { segments[0], segments[2], segments[3] });
			}
			else if (segments[3].Length < Precision.eps)
			{
				return new Border(new ICurve2D[] { segments[0], segments[1], segments[2] });
			}
			else if (!Precision.OppositeDirection(segments[1].MiddleDirection, segments[3].MiddleDirection))
			{
				// Selbstüberschneidung
				GeoPoint2DWithParameter[] ips = segments[0].Intersect(segments[2]);
				if (ips.Length == 1 && ips[0].par1 >= 0.0 && ips[0].par1 <= 1.0 && ips[0].par2 >= 0.0 && ips[0].par2 <= 1.0)
				{
					ICurve2D[] splseg = new ICurve2D[6];
					splseg[0] = segments[0];
					splseg[1] = segments[1];
					splseg[2] = segments[2];
					splseg[3] = segments[0].Clone();
					splseg[4] = segments[3];
					splseg[5] = segments[2].Clone();
					splseg[0].StartPoint = ips[0].p;
					splseg[2].EndPoint = ips[0].p;
					splseg[3].EndPoint = ips[0].p;
					splseg[5].StartPoint = ips[0].p;
					splseg[3].Reverse();
					splseg[4].Reverse();
					splseg[5].Reverse();
					return new Border(splseg);
				}
				else
				{
					// ein Halbkreis, dessen Begrenzungslinien sich überlappen
				}
			}
			return new Border(segments);
		}
		internal Border[] GetParallel(double dist)
		{   // Versuch mit Vereinigung oder Differenz zu arbeiten. 
			// Natürlich wälzt man die Epsilon Problematik dorthin ab, aber mal sehen...
			Border original = this.Clone();
			original.flatten();
			double precision = this.Extent.Size * 1e-6;
			CompoundShape cs = new CompoundShape(new SimpleShape(original));
			if (dist > 0)
			{
				for (int i = 0; i < original.Count; i++)
				{
					CompoundShape toAdd = new CompoundShape(new SimpleShape(MakeLongRectHole(original[i], dist)));
					cs = cs + toAdd;
				}
				for (int i = 0; i < original.Count; i++)
				{
					CompoundShape toAdd = new CompoundShape(new SimpleShape(MakeHole(original[i].StartPoint, dist)));
					cs = cs + toAdd;
				}
			}
			else
			{
				dist = -dist;
				for (int i = 0; i < original.Count; i++)
				{
					CompoundShape toRemove = new CompoundShape(new SimpleShape(MakeLongRectHole(original[i], dist)));
					cs = cs - toRemove;
				}
				for (int i = 0; i < original.Count; i++)
				{
					CompoundShape toRemove = new CompoundShape(new SimpleShape(MakeHole(original[i].StartPoint, dist)));
					cs = cs - toRemove;
				}
			}
			List<Border> res = new List<Border>();
			for (int i = 0; i < cs.SimpleShapes.Length; i++)
			{
				res.Add(cs.SimpleShapes[i].Outline);
				for (int j = 0; j < cs.SimpleShapes[i].NumHoles; j++)
				{
					res.Add(cs.SimpleShapes[i].Holes[j]);
				}
			}
			for (int i = 0; i < res.Count; i++)
			{
				res[i].RemoveSpikes(precision);
			}
			return res.ToArray();
		}
		public double Area
		{
			get
			{
				if (Extent.Size == 0) Recalc(out bool dumy);
				if (!isClosed && (StartPoint | EndPoint) <= Extent.Size * 1e-3) forceClosed();
				if (!isClosed) throw new BorderException("Border must be closed for Area calculation", BorderException.BorderExceptionType.AreaOpen);
				if (area == 0.0) area = RecalcArea();
				return area;
			}
		}
		public bool IsEmpty
		{
			get
			{
				if (area != 0.0) return false;
				double a = getRoughArea();
				return a == 0.0;
			}
		}

		public bool IsClosed
		{
			get { return isClosed; }
		}
		public BoundingRect Extent
		{
			get { return extent; }
		}
		public double Length
		{
			get
			{
				double sum = 0.0;
				for (int i = 0; i < segment.Length; ++i)
				{
					sum += segment[i].Length;
				}
				return sum;
			}
		}
		public ICurve2D this[int index]
		{
			get { return segment[index]; }
		}
		public int Count
		{
			get { return segment.Length; }
		}
		internal enum PositionInternal { Inside, Outside, OnCurve, Unknown } // Achtung, wird hart auf Position gecasted, selbe Reihenfolge beachten
		/// <summary>
		/// Position of a point or boundingrectangle relative to a border.
		/// Inside: the point or rectangle is completely inside the border,
		/// Outside: the point or rectangle is completely outside the border,
		/// OnCurve: the point or rectangle is on the outline of the border.
		/// </summary>
		public enum Position { Inside, Outside, OnCurve, OpenBorder }
		private PositionInternal GetPosition(GeoPoint2D StartPoint, GeoPoint2D EndPoint, double precision)
		{   // Bestimme die Anzahl der Schnittpunkte auf dem Strahl StartPoint->EndPoint.
			// ungerade: Inside, gerade: Outside. In Zweifelsfällen: Unknown
			ArrayList IntersectionPoints = new ArrayList();
			Line2D sl = new Line2D(StartPoint, EndPoint);
			ICollection cl = QuadTree.GetObjectsCloseTo(sl);
			foreach (ICurve2D curve in cl)
			{
				GeoPoint2DWithParameter[] ips = sl.Intersect(curve); // par1 muss von sl sein!
				for (int i = 0; i < ips.Length; ++i)
				{   // liegt ein Schnittpunkt zu nahe an einem Eckpunkt, dann ist das Ergebnis
					// nicht verlässlich
					if (ips[i].p.TaxicabDistance(curve.StartPoint) < precision * 2) return PositionInternal.Unknown;
					if (ips[i].p.TaxicabDistance(curve.EndPoint) < precision * 2) return PositionInternal.Unknown;
					if (ips[i].par1 >= 0.0 && ips[i].par1 <= 1.0 && ips[i].par2 >= 0.0 && ips[i].par2 <= 1.0)
					{
						IntersectionPoints.Add(ips[i]);
					}
				}
			}
			// bei 0 oder einem Schnittpunkt ist die Aussage schon klar:
			if (IntersectionPoints.Count == 0) return PositionInternal.Outside;
			if (IntersectionPoints.Count == 1) return PositionInternal.Inside;
			// IntersectionPoints enthält jetzt alle gefundenen Schnittpunkt. i.A. sind das wenige
			// jetzt feststellen, ob welche doppelt sind, dann ist das Ergebnis untauglich
			GeoPoint2DWithParameter[] gpIntersectionPoints = (GeoPoint2DWithParameter[])IntersectionPoints.ToArray(typeof(GeoPoint2DWithParameter));
			double[] Parameters = new double[gpIntersectionPoints.Length];
			GeoPoint2D[] Points = new GeoPoint2D[gpIntersectionPoints.Length];
			for (int i = 0; i < gpIntersectionPoints.Length; ++i)
			{
				Parameters[i] = gpIntersectionPoints[i].par1;
				Points[i] = gpIntersectionPoints[i].p;
			}
			Array.Sort(Parameters, Points);
			for (int i = 0; i < Points.Length - 1; ++i)
			{
				if (Points[i].TaxicabDistance(Points[i + 1]) < precision * 2) return PositionInternal.Unknown;
			}
			// jetzt zählt nur noch die Anzahl
			if ((gpIntersectionPoints.Length & 0x01) == 0) return PositionInternal.Outside; // gerade Schnittzahl
			else return PositionInternal.Inside; // ungerade Schnittanzahl
		}
		/// <summary>
		/// Returns the <see cref="Position"/> of the given Point relative to this Border.
		/// If the outline of the border interferes with a square around p (width and height
		/// is 2*precision), the result will be OnCurve.
		/// </summary>
		/// <param name="p">The Point to test</param>
		/// <param name="precision">The "radius" (half width) of a square centered at p</param>
		/// <returns>Inside, Outside or OnCurve (on the outline of this border)</returns>
		public Position GetPosition(BoundingRect rect)
		{
			// 1. schneller aber grober Vortest auf außerhalb
			// bool dbg = this.IsClosed;
			// Recalc(out dbg); // warum steht das hier, macht Performance Probleme
			if (isClosed)
			{
				if (BoundingRect.Disjoint(rect, Extent)) return Position.Outside;
			}

			GeoPoint2D p = rect.GetCenter();
			// 2. Vortest, ob der Punkt auf dem Rand liegt, relativ schnell wg. QuadTree
			ICollection cl = QuadTree.GetObjectsFromRect(rect);
			foreach (ICurve2D curve in cl)
			{
				if (curve.HitTest(ref rect, false)) return Position.OnCurve;
			}
			if (!isClosed) return Position.OpenBorder;

			// 3. suche einen Punkt außerhalb und versuche durch Anzahl der Schnittpunkte zu
			// bestimmen, wie die Position ist
			GeoPoint2D lineEnd; // Punkt außerhalb, je nach dem, wo er in Hinsicht auf den
								// Mittelpunkt liegt mit der Diagonale des Extent nach außen gehen
			GeoPoint2D c = extent.GetCenter();
			if (p.x > c.x) lineEnd.x = p.x + extent.Width;
			else lineEnd.x = p.x - extent.Width;
			if (p.y > c.y) lineEnd.y = p.y + extent.Height;
			else lineEnd.y = p.y - extent.Height;
			double len = extent.Width + extent.Height; // länger als die Diagonale
			double precision = len * 1e-8; // es muss benachbarte Punkte geben, die einen größeren Abstand haben
			PositionInternal test = GetPosition(p, lineEnd, precision);
			if (test != PositionInternal.Unknown) return (Position)(test);
			// 4. Hier angekommen heißt, der Schnittlinientest war nicht erfolgreich. (Sehr selten)
			// er ging entweder zu nah durch einen Eckpunkt oder tangential durch
			// ein Kurvenstück. Hier läuft jetzt brachial ein Radarstrahl um p herum und
			// sucht eine sichere Lösung. Die Genauigkeit wird dazu auch heruntergeschraubt,
			// denn der einzg vorstellbare Grund hier endlos hängenzubleiben wäre wenn die
			// Genauigkeit größer als der größte Punktabstand wäre
			Angle a = 0.0;
			SweepAngle offset = 1.0;
			int i = 0;
			while (test == PositionInternal.Unknown)
			{
				a = a + offset; // wg. der Irrationalität von pi kommt man hier nie auf den selben Winkel
				GeoPoint2D pp;
				switch (i % 4)
				{
					default:
					case 0: pp = rect.GetLowerLeft(); break;
					case 1: pp = rect.GetUpperLeft(); break;
					case 2: pp = rect.GetLowerRight(); break;
					case 3: pp = rect.GetUpperRight(); break;
				}
				test = GetPosition(pp, new GeoPoint2D(p, len, a), precision);
				++i;
				if (i == 8) return Position.OnCurve; // oft genug probiert ohne Ergebnis: der Startpunkt muss nahe der Kurve liegen
			}
			return (Position)(test);
		}
		/// <summary>
		/// Returns the <see cref="Position"/> of the given Point relative to this Border.
		/// </summary>
		/// <param name="p">The Point to test</param>
		/// <returns>Inside, Outside or OnCurve (on the outline of this border)</returns>
		public Position GetPosition(GeoPoint2D p)
		{
			return GetPosition(p, Precision.eps);
		}
		internal Position GetPosition(GeoPoint2D p, double precision)
		{
			if (precision == 0.0) precision = Precision.eps;
			return GetPosition(new BoundingRect(p, precision, precision));
		}
		internal PositionInternal GetOrientedPosition(GeoPoint2D p)
		{
			// die Segmente sind nicht zwingend linksrum orientiert. D.h. bei rechtsrum ist "PositionInternal.Inside" die äußere Fläche,
			// innen heißt links von den Segmenten.
			// Hier wird erwartet, dass keine Kurvenpunkte getestet werden. Es soll möglichst schnell gehen.
			// ein Strahl durch den Punkt p, der zwischen den Eckpunkten durchgeht, muss nur auf die Segmente getestet werden, deren Eckpunkte er
			// durchquert. Andere Segmente können zwar auch geschnitten werden, aber immer nur mit einer geraden Anzahl an Schnitten, die sich gegenseitig aufheben.
			// Die Orientierung am nächsten Schnittpunkt wird betrachtet.

			List<int> segmentsToIntersect = null;
			GeoVector2D direction = GeoVector2D.NullVector;
			GeoPoint2D middlePoint = GeoPoint2D.Origin;
			for (int i = 0; i < segment.Length; i++)
			{
				GeoPoint2D mp = new GeoPoint2D(segment[i].StartPoint, segment[i].EndPoint);
				int quality = 0;
				if (segment[i] is Line2D || segment[i] is Arc2D || segment[i] is EllipseArc2D) quality = 0;
				else quality = 1;
				int minquality = int.MaxValue;
				if ((mp | p) > 0.0)
				{
					List<int> otherSegements = new List<int>();
					otherSegements.Add(i);
					for (int j = i + 1; j < segment.Length; j++)
					{
						if (!Geometry.OnSameSide(segment[j].StartPoint, segment[j].EndPoint, p, mp))
						{
							otherSegements.Add(j);
							if (segment[j] is Line2D || segment[j] is Arc2D || segment[j] is EllipseArc2D) quality += 1; // jedes zusätzliche Objekt +1
							else quality += 2; // extra Strafpunkt für schwer zu berechnen
						}
					}
					if (quality < minquality)
					{
						segmentsToIntersect = otherSegements;
						middlePoint = mp;
						if (quality == 1) break; // genau 2 einfache Kurven in der Liste
					}
				}
			}
			GeoVector2D dir = middlePoint - p;
			double minpar = double.MaxValue;
			double orient = 0.0;
			for (int i = 0; i < segmentsToIntersect.Count; i++)
			{
				GeoPoint2DWithParameter[] ip = segment[segmentsToIntersect[i]].Intersect(p, middlePoint);
				for (int j = 0; j < ip.Length; j++)
				{
					if (ip[j].par1 >= 0.0 && ip[j].par1 <= 1)
					{
						if (Math.Abs(ip[j].par2) < minpar)
						{
							minpar = Math.Abs(ip[j].par2);
							orient = GeoVector2D.Orientation(segment[segmentsToIntersect[i]].DirectionAt(ip[j].par1), dir);
							if (ip[j].par2 < 0) orient = -orient; // Schnitt nach hinten, also Vorzeichen umdrehen
						}
					}
				}
			}
			if (orient != 0.0)
			{
				if (orient > 0.0)
					return PositionInternal.Outside;
				else
					return PositionInternal.Inside;
			}
			return PositionInternal.Unknown;
		}

		internal static int GetBeamIntersectionCount(List<ICurve2D> segment, GeoPoint2D fromHere)
		{
			// gesucht ist die Anzahl von Schnittpunkten durch alle Segmente mit einem Starhl von "fromHere", wobei die Richtung frei gewählt werden kann.
			// Die Richtung wird versucht so zu wählen, dass möglichst wenig Schnitte entstehen. Gefährliche Schnitte sind Tangenten und Schnitte durch Endpunkte der Segmente
			// solche sollen nicht vorkommen.

			// Problem: Intersect mit 2 Punkten liefert u.U. nur die inneren Schnittpunkte, nicht die in Verlängerung.
			// Deshalb erzeugen wir eine lange Linie, die auf beiden Seiten aus dem Border herausgeht.
			BoundingRect ext = BoundingRect.EmptyBoundingRect;
			for (int i = 0; i < segment.Count; i++)
			{
				ext.MinMax(segment[i].StartPoint);
			}
			double length = ext.Size * 4; // das müsste reichen, oder?

			// bei Punkt auf Kurve ist das Ergebnis zufällig
			for (int i = 0; i < segment.Count; i++)
			{
				GeoPoint2D mp = new GeoPoint2D(segment[i].StartPoint, segment[i].EndPoint); // versuche mit diesem Punkt
				GeoVector2D dir = (mp - fromHere).Normalized;
				int forward = 0, backward = 0;
				bool badPoint = false;
				for (int k = 0; k < segment.Count; k++)
				{
					GeoPoint2DWithParameter[] ip = segment[k].Intersect(fromHere - length * dir, fromHere + length * dir); // Mitte am Testpunkt, geht über alle segmente hinaus
					for (int j = 0; j < ip.Length; j++)
					{
						if (Math.Abs(ip[j].par1) < 1e-6 || Math.Abs(1.0 - ip[j].par1) < 1e-6)
						{   // Schnitt durch einen Eckpunkt ist schlecht
							badPoint = true;
							break;
						}
						if (ip[j].par1 > 0.0 && ip[j].par1 < 1)
						{
							if (Precision.SameDirection(segment[k].DirectionAt(ip[j].par1), dir, false))
							{   // ein tangentialer Schnitt ist schlecht
								badPoint = true;
								break;
							}
							if (ip[j].par2 > 0.5) ++forward;
							else ++backward;
						}
					}
					if (badPoint) break;
				}
				if (!badPoint && ((forward & 0x01) == (backward & 0x01)))
				{   // beide Schnitt-Anzahlen sind gerade oder beide ungerade, gute Lösung
					return forward;
				}
			}
			// kein Segment-Mittelpunkt hat gefruchtet
			double angle = 1.0; // irgend ein Winkel im Bogenmaß
			for (int i = 0; i < 20; i++) // irgendwann ist Schluss
			{
				GeoVector2D dir = (new Angle(angle)).Direction;
				int forward = 0, backward = 0;
				bool badPoint = false;
				for (int k = 0; k < segment.Count; k++)
				{
					GeoPoint2DWithParameter[] ip = segment[k].Intersect(fromHere - length * dir, fromHere + length * dir);
					for (int j = 0; j < ip.Length; j++)
					{
						if (Math.Abs(ip[j].par1) < 1e-6 || Math.Abs(1.0 - ip[j].par1) < 1e-6)
						{
							badPoint = true;
							break;
						}
						if (ip[j].par1 > 0.0 && ip[j].par1 < 1)
						{
							if (Precision.SameDirection(segment[k].DirectionAt(ip[j].par1), dir, false))
							{
								badPoint = true;
								break;
							}
							if (ip[j].par2 > 0.5) ++forward;
							else ++backward;
						}
					}
					if (badPoint) break;
				}
				if (!badPoint && (forward & 0x01) == (backward & 0x01))
				{   // beide Schnitt-Anzahlen sind gerade oder beide ungerade, gut Lösung
					return forward;
				}
				angle += 1.0; // wird sich nie wiederholen
			}
			return 0;
		}
		/// <summary>
		/// Returnes true, when the provided point is inside the loop of curves.
		/// When the curves are oriented clockwise, true means outside (the hole)
		/// </summary>
		/// <param name="curves"></param>
		/// <param name="toTest"></param>
		/// <returns></returns>
		static public bool IsInside(ICurve2D[] curves, GeoPoint2D toTest)
		{
			int num = Border.GetBeamIntersectionCount(new List<ICurve2D>(curves), toTest);
			return Border.CounterClockwise(curves) ^ ((num & 0x01) == 0); // true: gegenUhrzeigersinn und Anzahl ungerade -oder- Uhrzeigersinn und Anzahl gerade
		}

		/// <summary>
		/// Returns a list of all intersectionpoints of this border with the given curve.
		/// Some intersectionpoints may be found twice, if the Curve passes through a vertex
		/// of this border. The par1 member of the intersectionpoint is the parameter
		/// of this border (0.0&lt;=par1&lt;=this.Count) the par2 member is the parameter
		/// of the curve (0.0&lt;=par1&lt;=1.0).
		/// </summary>
		/// <param name="IntersectWith">curve to intersect this border with</param>
		/// <returns>list of intersection points</returns>
		public GeoPoint2DWithParameter[] GetIntersectionPoints(ICurve2D IntersectWith)
		{
			return GetIntersectionPoints(IntersectWith, extent.Size * 1e-6);
		}
		public GeoPoint2DWithParameter[] GetIntersectionPoints(ICurve2D IntersectWith, double precision)
		{
			ArrayList result = new ArrayList();
			ICollection cl = QuadTree.GetObjectsCloseTo(IntersectWith);
			foreach (ICurve2D curve in cl)
			{
				GeoPoint2DWithParameter[] ips = curve.Intersect(IntersectWith);
				if (ips.Length == 0)
				{   // berührpunkte am Anfang bzw. Ende ggf noch dazugeben
				}
				int Index = (int)segmentToIndex[curve]; // das darf nie fehlschlagen
				bool added = false;
				for (int i = 0; i < ips.Length; ++i)
				{
					// die Abfrage ist halt sehr knapp. Da kann es evtl. passieren, dass ein Schnitt
					// genau durch eine Ecke nicht erwischt wird. Aber das Parameter Epsilon ist schwer
					// zu kriegen, es müsste bei ICurve2D implementiert werden
					// Verbesserung: da der Parameter jetzt auf 0..1 eingeschränkt ist, wäre die
					// Länge der Kurve bezogen auf Precision.eps ein ganz gutes Maß
					// Jetzt erstmal mit 1e-6 gearbeitet. Probleme traten vor allem bei den Umrisskanten (3D) auf
					// da diese zunächst auf das Border getrimmt werden und dann das Border aufteilen sollen,
					// d.h. immer Schnittpunkte genau am Anfang bzw. genau am Ende haben.
					if (ips[i].par1 >= 0.0 - 1e-6 && ips[i].par1 <= 1.0 + 1e-6 && ips[i].par2 >= 0.0 - 1e-6 && ips[i].par2 <= 1.0 + 1e-6)
					{
						ips[i].par1 += Index; // der Index für par2 ist hier unbekannt
						if (ips[i].par1 > Count && isClosed) ips[i].par1 -= Count; // Schnittpunkt ganz am Ende soll 0.0 liefern, sonst Probleme bei BorderOperation
						result.Add(ips[i]);
						added = true;
					}
				}
				if (!added)
				{   // wenn nichts zugefügt wurde noch die Endpunkte untersuchen
					// war früher weiter oben, aber da wurden ggf. Endpunkte als Schnitt gefunden und dann wieder aussortiert, da die Parameter nicht passten
					List<GeoPoint2DWithParameter> toAdd = new List<GeoPoint2DWithParameter>();
					double md = curve.MinDistance(IntersectWith.StartPoint);
					if (md < precision)
					{
						toAdd.Add(new GeoPoint2DWithParameter(IntersectWith.StartPoint, curve.PositionOf(IntersectWith.StartPoint), 0.0));
					}
					md = curve.MinDistance(IntersectWith.EndPoint);
					if (md < precision)
					{
						toAdd.Add(new GeoPoint2DWithParameter(IntersectWith.EndPoint, curve.PositionOf(IntersectWith.EndPoint), 1.0));
					}
					md = IntersectWith.MinDistance(curve.StartPoint);
					if (md < precision)
					{
						toAdd.Add(new GeoPoint2DWithParameter(curve.StartPoint, 0.0, IntersectWith.PositionOf(curve.StartPoint)));
					}
					md = IntersectWith.MinDistance(curve.EndPoint);
					if (md < precision)
					{
						toAdd.Add(new GeoPoint2DWithParameter(curve.EndPoint, 1.0, IntersectWith.PositionOf(curve.EndPoint)));
					}
					ips = toAdd.ToArray();
					for (int i = 0; i < ips.Length; i++)
					{
						ips[i].par1 += Index; // der Index für par2 ist hier unbekannt
						if (ips[i].par1 > Count && isClosed) ips[i].par1 -= Count; // Schnittpunkt ganz am Ende soll 0.0 liefern, sonst Probleme bei BorderOperation
						result.Add(ips[i]);
					}
				}
			}
			return (GeoPoint2DWithParameter[])result.ToArray(typeof(GeoPoint2DWithParameter));
		}
		public GeoPoint2DWithParameter[] GetIntersectionPoints(Border IntersectWith)
		{
			return GetIntersectionPoints(IntersectWith, extent.Size * 1e-6 + IntersectWith.extent.Size * 1e-6);
		}
		public GeoPoint2DWithParameter[] GetIntersectionPoints(Border IntersectWith, double precision)
		{
			ArrayList res = new ArrayList();
			for (int i = 0; i < IntersectWith.Count; ++i)
			{
				ICurve2D c = IntersectWith[i];
				GeoPoint2DWithParameter[] ips = GetIntersectionPoints(c, precision);
				for (int j = 0; j < ips.Length; ++j)
				{
					ips[j].par2 += i;
					if (IntersectWith.isClosed && ips[j].par2 >= IntersectWith.Count) ips[j].par2 -= IntersectWith.Count;
				}
				res.AddRange(ips);
			}
			return (GeoPoint2DWithParameter[])res.ToArray(typeof(GeoPoint2DWithParameter));
		}
		/// <summary>
		/// Returns an array of Border objects by breaking this border at the given positions.
		/// Parameter must be an ordered list of double values. Each value must be greater 0.0
		/// and less the number of iCurve2D objects in this Border.
		/// </summary>
		/// <param name="Parameter"></param>
		/// <returns></returns>
		public Border[] Split(double[] Parameter)
		{
			ArrayList res = new ArrayList();
			double lastPos = 0.0;
			for (int i = 0; i <= Parameter.Length; ++i)
			{   // die Schleife läuft um 1 weiter als Parameter.Length, um den letzten 
				// Abschnitt auch noch zu bekommen
				int StartIndex = (int)lastPos;
				double nextPos;
				if (i == Parameter.Length) nextPos = segment.Length;
				else nextPos = Parameter[i];
				int EndIndex = (int)nextPos;
				if (EndIndex >= segment.Length) EndIndex = segment.Length - 1;
				if (StartIndex > EndIndex)
				{   // Rundungsfehler am Ende, EndIndex wird ja ggf. verkleinert
				}
				else if (StartIndex == EndIndex)
				{   // nur ein Segment wird benötigt
					ICurve2D TheOnlyCurve = segment[StartIndex].Clone();
					TheOnlyCurve = TheOnlyCurve.Trim(lastPos - StartIndex, nextPos - StartIndex);
					if (TheOnlyCurve.Length > Precision.eps) res.Add(new Border(new ICurve2D[] { TheOnlyCurve }));
				}
				else
				{
					ArrayList curves = new ArrayList(EndIndex - StartIndex + 1);
					ICurve2D FirstCurve = segment[StartIndex].Clone();
					FirstCurve = FirstCurve.Trim(lastPos - StartIndex, 1.0);
					if (FirstCurve.Length > Precision.eps) curves.Add(FirstCurve);
					for (int j = StartIndex + 1; j < EndIndex; ++j)
					{
						ICurve2D InnerCurve = segment[j].Clone();
						if (InnerCurve.Length > Precision.eps) curves.Add(InnerCurve);
					}
					ICurve2D LastCurve = segment[EndIndex].Clone();
					LastCurve = LastCurve.Trim(0.0, nextPos - EndIndex);
					if (LastCurve.Length > Precision.eps) curves.Add(LastCurve);
					if (curves.Count > 0)
					{
						res.Add(new Border((ICurve2D[])(curves.ToArray(typeof(ICurve2D)))));
					}
				}
				lastPos = nextPos;
			}
			return (Border[])res.ToArray(typeof(Border));
		}
		/// <summary>
		/// Splits the border into parts according to <paramref name="positions"/>. 
		/// </summary>
		/// <param name="positions"></param>
		/// <returns></returns>
		private List<List<ICurve2D>> SplitCyclical(double[] positions)
		{
			List<List<ICurve2D>> res = new List<List<ICurve2D>>();
			for (int i = 0; i < positions.Length; i++)
			{
				double startPos = positions[i];
				double endPos = positions[(i + 1) % positions.Length];
				int startIndex = (int)Math.Floor(startPos);
				int endIndex = (int)Math.Floor(endPos);
				if (startIndex == endIndex)
				{
					if (startPos > endPos) endIndex += segment.Length; // crossing 0
				}
				else if (endIndex < startIndex) endIndex += segment.Length; // crossing 0
				List<ICurve2D> part = new List<ICurve2D>();
				startPos -= Math.Floor(startPos);
				endPos -= Math.Floor(endPos);
				for (int j = startIndex; j <= endIndex; j++)
				{
					int index = j % segment.Length;
					ICurve2D seg = segment[index];
					if (j == startIndex && j == endIndex)
					{
						ICurve2D trimmed = seg.Trim(startPos, endPos);
						if (trimmed != null) part.Add(trimmed);
					}
					else if (j == startIndex)
					{
						ICurve2D trimmed = seg.Trim(startPos, 1.0);
						if (trimmed != null) part.Add(trimmed);
					}
					else if (j == endIndex)
					{
						ICurve2D trimmed = seg.Trim(0.0, endPos);
						if (trimmed != null) part.Add(trimmed);
					}
					else
					{
						part.Add(seg.Clone());
					}
				}
				res.Add(part);
			}
			return res;
		}
		/// <summary>
		/// Identisch mit Split weiter oben, jedoch werden die Punkte noch nachgebessert durch die gegebenen GeoPoint2D
		/// </summary>
		/// <param name="parameter"></param>
		/// <param name="points"></param>
		/// <returns></returns>
		internal Border[] Split(double[] parameter, GeoPoint2D[] points, double[] orientation)
		{
			ArrayList res = new ArrayList();
			double lastPos = 0.0;
			for (int i = 0; i <= parameter.Length; ++i)
			{   // die Schleife läuft um 1 weiter als Parameter.Length, um den letzten 
				// Abschnitt auch noch zu bekommen
				int startIndex = (int)lastPos;
				if (startIndex == segment.Length) --startIndex;
				double nextPos;
				if (i == parameter.Length) nextPos = segment.Length;
				else nextPos = parameter[i];
				int endIndex = (int)nextPos;
				if (endIndex >= segment.Length) endIndex = segment.Length - 1;
				if (nextPos <= 0.0 || lastPos > segment.Length || lastPos > nextPos)
				{   // hinter dem Ende oder vor dem Anfang abschneiden
					// also nix tun
				}
				else if (startIndex == endIndex)
				{   // nur ein Segment wird benötigt
					ICurve2D theOnlyCurve = segment[startIndex].Clone();
					theOnlyCurve = theOnlyCurve.Trim(lastPos - startIndex, nextPos - startIndex);
					if (theOnlyCurve != null && theOnlyCurve.Length > Precision.eps)
					{
						if (i > 0) theOnlyCurve.StartPoint = points[i - 1]; // Punkte noch nachbessern
						if (i < parameter.Length) theOnlyCurve.EndPoint = points[i];
						theOnlyCurve.UserData.CloneFrom(segment[startIndex].UserData);

						Border toAdd = new Border(new[] { theOnlyCurve });
						if (i == 0)
						{
							if (orientation[i] > 1e-6) toAdd.orientation = Orientation.negative;
							else if (orientation[i] < -1e-6) toAdd.orientation = Orientation.positive;
							else toAdd.orientation = Orientation.unknown;
						}
						else if (i == parameter.Length)
						{
							if (orientation[i - 1] > 1e-6) toAdd.orientation = Orientation.positive;
							else if (orientation[i - 1] < -1e-6) toAdd.orientation = Orientation.negative;
							else toAdd.orientation = Orientation.unknown;
						}
						else
						{
							if (orientation[i] > 1e-6 && orientation[i - 1] < -1e-6) toAdd.orientation = Orientation.negative;
							else if (orientation[i] < -1e-6 && orientation[i - 1] > 1e-6) toAdd.orientation = Orientation.positive;
							else toAdd.orientation = Orientation.unknown;
						}
						res.Add(toAdd);
					}
				}
				else
				{
					ArrayList curves = new ArrayList(endIndex - startIndex + 1);
					ICurve2D firstCurve = segment[startIndex].Clone();
					firstCurve = firstCurve.Trim(lastPos - startIndex, 1.0);
					firstCurve.UserData.CloneFrom(segment[startIndex].UserData);
					if (i > 0) firstCurve.StartPoint = points[i - 1];
					if (firstCurve.Length > Precision.eps) curves.Add(firstCurve);
					for (int j = startIndex + 1; j < endIndex; ++j)
					{
						ICurve2D innerCurve = segment[j].Clone();
						innerCurve.UserData.CloneFrom(segment[j].UserData);
						if (innerCurve.Length > Precision.eps) curves.Add(innerCurve);
					}
					ICurve2D lastCurve = segment[endIndex].Clone();
					lastCurve = lastCurve.Trim(0.0, nextPos - endIndex);
					lastCurve.UserData.CloneFrom(segment[endIndex].UserData);
					if (i < parameter.Length) lastCurve.EndPoint = points[i];
					if (lastCurve.Length > Precision.eps) curves.Add(lastCurve);
					if (curves.Count > 0)
					{
						Border toAdd = new Border((ICurve2D[])(curves.ToArray(typeof(ICurve2D))));
						if (i == 0)
						{
							if (orientation[i] > 1e-6) toAdd.orientation = Orientation.negative;
							else if (orientation[i] < -1e-6) toAdd.orientation = Orientation.positive;
							else toAdd.orientation = Orientation.unknown;
						}
						else if (i == parameter.Length)
						{
							if (orientation[i - 1] > 1e-6) toAdd.orientation = Orientation.positive;
							else if (orientation[i - 1] < -1e-6) toAdd.orientation = Orientation.negative;
							else toAdd.orientation = Orientation.unknown;
						}
						else
						{
							if (orientation[i] > 1e-6 && orientation[i - 1] < -1e-6) toAdd.orientation = Orientation.negative;
							else if (orientation[i] < -1e-6 && orientation[i - 1] > 1e-6) toAdd.orientation = Orientation.positive;
							else toAdd.orientation = Orientation.unknown;
						}
						res.Add(toAdd);
					}
				}
				lastPos = nextPos;
			}
			return (Border[])res.ToArray(typeof(Border));
		}
		/// <summary>
		/// Returns the position of this point on the border.
		/// The position is the index of the closest segment plus the position on this segment
		/// </summary>
		/// <param name="p">the point for which the position is returned</param>
		/// <returns>the position between 0 and the number of segments</returns>
		public double GetParameter(GeoPoint2D p)
		{
			double mindist = double.MaxValue;
			double res = double.MaxValue;
			for (int i = 0; i < segment.Length; i++)
			{
				double d = segment[i].MinDistance(p);
				if (d < mindist)
				{
					double pos = segment[i].PositionOf(p);
					if (segment[i].IsParameterOnCurve(pos))
					{
						mindist = d;
						res = i + pos;
					}
				}
			}
			if (res != double.MaxValue)
			{
				if (res < 0) res += segment.Length;
				if (res >= segment.Length) res -= segment.Length;
				return res;
			}
			throw new BorderException("Border.GetParameter: point not on Border", BorderException.BorderExceptionType.PointNotOnBorder);
		}
		public GeoPoint2D PointAt(double par)
		{
			int ind = (int)Math.Floor(par);
			if (ind >= segment.Length) return EndPoint;
			if (ind < 0) return StartPoint;
			return segment[ind].PointAt(par - ind);
		}
		public GeoVector2D DirectionAt(double par)
		{
			int ind = (int)Math.Floor(par);
			if (ind >= segment.Length) return segment[segment.Length - 1].EndDirection;
			if (ind < 0) return segment[0].StartDirection;
			if (Math.Abs(par - ind) < 1e-7 && ind > 0)
			{   // Zwischenwert beider segmente nehmen, wenn sehr nahe beim Eckpunkt. das ist wichtig für Borderoperation
				// um die Schnittrichtung zu bestimmen
				return 0.5 * (segment[ind].StartDirection + segment[ind - 1].EndDirection);
			}
			return segment[ind].DirectionAt(par - ind);
		}
		public double GetMinDistance(Border Other)
		{
			double minDist = double.MaxValue;
			for (int i = 0; i < segment.Length; ++i)
			{
				for (int j = 0; j < Other.segment.Length; ++j)
				{
					minDist = Math.Min(minDist, segment[i].MinDistance(Other.segment[j]));
				}
			}
			return minDist;
		}
		public double GetMinDistance(GeoPoint2D p)
		{
			double minDist = double.MaxValue;
			for (int i = 0; i < segment.Length; ++i)
			{
				minDist = Math.Min(minDist, segment[i].MinDistance(p));
			}
			return minDist;
		}
		public GeoPoint2D StartPoint
		{
			get { return segment[0].StartPoint; }
		}
		public GeoPoint2D EndPoint
		{
			get { return segment[segment.Length - 1].EndPoint; }
		}
		/// <summary>
		/// Concatenates two border objects. Both borders must be open (not closed) and
		/// the endpoint of the first border must be equal to the startpoint of the second 
		/// border (as defined by Precision.IsEqual).
		/// </summary>
		/// <param name="first"></param>
		/// <param name="second"></param>
		/// <returns></returns>
		public static Border operator +(Border first, Border second)
		{
			if (!Precision.IsEqual(first.EndPoint, second.StartPoint)) throw new BorderException("Border operator +: Borders not connected", BorderException.BorderExceptionType.NotConnected);
			ICurve2D[] segments = new ICurve2D[first.segment.Length + second.segment.Length];
			first.segment.CopyTo(segments, 0);
			second.segment.CopyTo(segments, first.segment.Length);
			return new Border(segments);
		}
		internal static Border Concat(Border first, Border second)
		{
			if (!Precision.IsEqual(first.EndPoint, second.StartPoint))
			{
				first.segment[first.segment.Length - 1].EndPoint = second.segment[0].StartPoint;
			}
			ICurve2D[] segments = new ICurve2D[first.segment.Length + second.segment.Length];
			first.segment.CopyTo(segments, 0);
			second.segment.CopyTo(segments, first.segment.Length);
			return new Border(segments);
		}
		internal static bool InsertParallelConnection(ICurve2D first, ICurve2D second, GeoVector2D enddir, GeoVector2D startdir, double dist, GeoPoint2D vertex, List<ICurve2D> curves, bool roundCorners)
		{   // fügt in die gegebene Liste einen Kreisbogen oder zwei Linien ein, wenn es sich um einen
			// "Außenknick" handelt. Wenn es tangential geht, dann werden die beiden Kurven zusammengeruckelt,
			// Ansonsten passiert nichts
			// Das Ergebnis besagt, ob es einen Zusammenhang gibt oder nicht
			SweepAngle sw = new SweepAngle(enddir, startdir);
			SweepAngle sw1 = new SweepAngle(first.EndDirection, second.StartDirection);
			//if (Precision.IsNull(sw))
			if (Math.Abs(sw.Radian) < 0.05) // war: 1e-2), geht bei _51.drw besser
			{   // es geht in gleicher Richtung weiter
				// die beiden entstandenen Kurven miteinander verbinden
				// die Abfrage IsNull ist zu streng, bei fast tangentialen Übergängen, die als Innenknick abgehandelt
				// werden gibt es u.U. keine Schnittpunkte aber zu große Lücken
				if (Math.Abs(Math.Abs(sw1.Radian) - Math.PI) < 1e-5) return false; // war 1e-3
				if (Curves2D.Connect(first, second)) return true;
				// Innenknick aber kein Zusammenhang: Linie einfügen, es geht nur um einen kleinen Fehler
				Line2D l1 = new Line2D(first.EndPoint, second.StartPoint);
				l1.UserData.Add("CADability.Border.Successor", first); // bei den Spitzen muss bekannt sein, was sie verlängern
				curves.Add(l1); // diese beiden Linien bilden eine Spitze
				return true;
			}
			if (dist * sw > 0.0)
			{   // Außenknick: Bogen oder Spitze einfügen
				if (!roundCorners)
				{
					GeoPoint2D intp;
					if (Geometry.IntersectLL(first.EndPoint, first.EndDirection, second.StartPoint, second.StartDirection, out intp))
					{
						Line2D l1 = new Line2D(first.EndPoint, intp);
						Line2D l2 = new Line2D(intp, second.StartPoint);
						l1.UserData.Add("CADability.Border.Successor", first); // bei den Spitzen muss bekannt sein, was sie verlängern
						l2.UserData.Add("CADability.Border.Predecessor", second);
						curves.Add(l1); // diese beiden Linien bilden eine Spitze
						curves.Add(l2);
						return true;
					}
					// wenns nicht klappt (tangentiale Spitze z.B.), dann halt Bogen einfügen
				}
				Arc2D a = new Arc2D(vertex, Math.Abs(dist), first.EndPoint, second.StartPoint, dist > 0.0);
				if (a.Length > Precision.eps) curves.Add(a); // den Bogen dazu
#if DEBUG
				DebuggerContainer dc1 = new DebuggerContainer();
				dc1.Add(first, System.Drawing.Color.Red, 0);
				dc1.Add(second, System.Drawing.Color.Green, 1);
				dc1.Add(a, System.Drawing.Color.Blue, 2);
#endif
				return true;
			}
			else
			{
				return false; // kein Zusammenhang, nichts gemacht
			}
		}
		private class BorderInQuadTree : IQuadTreeInsertable
		{
			public Border border;
			public BorderInQuadTree(Border border)
			{
				this.border = border;
			}
			#region IQuadTreeInsertable Members

			BoundingRect IQuadTreeInsertable.GetExtent()
			{
				return new BoundingRect(border.StartPoint, border.EndPoint);
			}

			bool IQuadTreeInsertable.HitTest(ref BoundingRect rect, bool includeControlPoints)
			{
				return rect.Contains(border.StartPoint) || rect.Contains(border.EndPoint);
			}

			object IQuadTreeInsertable.ReferencedObject
			{
				get { throw new Exception("The method or operation is not implemented."); }
			}

			#endregion
		}
		/* NEUES KONZEPT:
         * Mache aus der Border ein CompoundShape objekt 
         * Für negative dist: entferne davon für jedes Segment
         * das entprechende Stück (paralleles Segment, Ecken verbinden)
         * Für jeden Innenknick entferne zusätzlich das "Tortenstück".
         * Für positive dist: Füge das entsprechend Stück hinzu und für die Ecken auch das "Tortenstück"
         * Statt Tortenstück ggf. auch Rhombus (rechtwinkliges Drachenviereck).
         * Indizierung der Segmente nicht vergessen und bei den boolschen Operationen beibehalten.
        */
		/// <summary>
		/// Yields a set of borders that are parallel to this border with a given distance. Positive
		/// distance yields a border to the right side (or outside if closed) of this border, negative
		/// to the left side or inside. In some cases there is no soltuion (e.g. an inside parallel
		/// border of a closed border must have a distance less than half of the diameter of the border)
		/// In other cases there may be a set of solutions, e.g. if the border is not convex.
		/// </summary>
		/// <param name="dist">the distance of the desired parallel border</param>
		/// <returns>array of parallel borders (maybe empty)</returns>
		public Border[] GetParallel(double dist, bool approxSpline, double precision, double roundAngle)
		{
			/* Das Konzept ist schlecht:
             * Besser: Verbinde alles äußere mit einem Bogen, das tangentiale muss sichergestellt zusammenhängen
             * der Rest kann offen bleiben. Alles in einen QuadTree werfen und Schnittpunkte der einzelnen Pfade miteinander 
             * berechnen. Anhand der Richtungen in einem Schnittpunkt bestimmen was wegfallen muss. Den Rest liefern.
             */
			// precision bedeutet Genauigkeit in anbhängigkeit von dist
			if (precision == 0.0) precision = 1e-3; // ein tausendstel von dist

			// Winzige Teilstücke in der Umrandung stören und erzeugen großen Aufwand
			List<ICurve2D> toIterate = new List<ICurve2D>(segment);
			for (int i = toIterate.Count - 2; i >= 1; --i)
			{
				if (toIterate[i].Length < Math.Abs(dist * precision))
				{
					GeoPoint2D p = toIterate[i].PointAt(0.5);
					toIterate[i - 1].EndPoint = p;
					toIterate[i + 1].StartPoint = p;
					toIterate.RemoveAt(i);
				}
			}
			// Polylinien und Pfade stören
			for (int i = toIterate.Count - 1; i >= 0; --i)
			{
				if (toIterate[i] is Polyline2D)
				{
					ICurve2D[] sub = (toIterate[i] as Polyline2D).GetSubCurves();
					toIterate.RemoveAt(i);
					toIterate.InsertRange(i, sub);
				}
				else if (toIterate[i] is Path2D)
				{
					ICurve2D[] sub = (toIterate[i] as Path2D).SubCurves;
					toIterate.RemoveAt(i);
					toIterate.InsertRange(i, sub);
				}
				else if (toIterate[i] is BSpline2D && approxSpline)
				{
					ICurve2D aprx = toIterate[i].Approximate(true, precision);
					toIterate.RemoveAt(i);
					if (aprx is Polyline2D)
					{
						ICurve2D[] sub = (aprx as Polyline2D).GetSubCurves();
						toIterate.InsertRange(i, sub);
					}
					else if (aprx is Path2D)
					{
						ICurve2D[] sub = (aprx as Path2D).SubCurves;
						toIterate.InsertRange(i, sub);
					}
				}
			}
			for (int i = 0; i < toIterate.Count; ++i)
			{
				int ind = i - 1;
				if (ind >= 0 || this.isClosed)
				{
					if (ind < 0) ind = toIterate.Count - 1;
					// double dbg = toIterate[ind].EndPoint | toIterate[i].StartPoint;
					toIterate[ind].EndPoint = toIterate[i].StartPoint;
				}
			}
			// 1.: Alle parallelen Segmente erstellen, ggf. Bögen oder Spitzen einfügen
			List<ICurve2D> contcurves = new List<ICurve2D>(); // zusammenhängende Curves2D
			List<Path2D> segmentparts = new List<Path2D>(); // einzelne Segmente der Parallelen (Path2D)
			for (int i = 0; i < toIterate.Count; ++i)
			{
				ICurve2D c = toIterate[i].Parallel(dist, approxSpline, Math.Abs(dist * precision), roundAngle); // das parallele
				c.UserData.CloneFrom(toIterate[i].UserData); // damit wird ermöglicht eine Indizierung der Seiten auf die parallelen zu propagieren
				ICurve2D lastCurve = null;
				if (contcurves.Count > 0) lastCurve = contcurves[contcurves.Count - 1];
				if (lastCurve != null)
				{
					// double a = Math.PI - new Angle(lastCurve.EndDirection, c.StartDirection);
					double a = Math.PI - new Angle(toIterate[i - 1].EndDirection, toIterate[i].StartDirection);
					bool round = a < roundAngle; // wir suchen den Innenwinkel
					if (InsertParallelConnection(lastCurve, c, toIterate[i - 1].EndDirection, toIterate[i].StartDirection, dist, toIterate[i].StartPoint, contcurves, round))
					{   // es gab einen Zusammenhang. Entweder wurde ein Verbindungsobjekt zwischen lastCurve und c 
						// zugefügt, oder die beiden Kurven (lastCurve,c) wurden zurechtgeruckelt
						if (c.Length > Precision.eps) contcurves.Add(c);
					}
					else
					{   // es gab keinen Zusammenhang, ein neuer Pfad muss angefangen werden
						Path2D p2d = new Path2D(contcurves.ToArray(), true);
						segmentparts.Add(p2d);
						contcurves.Clear();
						if (c.Length > Precision.eps) contcurves.Add(c);
					}
				}
				else
				{   // die erste Kurve in einem neuen Segment
					if (c.Length > Precision.eps) contcurves.Add(c);
				}
			}
			if (contcurves.Count > 0)
			{   // den letzten Pfad erzeugen und zufügen, müsste eigentlich immer drankommen
				if (isClosed)
				{   // wenn Ursprungsborder geschlossen, dan ggf. noch Bogen bzw. Linien einfügen
					ICurve2D c1 = contcurves[contcurves.Count - 1];
					ICurve2D c2 = null;
					if (segmentparts.Count > 0)
					{
						Path2D pp2d = segmentparts[0];
						c2 = pp2d[0];
					}
					else
					{
						c2 = contcurves[0];
					}
					double a = Math.PI - new Angle(toIterate[toIterate.Count - 1].EndDirection, toIterate[0].StartDirection);
					bool round = a < roundAngle; ;
					InsertParallelConnection(c1, c2, toIterate[toIterate.Count - 1].EndDirection, toIterate[0].StartDirection, dist, toIterate[toIterate.Count - 1].EndPoint, contcurves, round);
					// hier ist egal, ob es geklappt hat oder nicht
				}
				Path2D p2d = new Path2D(contcurves.ToArray(), true);
				segmentparts.Add(p2d);
			}
#if DEBUG
			DebuggerContainer dc1 = new DebuggerContainer();
			//for (int i = 0; i < contcurves.Count; ++i)
			//{
			//    dc1.Add(contcurves[i], System.Drawing.Color.Red, i);
			//}
			for (int i = 0; i < segmentparts.Count; ++i)
			{
				System.Drawing.Color clr;
				if ((i & 1) == 0)
				{
					clr = System.Drawing.Color.Blue;
				}
				else
				{
					clr = System.Drawing.Color.Red;
				}
				dc1.Add(segmentparts[i], clr, i);
			}
			for (int i = 0; i < segment.Length; ++i)
			{
				dc1.Add(segment[i], System.Drawing.Color.Green, i);
			}
#endif

			// die gefundenen Pfade zu Border machen, um diese effektiv zu verschneiden
			List<Border> RawSegemntsList = new List<Border>();
			for (int i = 0; i < segmentparts.Count; ++i)
			{
				Path2D p2d = segmentparts[i] as Path2D;
				bool reversed;
				Border bdr = p2d.MakeBorder(out reversed);
				bdr.RemoveSmallSegments(Math.Abs(dist / 100));
				if (!reversed) RawSegemntsList.Add(bdr);
			}

			Border[] RawSegments = RawSegemntsList.ToArray();
			SortedDictionary<double, GeoPoint2D>[] BreakSegment = new SortedDictionary<double, GeoPoint2D>[RawSegments.Length];
			SortedDictionary<double, double>[] intersectionDirection = new SortedDictionary<double, double>[RawSegments.Length];
			// BreakSegment: für jedes RawSegment eine Liste der Unterbrechungspunkte
			// wobei jeweils der Parameterwert verbunden ist mit dem (genaueren) Punkt selbst
			for (int i = 0; i < RawSegments.Length; ++i)
			{
				BreakSegment[i] = new SortedDictionary<double, GeoPoint2D>();
				intersectionDirection[i] = new SortedDictionary<double, double>();
			}
			// return RawSegments; // DEBUG!!!

			// RawSegments sind also die Parallelen zu den konvexen Teilstücken der ursprünglichen Border.
			// Diese werden jetzt alle miteinander in Beziehung gesetzt und an allen Schnittpunkten
			// aufgebochen.

			// alle RawSegments miteinander schneiden
			for (int i = 0; i < RawSegments.Length; ++i)
			{
				Path2D pathi = RawSegments[i].AsPath();
				double[] selfintpts = (pathi as ICurve2D).GetSelfIntersections();
				for (int j = 0; j < selfintpts.Length; ++j)
				{
					BreakSegment[i][selfintpts[j] * pathi.SubCurvesCount] = (pathi as ICurve2D).PointAt(selfintpts[j]);
					GeoVector2D dir = (1.0 / pathi.Length) * (pathi as ICurve2D).DirectionAt(selfintpts[j]);
					GeoVector2D other; // es sind immer zwei aufeinanderfolgende Parameterwerte, die zum selben Schnittpunkt gehören
					if ((j & 0x01) == 0) other = (1.0 / pathi.Length) * (pathi as ICurve2D).DirectionAt(selfintpts[j + 1]);
					else other = (1.0 / pathi.Length) * (pathi as ICurve2D).DirectionAt(selfintpts[j - 1]);
					intersectionDirection[i][selfintpts[j] * pathi.SubCurvesCount] = GeoVector2D.Orientation(dir, other);
					// die Parameterzähling läuft im Pfad von 0 bis 1, im BreakSegment von 0 bis n
					// GetSelfIntersections könnte auch den Schnittpunkt mitliefern
				}
				for (int j = i + 1; j < RawSegments.Length; ++j)
				{
					GeoPoint2DWithParameter[] pointlist = RawSegments[i].GetIntersectionPoints(RawSegments[j]);
					for (int k = 0; k < pointlist.Length; ++k)
					{
						if (((pointlist[k].p | RawSegments[i].StartPoint) < Math.Abs(dist * precision)) && ((pointlist[k].p | RawSegments[j].EndPoint) < Math.Abs(dist * precision)) ||
						((pointlist[k].p | RawSegments[i].EndPoint) < Math.Abs(dist * precision)) && ((pointlist[k].p | RawSegments[j].StartPoint) < Math.Abs(dist * precision)))
						{   // der Schnittpunkt ist in Wahrheit ein Zusammenhang am Anfang oder Ende
							if ((Math.Floor(pointlist[k].par1) == 0 || Math.Floor(pointlist[k].par1) == RawSegments[i].Count - 1) &&
								(Math.Floor(pointlist[k].par2) == 0 || Math.Floor(pointlist[k].par2) == RawSegments[j].Count - 1))
							{   // aber es darf auch ein noch so kleines Stück vom Border nicht überstehen
								continue;
							}
						}
						BreakSegment[i][pointlist[k].par1] = pointlist[k].p;
						BreakSegment[j][pointlist[k].par2] = pointlist[k].p;
						GeoVector2D dir = (1.0 / RawSegments[i].Length) * RawSegments[i].DirectionAt(pointlist[k].par1);
						GeoVector2D other = (1.0 / RawSegments[j].Length) * RawSegments[j].DirectionAt(pointlist[k].par2);
						double or = GeoVector2D.Orientation(dir, other);
						intersectionDirection[i][pointlist[k].par1] = or;
						intersectionDirection[j][pointlist[k].par2] = -or;
					}
				}
				if (!isClosed)
				{   // einen Kreis um Anfang und Ende
					Arc2D scap, ecap;
					if (dist > 0)
					{
						scap = new Arc2D(RawSegments[i].StartPoint, dist, RawSegments[i].segment[0].StartDirection.ToRight().Angle, 2.0 * Math.PI);
						ecap = new Arc2D(RawSegments[i].EndPoint, dist, RawSegments[i].segment[RawSegments[i].segment.Length - 1].StartDirection.ToRight().Angle, 2.0 * Math.PI);
					}
					else
					{
						scap = new Arc2D(RawSegments[i].StartPoint, -dist, RawSegments[i].segment[0].StartDirection.ToLeft().Angle, -2.0 * Math.PI);
						ecap = new Arc2D(RawSegments[i].EndPoint, -dist, RawSegments[i].segment[RawSegments[i].segment.Length - 1].StartDirection.ToLeft().Angle, -2.0 * Math.PI);
					}
					GeoPoint2DWithParameter[] pointlist = RawSegments[i].GetIntersectionPoints(scap);
					for (int k = 0; k < pointlist.Length; ++k)
					{
						if (((pointlist[k].p | RawSegments[i].StartPoint) < Math.Abs(dist * precision)) ||
						((pointlist[k].p | RawSegments[i].EndPoint) < Math.Abs(dist * precision)))
						{   // der Schnittpunkt ist in Wahrheit ein Zusammenhang am Anfang oder Ende
							if ((Math.Floor(pointlist[k].par1) == 0 || Math.Floor(pointlist[k].par1) == 1) &&
								(Math.Floor(pointlist[k].par2) == 0 || Math.Floor(pointlist[k].par2) == 1))
							{   // aber es darf auch ein noch so kleines Stück vom Border nicht überstehen
								continue;
							}
						}
						BreakSegment[i][pointlist[k].par1] = pointlist[k].p;
						GeoVector2D dir = (1.0 / RawSegments[i].Length) * RawSegments[i].DirectionAt(pointlist[k].par1);
						GeoVector2D other = (1.0 / scap.Length) * scap.DirectionAt(pointlist[k].par2);
						double or = GeoVector2D.Orientation(dir, other);
						intersectionDirection[i][pointlist[k].par1] = 0.0;
					}
					pointlist = RawSegments[i].GetIntersectionPoints(ecap);
					for (int k = 0; k < pointlist.Length; ++k)
					{
						if (((pointlist[k].p | RawSegments[i].StartPoint) < Math.Abs(dist * precision)) ||
						((pointlist[k].p | RawSegments[i].EndPoint) < Math.Abs(dist * precision)))
						{   // der Schnittpunkt ist in Wahrheit ein Zusammenhang am Anfang oder Ende
							if ((Math.Floor(pointlist[k].par1) == 0 || Math.Floor(pointlist[k].par1) == 1) &&
								(Math.Floor(pointlist[k].par2) == 0 || Math.Floor(pointlist[k].par2) == 1))
							{   // aber es darf auch ein noch so kleines Stück vom Border nicht überstehen
								continue;
							}
						}
						BreakSegment[i][pointlist[k].par1] = pointlist[k].p;
						GeoVector2D dir = (1.0 / RawSegments[i].Length) * RawSegments[i].DirectionAt(pointlist[k].par1);
						GeoVector2D other = (1.0 / ecap.Length) * ecap.DirectionAt(pointlist[k].par2);
						double or = GeoVector2D.Orientation(dir, other);
						intersectionDirection[i][pointlist[k].par1] = 0.0;
					}
				}
			}
			List<Border> BrokenSegments = new List<Border>();
			for (int i = 0; i < BreakSegment.Length; ++i)
			{
				if (BreakSegment[i].Count > 0)
				{
					// BreakSegment[i].Sort(); // alleListen aufsteigend sortieren
					// und die an diesen Stellen aufgebrochenen Borders aufsammeln
					List<double> parameters = new List<double>(BreakSegment[i].Keys);
					List<GeoPoint2D> points = new List<GeoPoint2D>(BreakSegment[i].Values);
					List<double> orientations = new List<double>(intersectionDirection[i].Values);
					Border[] splitted = RawSegments[i].Split(parameters.ToArray(), points.ToArray(), orientations.ToArray());
					BrokenSegments.AddRange(splitted);
				}
				else
				{   // z.B. ein einfacher Kreis, der hat keine unterbrechung und keine Selbstüberschneidung
					BrokenSegments.Add(RawSegments[i]);
				}
			}
			// return (Border [])BrokenSegments.ToArray(typeof(Border)); debug

			// PROBLEM: angenäherte Splines befinden sich zu nahe an der Originalkurve und werden im folgenden
			// entfernt. Das darf nicht passieren...
			// VERBESSERUNG: BrokenSegments, die keinen Zusammenhang an beiden Enden mit anderen haben
			// könnten eigentlich verworfen werden. Das würde die kommende Schleife entlasten und zu 
			// höherer Sicherheit führen.
			// BrokenSegments sind jetzt alle Teilstücke der RawSegments an ihren Schnittpunkten 
			// aufgebrochen. Jetzt werden alle Stücke daraufhin untersucht, ob sie zu nahe an der 
			// Ausgangs-Border liegen. Die zu nahe liegenden werden verworfen.
#if DEBUG
			DebuggerContainer dc2 = new DebuggerContainer();
			for (int i = 0; i < BrokenSegments.Count; ++i)
			{
				if (BrokenSegments[i].orientation == Orientation.positive) dc2.Add(BrokenSegments[i], System.Drawing.Color.Red, i);
				else if (BrokenSegments[i].orientation == Orientation.negative) dc2.Add(BrokenSegments[i], System.Drawing.Color.Green, i);
				else dc2.Add(BrokenSegments[i], System.Drawing.Color.Black, i);

			}
			for (int i = 0; i < segment.Length; ++i)
			{
				dc2.Add(segment[i], System.Drawing.Color.BlueViolet, i);
			}
#endif
			if (BrokenSegments.Count > 1)
			{   // wenn es nur ein einziges gibt, dann besteht das original Border auch nur aus einem Objekt und die Parallele
				// ist nicht selbstüberschneidend. Dann auf jeden fall behalten
				Border bdrForDist = new Border(toIterate.ToArray()); // border to check distance (because this Border may have been approximated)
				for (int i = BrokenSegments.Count - 1; i >= 0; --i)
				{   // die Orientierung gibt an, was dazugehört und was nicht. Es können aber vielleicht bei
					// Mehrfachüberschneidungen auch noch Reste als OK durchgehen, die in Wirklichkeit nicht ok sind.
					// Das muss noch getestet werden
					Border tst = BrokenSegments[i];
					if (((dist < 0.0) && (tst.orientation == Orientation.positive)) || ((dist > 0.0) && (tst.orientation == Orientation.negative)))
					{
						BrokenSegments.RemoveAt(i);
					}
					else if (tst.orientation == Orientation.unknown)
					{   // Aufwendiger Test mit Abstand zum Rand
						//BrokenSegments.RemoveAt(i); // mal testweise nicht verwenden

						//double dbg = bdrForDist.GetMinDistance(tst);
						if (bdrForDist.GetMinDistance(tst) < Math.Abs(dist * (1 - 2 * precision)))
						{
							// double dbg = GetMinDistance(tst);
							BrokenSegments.RemoveAt(i);
						}
						else if (tst.Length < Math.Abs(dist * precision)) // war dist * 10, gibt aber Fehler bei Tasche nicht geräumt 3.drw
						{   // winzigste Sstückchen stören
							BrokenSegments.RemoveAt(i);
						}
					}
				}
			}
#if DEBUG
			DebuggerContainer dc2a = new DebuggerContainer();
			for (int i = 0; i < BrokenSegments.Count; ++i)
			{
				dc2a.Add(BrokenSegments[i], System.Drawing.Color.Red, i);
			}
#endif
			// Zusatz: wenn geschlossen, dann müssen alle BrokenSegments einen Anschluss an beiden Enden haben
			// wenn nicht, fliegen sie raus. Bei offenen kann der erste und letzte frei enden
			//int firsti, lasti;
			//if (IsClosed)
			//{
			//    firsti = 0;
			//    lasti = BrokenSegments.Count - 1;
			//}
			//else
			//{
			//    firsti = 1;
			//    lasti = BrokenSegments.Count - 2;
			//}
			//for (int i = lasti; i >= firsti; --i)
			//{
			//    Border bdr = BrokenSegments[i];
			//    if (bdr.IsClosed) continue;
			//    bool startConnection = false;
			//    bool endConnection = false;
			//    for (int j = 0; j < BrokenSegments.Count; ++j)
			//    {
			//        if (i != j)
			//        {
			//            if ((BrokenSegments[j].EndPoint | bdr.StartPoint) < Math.Abs(dist * precision))
			//                startConnection = true;
			//            if ((BrokenSegments[j].StartPoint | bdr.EndPoint) < Math.Abs(dist * precision))
			//                endConnection = true;
			//            //if (Precision.IsEqual(BrokenSegments[j].EndPoint, bdr.StartPoint))
			//            //    startConnection = true;
			//            //if (Precision.IsEqual(BrokenSegments[j].StartPoint, bdr.EndPoint))
			//            //    endConnection = true;
			//            if (startConnection && endConnection) continue;
			//        }
			//    }
			//    if (!startConnection || !endConnection) BrokenSegments.RemoveAt(i);
			//}
#if DEBUG
			DebuggerContainer dc3 = new DebuggerContainer();
			for (int i = 0; i < BrokenSegments.Count; ++i)
			{
				dc3.Add(BrokenSegments[i], System.Drawing.Color.Red, i);
			}
			for (int i = 0; i < segment.Length; ++i)
			{
				dc3.Add(segment[i], System.Drawing.Color.Green, i);
			}
#endif
			// Die übrig gebliebenen Teilstücke werden zu zusammenhängenden Border Objekten
			// zusammengefasst und als Ergebnis abgelegt. Hier kann man sicher sein, dass
			// alle Teilstücke bereits die richtige Orientierung haben.

			// das Problem: kleinste Reststücke führen die Zusammenknüpfung in die Irre:
			// wir dürfen nicht irgendeinen Anschluss nehmen, der passt, sondern den besten
			// dazu einen Quadtree, der den schnellen Zugriff über die STart- bzw. Endpunkte einer Border erlaubt
			QuadTree<BorderInQuadTree> bdrquad = new QuadTree<BorderInQuadTree>(extent);
			for (int i = 0; i < BrokenSegments.Count; ++i)
			{
				if (BrokenSegments[i].Length > Math.Abs(dist * precision))
				{   // Kleinkram weglassen, der wird sowieso überbrückt
					// Problem nur wenn zwei kleine zusammenhängen und damit größer werden als die Grenze
					bdrquad.AddObject(new BorderInQuadTree(BrokenSegments[i]));
				}
			}
			// zuerst alle Borders, die nicht an beiden Seiten einen Anschluss haben rauswerfen
			List<BorderInQuadTree> toRemove = new List<BorderInQuadTree>();
			do
			{
				// wenn welche rausgeworfen wurden, dann muss man nochmal durchlaufen
				// Es ist wahrscheinlich besser mehrere Runden durchzulaufen und zu sammeln als
				// bei jedem gefundenen von vorne anzufangen, oder?
				toRemove.Clear();
				foreach (BorderInQuadTree biq in bdrquad.AllObjects())
				{
					if (IsClosed || (biq.border != BrokenSegments[0] && biq.border != BrokenSegments[BrokenSegments.Count - 1]))
					{
						if (((biq.border.EndPoint | biq.border.StartPoint) < Math.Abs(dist * precision))) continue; // geschlossen, nicht verwerfen
						bool startConnection = false;
						bool endConnection = false;
						foreach (BorderInQuadTree biq1 in bdrquad.ObjectsCloseTo(biq))
						{
							if (biq != biq1)
							{
								if (!endConnection && ((biq.border.EndPoint | biq1.border.StartPoint) < Math.Abs(dist * precision))) endConnection = true;
								if (!startConnection && ((biq.border.StartPoint | biq1.border.EndPoint) < Math.Abs(dist * precision))) startConnection = true;
							}
							if (startConnection && endConnection) break;
						}
						if (!startConnection || !endConnection)
						{
							toRemove.Add(biq);
						}
					}
				}
#if DEBUG
				DebuggerContainer dc3a = new DebuggerContainer();
				int oddc = 0;
				foreach (BorderInQuadTree biq in toRemove)
				{
					if ((oddc & 0x1) == 0)
						dc3a.Add(biq.border, System.Drawing.Color.Red, 0);
					else
						dc3a.Add(biq.border, System.Drawing.Color.Blue, 0);
					++oddc;
				}
#endif
				bdrquad.RemoveObjects(toRemove);
			} while (toRemove.Count > 0);

#if DEBUG
			DebuggerContainer dc4 = new DebuggerContainer();
			foreach (BorderInQuadTree biq in bdrquad.AllObjects())
			{
				if (biq.border.orientation == Orientation.unknown)
					dc4.Add(biq.border, System.Drawing.Color.Black, 0);
				else if (biq.border.orientation == Orientation.positive)
					dc4.Add(biq.border, System.Drawing.Color.Red, 0);
				else
					dc4.Add(biq.border, System.Drawing.Color.Green, 0);
			}
			for (int i = 0; i < segment.Length; ++i)
			{
				dc4.Add(segment[i], System.Drawing.Color.Blue, i);
			}
#endif
			// Versuche die segmente an ihren Endpunkten zu verbinden. zuerst nur die "guten" also richtig orientierten,
			// dann auch die mit unbekannter Orientierung
			bool connected = false;
			List<Border> res = new List<Border>();
			do
			{   // 1. Durchlauf: nur die richtig orientierten segmente verwenden
				// folgendes Scenario machte Probleme:
				// eine schöne große geschlossene Border existiert und an einem Eckpunkt eine winzige fast geschlossene
				// fast am Eckpunkt anhängende Border, die auch richtig orientiert ist, aber zu nahe am Rand liegt.
				// Wir dürfen nicht mit dem Schnipsel anfangen, sonst wird alles in eine Border verbunden und
				// landet dann im Aus, weil zu nahe am Rand. Deshalb mit den großen anfangen
				// das machts natürlich langsam
				connected = false;
				List<BorderInQuadTree> allbiqs = new List<BorderInQuadTree>(bdrquad.AllObjects());
				allbiqs.Sort(delegate (BorderInQuadTree b1, BorderInQuadTree b2)
				{
					double d1 = b1.border.Length;
					double d2 = b2.border.Length;
					if (d1 > d2) return -1;
					if (d2 > d1) return 1;
					return 0;
				}
				);
				// das größte zuerst
				foreach (BorderInQuadTree biq in allbiqs)
				{
					if (biq.border.orientation == Orientation.unknown) continue;
					// den kleinsten Abstand suchen, manchmal hängt ein kleiner Schnipsel
					// in der Nähe von einem Eckpunkt und wir gehen auf die falscge Fährte
					BorderInQuadTree biqfound = null;
					bool forward = true;
					double mindist = double.MaxValue;
					foreach (BorderInQuadTree biq1 in bdrquad.ObjectsCloseTo(biq))
					{
						if (biq1.border.orientation == Orientation.unknown) continue;
						if (biq != biq1)
						{
							double d = biq.border.EndPoint | biq1.border.StartPoint;
							if (d < Math.Abs(dist * 10.0 * precision) && d < mindist)
							{
								mindist = d;
								forward = true;
								biqfound = biq1;
							}

							else
							{
								d = biq.border.StartPoint | biq1.border.EndPoint;
								if (d < Math.Abs(dist * 10.0 * precision) && d < mindist)
								{
									mindist = d;
									forward = false; ;
									biqfound = biq1;
								}
							}
						}
					}
					if (biqfound != null)
					{
						Border bdr = null;
						if (forward)
						{
							bdrquad.RemoveObject(biq); // vorher entfernen, biq.border wird bei ConCat mögl. verändert
							bdrquad.RemoveObject(biqfound);
							bdr = Border.Concat(biq.border, biqfound.border);
						}
						else
						{
							bdrquad.RemoveObject(biq);
							bdrquad.RemoveObject(biqfound);
							bdr = Border.Concat(biqfound.border, biq.border);
						}
						if (bdr != null)
						{
							if (bdr.IsClosed)
							{
								if (this.GetMinDistance(bdr) > Math.Abs(dist) - Math.Abs(10 * dist * precision))
								{   // es gibt geschlossene Borders, die dem Orientierungskriterium genügen
									// aber dennoch nicht zur Lösung gehören. Es scheint dann so, dass alle Punkte
									// dieser Borders zu nah am Ausgangsborder liegen, deshalb genügt es einen Punkt zu testen
									// z.B. errorOnOffset.cdb, Abstand 3.0
									res.Add(bdr);
								}
								else
								{
								}
							}
							else
							{
								bdr.orientation = biq.border.orientation;
								bdrquad.AddObject(new BorderInQuadTree(bdr));
							}
							connected = true;
							break;
						}
					}
					if (connected) break;
				}
			} while (connected);
			connected = false;
			do
			{   // 2. Durchlauf: alle segmente verwenden
				connected = false;
				foreach (BorderInQuadTree biq in bdrquad.AllObjects())
				{
					foreach (BorderInQuadTree biq1 in bdrquad.ObjectsCloseTo(biq))
					{
						if (biq != biq1)
						{
							Border bdr = null;
							int i0, i1;
							if ((biq.border.EndPoint | biq1.border.StartPoint) < Math.Abs(dist * precision))
							{
								bdrquad.RemoveObject(biq); // vorher entfernen, biq.border wird bei ConCat mögl. verändert
								bdrquad.RemoveObject(biq1);
								bdr = Border.Concat(biq.border, biq1.border);
							}
							else if ((biq.border.StartPoint | biq1.border.EndPoint) < Math.Abs(dist * precision))
							{
								bdrquad.RemoveObject(biq);
								bdrquad.RemoveObject(biq1);
								bdr = Border.Concat(biq1.border, biq.border);
							}
							else if (biq.border.Overlaps(biq1.border, out i0, out i1))
							{
								bdrquad.RemoveObject(biq);
								bdrquad.RemoveObject(biq1);
								bdr = Border.Concat(biq.border, biq1.border, i0, i1);
							}
							else if (biq1.border.Overlaps(biq.border, out i0, out i1))
							{
								bdrquad.RemoveObject(biq);
								bdrquad.RemoveObject(biq1);
								bdr = Border.Concat(biq1.border, biq.border, i0, i1);
							}
							if (bdr != null)
							{
								if (bdr.IsClosed)
								{
									if (this.GetMinDistance(bdr) > Math.Abs(dist) - Math.Abs(dist * precision))
									{   // es gibt geschlossene Borders, die dem Orientierungskriterium genügen
										// aber dennoch nicht zur Lösung gehören. Es scheint dann so, dass alle Punkte
										// dieser Borders zu nah am Ausgangsborder liegen, deshalb genügt es einen Punkt zu testen
										// z.B. errorOnOffset.cdb, Abstand 3.0
										res.Add(bdr);
									}
									else
									{
									}
								}
								else
								{
									// es kann sein, dass sich das border am Anfang und am Ende überlappen
									// (z.B. ein Rechteck, bei dem eine Seite durch einen Kreis ausgebeult wird, dessen Mittelpunkt außerhalb des Rechtecks liegt.
									// Wenn dann nach innen dann das Rechteck wieder entsteht, so überlappen sich dessen Anfang und Ende)
									if (bdr.RemoveOverlap())
									{
										if (this.GetMinDistance(bdr) > Math.Abs(dist) - Math.Abs(dist * precision))
										{
											res.Add(bdr);
										}
									}
									else
									{
										if (biq.border.orientation != Orientation.unknown) bdr.orientation = biq.border.orientation;
										else bdr.orientation = biq1.border.orientation;
										bdrquad.AddObject(new BorderInQuadTree(bdr));
									}
								}
								connected = true;
								break;
							}
						}
					}
					if (connected) break;
				}
			} while (connected);

#if DEBUG
			DebuggerContainer dc5 = new DebuggerContainer();
			bool dbgclr = true;
			int dbgint = 0;
			foreach (BorderInQuadTree biq in bdrquad.AllObjects())
			{
				if (dbgclr)
					dc5.Add(biq.border, System.Drawing.Color.Red, dbgint++);
				else
					dc5.Add(biq.border, System.Drawing.Color.Blue, dbgint++);
				dbgclr = !dbgclr;
			}
			for (int i = 0; i < segment.Length; ++i)
			{
				dc5.Add(segment[i], System.Drawing.Color.Green, i);
			}
#endif
			// Alte Methode: immer nur die kleinste Lücke schließen
			// suche das beste Paar und füge es zusammen
			//BorderInQuadTree found1;
			//BorderInQuadTree found2;
			//do
			//{
			//    double mindist = double.MaxValue;
			//    found1 = null;
			//    found2 = null;
			//    foreach (BorderInQuadTree biq in bdrquad.AllObjects())
			//    {
			//        foreach (BorderInQuadTree biq1 in bdrquad.ObjectsFromRect(new BoundingRect(biq.border.EndPoint, Math.Abs(dist * precision), Math.Abs(dist * precision))))
			//        {
			//            if (biq != biq1)
			//            {
			//                double d = biq.border.EndPoint | biq1.border.StartPoint;
			//                if (d < mindist)
			//                {
			//                    found1 = biq;
			//                    found2 = biq1;
			//                    mindist = d;
			//                    if (mindist == 0.0) break;
			//                }
			//            }
			//        }
			//        if (mindist == 0.0) break; // braucht man nicht weitersuchen
			//        foreach (BorderInQuadTree biq1 in bdrquad.ObjectsFromRect(new BoundingRect(biq.border.StartPoint, Math.Abs(dist * precision), Math.Abs(dist * precision))))
			//        {
			//            if (biq != biq1)
			//            {
			//                double d = biq.border.StartPoint | biq1.border.EndPoint;
			//                if (d < mindist)
			//                {
			//                    found1 = biq1;
			//                    found2 = biq;
			//                    mindist = d;
			//                    if (mindist == 0.0) break;
			//                }
			//            }
			//        }
			//        if (mindist == 0.0) break; // braucht man nicht weitersuchen
			//    }
			//    if (found1 != null)
			//    {
			//        bdrquad.RemoveObject(found1);
			//        bdrquad.RemoveObject(found2);
			//        Border bdr = Border.Concat(found1.border, found2.border);
			//        if (bdr.isClosed)
			//        {   // geschlossene sind fertig
			//            res.Add(bdr);
			//        }
			//        else
			//        {
			//            bdrquad.AddObject(new BorderInQuadTree(bdr));
			//        }
			//    }
			//} while (found1 != null);

			// die überlappenden!!
			foreach (BorderInQuadTree biq in bdrquad.AllObjects())
			{   // Die restlichen Borders dazunehmen, entweder wenn offen erlaubt ist, oder fast geschlossen noch schließen
				if (!isClosed)
				{   // hier noch alle offenen dranhängen
					res.Add(biq.border);
				}
				else if ((biq.border.StartPoint | biq.border.EndPoint) < Math.Abs(dist * 10.0 * precision))
				{
					if (this.GetMinDistance(biq.border.StartPoint) > Math.Abs(dist) - Math.Abs(dist * 10.0 * precision))
					{   // es gibt geschlossene Borders, die dem Orientierungskriterium genügen
						// aber dennoch nicht zur Lösung gehören. Es scheint dann so, dass alle Punkte
						// dieser Borders zu nah am Ausgangsborder liegen, deshalb genügt es einen Punkt zu testen
						// z.B. errorOnOffset.cdb, Abstand 3.0
						double len = biq.border.Length;
						if (len > Math.Abs(0.1 * dist))
						{
							biq.border.forceClosed();
							res.Add(biq.border);
						}
					}
				}
				else if (biq.border.orientation != Orientation.unknown)
				{
					// gehört wahrscheinlich dazu
					double[] si = biq.border.AsPath().GetSelfIntersections();
					if (si.Length > 0)
					{
						GeoPoint2D pp = biq.border.AsPath().PointAt(si[0]);
						int firstind = -1;
						int lastind = -1;
						for (int i = 0; i < biq.border.Count; ++i)
						{
							if (firstind == -1)
							{
								if ((pp | biq.border[i].StartPoint) < Math.Abs(dist * precision))
								{
									firstind = i;
								}
							}
							if (firstind >= 0)
							{
								if ((pp | biq.border[i].EndPoint) < Math.Abs(dist * precision))
								{
									lastind = i;
									break;
								}
							}
						}
						if (lastind >= 0)
						{
							biq.border.Trimm(firstind, lastind);
							biq.border.forceClosed();
							res.Add(biq.border);
						}
					}
				}
			}
			if (IsClosed)
			{
				for (int i = 0; i < res.Count; i++)
				{
					if (!res[i].isClosed) res[i].forceClosed();
					// res[i].Area
					// this.Area
				}
				for (int i = res.Count - 1; i >= 0; --i)
				{
					if (Math.Abs(res[i].Area) < Math.Abs(this.Area) * 1e-5) res.RemoveAt(i);
				}
			}
			return res.ToArray();

			//ArrayList result = new ArrayList();
			//while (BrokenSegments.Count > 0)
			//{
			//    int lastind = BrokenSegments.Count - 1;
			//    Border bdr = BrokenSegments[lastind];
			//    BrokenSegments.RemoveAt(lastind);
			//    bool goon = !bdr.isClosed;
			//    while (goon)
			//    {
			//        goon = false;
			//        foreach (Border tst in BrokenSegments)
			//        {
			//            if ((tst.StartPoint | bdr.EndPoint) < 10 * precision)
			//            {
			//                BrokenSegments.Remove(tst);
			//                bdr = Border.Concat(bdr, tst);
			//                goon = !bdr.isClosed;
			//                break;
			//            }
			//            else if ((tst.EndPoint | bdr.StartPoint) < 10 * precision)
			//            {
			//                BrokenSegments.Remove(tst);
			//                bdr = Border.Concat(tst, bdr);
			//                goon = !bdr.isClosed;
			//                break;
			//            }
			//        }
			//    }
			//    // wenn geschlossen, dann muss auch bdr geschlossen sein
			//    if (!isClosed || bdr.isClosed) result.Add(bdr);
			//}

			//return (Border[])result.ToArray(typeof(Border));
		}
		/// <summary>
		/// Removes small segments which are smaller than <paramref name="minLength"/> and connects the remaining segments.
		/// </summary>
		/// <param name="minLength">Segments smaller than this value are beeing removed</param>
		public void RemoveSmallSegments(double minLength)
		{
			if (segment.Length < 2) return;
			List<ICurve2D> res = new List<ICurve2D>();
			for (int i = 0; i < segment.Length; i++)
			{
				if (segment[i].Length < minLength)
				{
					int before = i - 1;
					int after = i + 1;
					if (IsClosed)
					{
						if (before < 0) before = segment.Length - 1;
						if (after > segment.Length - 1) after = 0;
						GeoPoint2D center = new GeoPoint2D(segment[before].EndPoint, segment[after].StartPoint);
						segment[before].EndPoint = center;
						segment[after].StartPoint = center;
					}
					else
					{
						GeoPoint2D center; // Start und Endpunkt des Borders unverändert lassen
						if (i == 0) center = segment[i].StartPoint;
						else if (i == segment.Length - 1) center = segment[i].EndPoint;
						else center = new GeoPoint2D(segment[i].EndPoint, segment[i].StartPoint);
						if (before >= 0) segment[before].EndPoint = center;
						if (after < segment.Length) segment[after].StartPoint = center;
					}
				}
				else
				{
					res.Add(segment[i]);
				}
			}
			if (res.Count > 0)
			{   // also extrem kleine Borders nicht killen
				Segments = res.ToArray();
				bool reversed;
				Recalc(out reversed);
			}
		}

		private static Border Concat(Border first, Border second, int i0, int i1)
		{
			ICurve2D[] segments = new ICurve2D[i0 + second.segment.Length - i1 + 1];
			for (int i = 0; i <= i0; ++i)
			{
				segments[i] = first.segment[i];
			}
			for (int i = i1; i < second.segment.Length; ++i)
			{
				segments[i0 + i - i1 + 1] = second.segment[i];
			}
			return new Border(segments);
		}

		private bool Overlaps(Border other, out int i0, out int i1)
		{
			double prec = this.Extent.Size * 1e-8;
			i0 = i1 = -1;
			for (int i = 1; i < Math.Min(segment.Length, other.segment.Length); ++i)
			{
				for (int j = 0; j <= i; ++j)
				{
					if ((segment[segment.Length - 1 - j].EndPoint | other.segment[i - j].StartPoint) < prec)
					{
						i0 = segment.Length - 1 - j;
						i1 = i - j;
						return true;
					}
				}
			}
			return false;
		}

#if DEBUG
		private bool Invalid
		{
			get
			{
				for (int i = 0; i < segment.Length; ++i)
				{
					if (segment[i] is Arc2D)
					{
						if (Math.Abs((segment[i] as Arc2D).Sweep) > 1.8 * Math.PI) return true;
					}
				}
				return false;
			}
		}
#endif
		private void forceClosed()
		{
			if (segment.Length == 1 && segment[0] is Arc2D)
			{   // kann sonst leicht ein Bogen der Länge 0 werden
				Arc2D a2d = segment[0] as Arc2D;
				a2d.Close();
			}
			else
			{
				GeoPoint2D p = new GeoPoint2D(StartPoint, EndPoint);
				segment[0].StartPoint = p;
				segment[segment.Length - 1].EndPoint = p;
			}
			isClosed = true;
		}
		public Border Project(Plane fromPlane, Plane toPlane)
		{
			ArrayList segs = new ArrayList(segment.Length);
			for (int i = 0; i < segment.Length; ++i)
			{
				ICurve2D c = segment[i].Project(fromPlane, toPlane);
				if (c != null) segs.Add(c);
			}
			GeoVector n = toPlane.ToLocal(fromPlane.Normal);
			ICurve2D[] asegs = (ICurve2D[])(segs.ToArray(typeof(ICurve2D)));
			if (n.z < 0.0)
			{   // Richtung umdrehen
				Array.Reverse(asegs);
				for (int i = 0; i < asegs.Length; ++i)
				{
					asegs[i].Reverse();
				}
			}
			try
			{
				return new Border(asegs);
			}
			catch (CADability.Shapes.BorderException)
			{ // DEBUG!!!
				for (int i = 0; i < segment.Length; ++i)
				{
					ICurve2D c = segment[i].Project(fromPlane, toPlane);
					c.Reverse();
					GeoPoint2D s1 = c.EndPoint;
					GeoPoint2D s2 = toPlane.Project(fromPlane.ToGlobal(segment[i].StartPoint));
					GeoPoint2D e1 = c.StartPoint;
					GeoPoint2D e2 = toPlane.Project(fromPlane.ToGlobal(segment[i].EndPoint));
					double d1 = Geometry.Dist(s1, s2);
					double d2 = Geometry.Dist(e1, e2);
					if (d1 + d2 > 1e-10)
					{
						ICurve2D c2 = segment[i].Project(fromPlane, toPlane);
					}
				}
				return null;
			}
		}
		//public Border GetModified(ModOp2D m)
		//{	// über den Umweg IGeoObject ist das Problem vom Kreis zur Ellipse u.s.w. 
		//    // umgangen
		//    ArrayList segs = new ArrayList(segment.Length);
		//    ModOp m3d = ModOp.From2D(m);
		//    for (int i = 0; i < segment.Length; ++i)
		//    {
		//        IGeoObject go = segment[i].MakeGeoObject(Plane.XYPlane);
		//        go.Modify(m3d);
		//        ICurve2D c = (go as ICurve).GetProjectedCurve(Plane.XYPlane);
		//        if (c != null) segs.Add(c);
		//    }
		//    ICurve2D[] asegs = (ICurve2D[])(segs.ToArray(typeof(ICurve2D)));
		//    if (m.Determinant < 0.0)
		//    {	// Richtung umdrehen
		//        Array.Reverse(asegs);
		//        for (int i = 0; i < asegs.Length; ++i)
		//        {
		//            asegs[i].Reverse();
		//        }
		//    }
		//    return new Border(asegs, isClosed);
		//}
		public Border GetModified(ModOp2D m)
		{   // reimplemented: the old version made 3d curves, manipulated them and projected them back to make ellipses from circles when necessary
			// Now ICurve2D should implement this correctly and much faster
			List<ICurve2D> segs = new List<ICurve2D>(segment.Length);
			ModOp m3d = ModOp.From2D(m);
			for (int i = 0; i < segment.Length; ++i)
			{
				ICurve2D c = segment[i].GetModified(m);
				if (c != null) segs.Add(c);
			}
			if (m.Determinant < 0.0)
			{   // Reverse direction (array and each individual segment)
				segs.Reverse();
				for (int i = 0; i < segs.Count; ++i)
				{
					segs[i].Reverse();
				}
			}
			return new Border(segs.ToArray(), isClosed);
		}
		public ICurve2D[] GetPart(double startParam, double endParam, bool forward)
		{
			if (startParam == endParam) return new ICurve2D[] { };
			if (!forward)
			{   // umdrehen und zum Schluss das Ergebnis umdrehen
				double d = startParam;
				startParam = endParam;
				endParam = d;
			}
			int inds = (int)Math.Floor(startParam);
			if (inds < 0) inds = 0;
			double pars = startParam - inds;
			if (pars < 0.0) pars = 0.0;
			if (inds >= segment.Length)
			{
				inds = 0;
				pars = 0.0;
			}
			int inde = (int)Math.Floor(endParam);
			if (inde < 0) inde = 0;
			double pare = endParam - inde;
			if (pare < 0.0) pare = 0.0; // Rundungsproblem
			if (isClosed && inde >= segment.Length)
			{
				inde = 0;
				pare = 0.0;
			}
			//if (inde == inds && startParam < endParam) war vorher so, aber das führte zu Problemen (mail 2.9.10, Achintya)
			if (inde == inds && pars < pare)
			{
				// nur ein Stück von einem Segment
				ICurve2D c2d = segment[inds].Trim(pars, pare);
				if (c2d != null)
				{
					if (!forward) c2d.Reverse();
					c2d.UserData.CloneFrom(segment[inds].UserData);
					return new ICurve2D[] { c2d };
				}
			}
			// else
			{
				ArrayList res = new ArrayList();
				ICurve2D c2d = null;
				if (pars < 1) c2d = segment[inds].Trim(pars, 1.0);
				if (c2d != null && c2d.Length > Precision.eps)
				{
					c2d.UserData.CloneFrom(segment[inds].UserData);
					res.Add(c2d);
				}
				if (inde > inds)
				{
					for (int i = inds + 1; i < inde; ++i)
					{
						res.Add(segment[i].Clone());
					}
				}
				else
				{
					for (int i = inds + 1; i < segment.Length; ++i)
					{
						res.Add(segment[i].Clone());
					}
					for (int i = 0; i < inde; ++i)
					{
						res.Add(segment[i].Clone());
					}
				}
				if (inde < segment.Length && pare > 0)
				{
					c2d = segment[inde].Trim(0.0, pare);
					if (c2d != null && c2d.Length > Precision.eps)
					{
						c2d.UserData.CloneFrom(segment[inde].UserData);
						res.Add(c2d);
					}
				}
				ICurve2D[] toReturn = (ICurve2D[])res.ToArray(typeof(ICurve2D));
				if (!forward)
				{
					for (int i = 0; i < toReturn.Length; ++i)
					{
						toReturn[i].Reverse();
					}
					Array.Reverse(toReturn);
				}
				return toReturn;
			}
		}
		internal void Trimm(int firstIndex, int lastIndex)
		{
			ICurve2D[] newsegments = new ICurve2D[lastIndex - firstIndex + 1];
			Array.Copy(segment, firstIndex, newsegments, 0, lastIndex - firstIndex + 1);
			Segments = newsegments;
			bool dumy;
			Recalc(out dumy);
		}
		public void AddToGraphicsPath(System.Drawing.Drawing2D.GraphicsPath path, bool counterClockWise)
		{
			if (counterClockWise)
			{
				for (int i = 0; i < segment.Length; ++i)
				{
					segment[i].AddToGraphicsPath(path, true);
				}
			}
			else
			{
				for (int i = segment.Length - 1; i >= 0; --i)
				{
					segment[i].AddToGraphicsPath(path, false);
				}
			}
		}
		public double[] Clip(ICurve2D toClip, bool returnInsideParts)
		{
			List<double> res = new List<double>();
			using (new PrecisionOverride((extent.Width + extent.Height) * 1e-9))
			{
				ICollection cl = QuadTree.GetObjectsCloseTo(toClip);
				List<GeoPoint2DWithParameter> PointsOnToClip = new List<GeoPoint2DWithParameter>();
				foreach (ICurve2D curve in cl)
				{
#if DEBUG
					DebuggerContainer dc = new DebuggerContainer();
					dc.Add(curve, System.Drawing.Color.Red, 0);
					dc.Add(toClip, System.Drawing.Color.Blue, 1);
#endif
					GeoPoint2DWithParameter[] pwp = toClip.Intersect(curve);
					for (int i = 0; i < pwp.Length; ++i)
					{
						if (toClip.IsParameterOnCurve(pwp[i].par1) && curve.IsParameterOnCurve(pwp[i].par2))
						{
							PointsOnToClip.Add(pwp[i]);
						}
					}
				}
				PointsOnToClip.Sort(new ICompareGeoPoint2DWithParameter1());
				// doppelte rausschmeißen
				for (int i = PointsOnToClip.Count - 1; i > 0; --i)
				{
					if (PointsOnToClip[i].par1 - PointsOnToClip[i - 1].par1 < 1e-7)
					{
						PointsOnToClip.RemoveAt(i);
					}
				}
				if (PointsOnToClip.Count > 0)
				{
					// fast 0.0 und fast 1.0 auf 0.0 bzw. 1.0 setzen
					if (Math.Abs(PointsOnToClip[0].par1) < 1e-6) PointsOnToClip[0] = new GeoPoint2DWithParameter(PointsOnToClip[0].p, 0.0, PointsOnToClip[0].par2);
					if (Math.Abs(1.0 - PointsOnToClip[PointsOnToClip.Count - 1].par1) < 1e-6) PointsOnToClip[PointsOnToClip.Count - 1] = new GeoPoint2DWithParameter(PointsOnToClip[PointsOnToClip.Count - 1].p, 1.0, PointsOnToClip[PointsOnToClip.Count - 1].par2);
				}
				for (int i = -1; i < PointsOnToClip.Count; ++i)
				{
					GeoPoint2D startPoint, endPoint;
					double startParameter, endParameter;
					if (i == -1)
					{
						startPoint = toClip.StartPoint;
						startParameter = 0.0;
					}
					else
					{
						GeoPoint2DWithParameter pp = (GeoPoint2DWithParameter)PointsOnToClip[i];
						startPoint = pp.p;
						startParameter = pp.par1;
					}
					if (i == PointsOnToClip.Count - 1)
					{
						endPoint = toClip.EndPoint;
						endParameter = 1.0;
					}
					else
					{
						GeoPoint2DWithParameter pp = (GeoPoint2DWithParameter)PointsOnToClip[i + 1];
						endPoint = pp.p;
						endParameter = pp.par1;
					}
					if ((endParameter - startParameter > 0.5) || !Precision.IsEqual(startPoint, endPoint))
					{   // Problem in der Bedingung war, dass ganz innerhalb liegende Kreise/Ellipse
						// natürlich startPoint und endPoint gleich haben, aber hier nicht rausfliegen dürfen
						// deshalb "(endParameter-startParameter>0.5)" mit dazugenommen
						GeoPoint2D center = toClip.PointAt((startParameter + endParameter) / 2.0);
						Position pos = GetPosition(center);
						if ((pos == Position.Inside && returnInsideParts) ||
								(pos == Position.Outside && !returnInsideParts))
						{
							//ICurve2D c = toClip.Clone();
							//c = c.Trim(startParameter,endParameter);
							res.Add(startParameter);
							res.Add(endParameter);
						}
						if (pos == Position.OnCurve)
						{   // wenn man einen Zylinder oder Kegel genau an der Naht mit einer Ebene schneidet
							// so liegt der Mittelpunkt auch auf dem Rand. Dieses Linienstück wollen wir
							// mit dazu nehmen, denn es fehlt sonst beim Ebenen Schnitt und dort ist es essentiell
							// wenn es Situationen gibt, bei denen solche Stücke fehlen sollen, dann müsste das
							// mit einem zusätzlichen Parameter geregelt werden.
							res.Add(startParameter);
							res.Add(endParameter);
						}
					}
				}
			}
			return res.ToArray();
		}
		public GeoPoint2D SomeInnerPoint
		{
			get
			{
				Line2D l2d = new Line2D(extent.GetLowerLeft(), extent.GetUpperRight());
				double[] c = Clip(l2d, true);
				if (c.Length > 0)
				{
					double p = (c[0] + c[1]) / 2.0;
					GeoPoint2D res = l2d.PointAt(p);
					if (GetPosition(res) == Position.Inside) return res;
				}
				if (Area < Precision.eps) throw new BorderException("no inner point", BorderException.BorderExceptionType.General);
				while (true)
				{
					Random rnd = new Random();
					GeoPoint2D p1 = new GeoPoint2D(extent.Left, extent.Bottom + rnd.NextDouble() * extent.Height);
					GeoPoint2D p2 = new GeoPoint2D(extent.Right, extent.Bottom + rnd.NextDouble() * extent.Height);
					l2d = new Line2D(p1, p2);
					c = Clip(l2d, true);
					if (c.Length > 0)
					{
						double p = (c[0] + c[1]) / 2.0;
						GeoPoint2D res = l2d.PointAt(p);
						if (GetPosition(res) != Position.Outside) return res;
					}
				}
			}
		}

		internal Border[] RemoveConstrictions(double precision)
		{
			// If there are constrictions, i.e., areas where the border touches itself within "precision",
			// remove these constrictions and return a list of sub-borders that are at least "precision" apart
			// and do not have such narrow areas.

			List<Border> res = new List<Border>();
			double len = this.Extent.Size;

			for (int i = 0; i < segment.Length; i++)
			{
				ICollection cl = QuadTree.GetObjectsCloseTo(segment[i]);
				foreach (ICurve2D curve in cl)
				{
					if (curve != segment[i])
					{
						GeoPoint2D p1, p2;
						double mind = Curves2D.SimpleMinimumDistance(segment[i], curve, out p1, out p2);
						if (mind < precision)
						{
							double par1 = segment[i].PositionOf(p1);
							double par2 = curve.PositionOf(p2);
							if (Precision.OppositeDirection(segment[i].DirectionAt(par1), curve.DirectionAt(par2)))
							{
								// Split into parts
								int k = (int)segmentToIndex[curve];
								Border[] parts = Split(new double[] { i + par1, k + par2 });
								if (parts.Length == 3)
								{
									List<ICurve2D> c2ds = new List<ICurve2D>();
									c2ds.AddRange(parts[0].Segments);
									c2ds.AddRange(parts[2].Segments);
									Border bdr1 = new Border(c2ds.ToArray(), true);
									Border bdr2 = parts[1];
									bdr1.forceClosed();
									bdr1.RemoveNarrowEnd(precision);
									bdr2.forceClosed();
									bdr2.RemoveNarrowEnd(precision);

									// Recursively remove constrictions
									if (bdr1.Area > precision * precision)
										res.AddRange(bdr1.RemoveConstrictions(precision));
									if (bdr2.Area > precision * precision)
										res.AddRange(bdr2.RemoveConstrictions(precision));
								}
							}
						}
					}
				}
			}

			// If no constrictions were found, add the current border
			if (res.Count == 0)
				res.Add(this);

			return res.ToArray();
		}


		private bool RemoveNarrowEnd(double precision)
		{   // an der Naht ist dieses Border möglicherweise spitz. Gehe soweit zurück, bis mindestens eine Breite von precision erreicht wird
			if (Area < precision * precision)
			{
				Segments = new ICurve2D[0];
				bool rev;
				Recalc(out rev);
				return true;
			}
			while (Area > precision * precision)
			{
				if (segment.Length == 2)
				{
					double d1 = Math.Abs(segment[0].MinDistance(segment[1].PointAt(0.5)));
					if (d1 < precision)
					{
						Segments = new ICurve2D[0];
						bool rev;
						Recalc(out rev);
						return true;
					}
				}
				// versuche das segment 0 so weit zu kürzen, dass das dünne ende verschwindet
				double pos = 1.0;
				double step = 0.5;
				double lenprec = segment[0].Length * precision;
				do
				{
					ICollection cl = QuadTree.GetObjectsCloseTo(segment[0]);
					double mindist = double.MaxValue;
					ICurve2D mincurve = null;
					foreach (ICurve2D curve in cl)
					{
						if (curve != segment[0])
						{
							double d1 = Math.Abs(curve.MinDistance(segment[0].PointAt(pos)));
							if (d1 < mindist)
							{
								mindist = d1;
								mincurve = curve;
							}
						}
					}
					if (mindist < precision)
					{
						if (pos == 1.0 || step < lenprec)
						{   // aufhören noch genauer zu suchen und hier abschneiden
							double endpos = (int)(segmentToIndex[mincurve]) + mincurve.PositionOf(segment[0].PointAt(pos));
							Border[] parts = Split(new double[] { pos, endpos });
							Segments = parts[1].Segments;
							forceClosed();
							bool reversed;
							Recalc(out reversed);
							if (pos == 1.0)
							{
								// das ganze erste Segment wurde entfernt, versuche es mit dem nächsten Segment
								RemoveNarrowEnd(precision);
							}
							return true;
						}
						pos += step;
						step /= 2.0;
					}
					else
					{
						pos -= step;
						step /= 2.0;
					}
				} while (step > lenprec);
			}
			return false; // nix verändert
		}
		public double[] GetSelfIntersection(double precision)
		{
			List<double[]> resList = new List<double[]>();
			for (int i = 0; i < segment.Length; i++)
			{
				ICollection cl = QuadTree.GetObjectsCloseTo(segment[i]);
				foreach (ICurve2D curve in cl)
				{
					if (curve != segment[i])
					{
						int ci = (int)segmentToIndex[curve]; // das darf nie fehlschlagen
						if (ci > i + 1 && (i > 0 || ci != segment.Length - 1)) // nicht den unmittelbar folgenden betrachten und keine vorhergehenden
						{
							GeoPoint2DWithParameter[] ips = segment[i].Intersect(curve);
							if (ips.Length == 0)
							{   // berührpunkte am Anfang bzw. Ende ggf noch dazugeben
								List<GeoPoint2DWithParameter> toAdd = new List<GeoPoint2DWithParameter>();
								double md = segment[i].MinDistance(curve.StartPoint);
								if (md < precision)
								{
									toAdd.Add(new GeoPoint2DWithParameter(curve.StartPoint, segment[i].PositionOf(curve.StartPoint), 0.0));
								}
								md = segment[i].MinDistance(curve.EndPoint);
								if (md < precision)
								{
									toAdd.Add(new GeoPoint2DWithParameter(curve.EndPoint, segment[i].PositionOf(curve.EndPoint), 1.0));
								}
								md = curve.MinDistance(segment[i].StartPoint);
								if (md < precision)
								{
									toAdd.Add(new GeoPoint2DWithParameter(segment[i].StartPoint, 0.0, curve.PositionOf(segment[i].StartPoint)));
								}
								md = curve.MinDistance(segment[i].EndPoint);
								if (md < precision)
								{
									toAdd.Add(new GeoPoint2DWithParameter(segment[i].EndPoint, 1.0, curve.PositionOf(segment[i].EndPoint)));
								}
								if (toAdd.Count > 0)
								{
									ips = toAdd.ToArray();
								}
							}
							if (ips != null && ips.Length > 0)
							{
								for (int j = 0; j < ips.Length; j++)
								{
									if (segment[i].IsParameterOnCurve(ips[j].par1) && curve.IsParameterOnCurve(ips[j].par2))
									{   // nur Punkte auf den Kurven
										GeoVector2D dir1 = segment[i].DirectionAt(ips[j].par1);
										GeoVector2D dir2 = curve.DirectionAt(ips[j].par2);
										if (!Precision.SameNotOppositeDirection(dir1, -dir2, false)) // nicht gut für Bögen, die tangential ankommen!!!
										{
											double p1 = i + ips[j].par1;
											double p2 = ci + ips[j].par2;
											// das können jetzt Schnittpunkte von aufeinanderfolgenden Kurven sein
											double[] da = new double[] { p1, p2, GeoVector2D.Orientation(dir1, dir2) };
											bool found = false;
											for (int k = 0; k < resList.Count; k++)
											{   // ist der Punkt schon drin?
												if (Math.Abs(resList[k][0] - p1) < 1e-5)
												{
													found = true;
													break;
												}
											}
											if (!found) resList.Add(da);
										}
									}
								}
							}
						}
					}
				}
			}
			double[] res = new double[3 * resList.Count];
			for (int i = 0; i < resList.Count; i++)
			{
				res[3 * i] = resList[i][0];
				res[3 * i + 1] = resList[i][1];
				res[3 * i + 2] = resList[i][2];
			}
			return res;
		}

		private int findCurveIndex(ICurve2D curve)
		{
			for (int i = 0; i < segment.Length; i++)
			{
				if (segment[i] == curve) return i;
			}
			return -1;
		}
		/// <summary>
		/// Returns this Border as a <see cref="Path2D"/> object. 
		/// </summary>
		/// <returns></returns>
		public Path2D AsPath()
		{
			Path2D res = new Path2D(segment, true);
			if (isClosed) res.ForceClosed();
			return res;
		}
		/// <summary>
		/// Calculates the distance between this Border and the <paramref name="other"/> Border respecting the given direction <paramref name="dir"/>.
		/// Or in other words: how far can you move this Border in the direction dir until it touches the other border.
		/// The result may be double.MaxValue, which means they will never touch each other or negative, if you would have to move this
		/// border in the opposite direction of dir.
		/// </summary>
		/// <param name="other">The border to meassure the distance to</param>
		/// <param name="dir">Direction for the distance</param>
		/// <returns>Distance</returns>
		public double DistanceAtDirection(Border other, GeoVector2D dir)
		{   // hier könnte man natürlich mit dem QuadTree erheblich optimieren
			double res = double.MaxValue;
			int i0, j0;
			for (int i = 0; i < segment.Length; ++i)
			{
				for (int j = 0; j < other.segment.Length; ++j)
				{
					double d = Curves2D.DistanceAtDirection(segment[i], other.segment[j], dir);
					if (d < res)
					{
						res = d;
						i0 = i;
						j0 = j;
					}
				}
			}
			return res;
		}
		internal Path2D CloneAsPath(int startindex, bool reverse)
		{
			int k = 0;
			ICurve2D[] path = new ICurve2D[segment.Length];
			if (reverse)
			{
				for (int i = startindex; i >= 0; --i)
				{
					path[k] = segment[i].CloneReverse(true);
					++k;
				}
				for (int i = segment.Length - 1; i > startindex; --i)
				{
					path[k] = segment[i].CloneReverse(true);
					++k;
				}
			}
			else
			{
				for (int i = startindex; i < segment.Length; ++i)
				{
					path[k] = segment[i].Clone();
					++k;
				}
				for (int i = 0; i < startindex; ++i)
				{
					path[k] = segment[i].Clone();
					++k;
				}
			}
			return new Path2D(path, true);
		}
		internal GeoObjectList DebugList
		{
			get
			{
				GeoObjectList res = new GeoObjectList();
				for (int i = 0; i < segment.Length; ++i)
				{
					res.Add(segment[i].MakeGeoObject(Plane.XYPlane));
				}
				return res;
			}
		}
#if (DEBUG)
		public void Debug(string Title)
		{
			System.Diagnostics.Trace.WriteLine("Border: " + Title);
			for (int i = 0; i < segment.Length; ++i)
			{
				System.Diagnostics.Trace.WriteLine(segment[i].GetType().FullName + ": (" + segment[i].StartPoint.ToString() + ")->(" + segment[i].EndPoint.ToString() + ")");
			}
		}
#endif

		#region ISerializable Members
		/// <summary>
		/// Constructor required by deserialization
		/// </summary>
		/// <param name="info">SerializationInfo</param>
		/// <param name="context">StreamingContext</param>
		protected Border(SerializationInfo info, StreamingContext context)
		{
			Segments = (ICurve2D[])InfoReader.Read(info, "Segments", typeof(ICurve2D[]));
		}
		/// <summary>
		/// Implements <see cref="ISerializable.GetObjectData"/>
		/// </summary>
		/// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
		/// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
		public void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("Segments", segment, typeof(ICurve2D[]));
		}
		#endregion
		#region IDeserializationCallback Members
		void IDeserializationCallback.OnDeserialization(object sender)
		{
			bool reversed;
			Recalc(out reversed); // alle segmente sind hoffentlich ordentlich gelesen
		}
		#endregion

		internal void Approximate(bool linesOnly, double precision)
		{
			for (int i = 0; i < segment.Length; ++i)
			{
				segment[i] = segment[i].Approximate(linesOnly, precision);
			}
			flatten(); // Pfade in Einzelteile aufteilen
		}

		internal static bool OutlineContainsPoint(List<Edge> outline, GeoPoint2D geoPoint2D)
		{
			throw new Exception("The method or operation is not implemented.");
		}

		internal Path MakePath(Plane plane)
		{
			Path2D p2d = new Path2D(segment, true);
			return p2d.MakeGeoObject(plane) as Path;
		}

		internal void Move(double dx, double dy)
		{
			for (int i = 0; i < segment.Length; ++i)
			{
				segment[i].Move(dx, dy);
			}
			bool reversed;
			Recalc(out reversed);
		}

		internal static Border[] ClosedBordersFromList(GeoObjectList l, Plane pln, double precision)
		{
			QuadTree<ICurve2D> qt = new QuadTree<ICurve2D>();
			Set<ICurve2D> forwardUsed = new Set<ICurve2D>(); // schon vorwärts verwendet
			Set<ICurve2D> backwardUsed = new Set<ICurve2D>(); // schon rückwärts verwendet
			for (int i = 0; i < l.Count; ++i)
			{
				if (l[i] is ICurve)
				{
					qt.AddObject((l[i] as ICurve).GetProjectedCurve(pln));
				}
			}
			List<Border> res = new List<Border>();
			foreach (ICurve2D c2d in qt.AllObjects())
			{
				if (!forwardUsed.Contains(c2d))
				{
					forwardUsed.Add(c2d);
					Path2D collect = new Path2D(new ICurve2D[] { c2d.Clone() });
					Set<ICurve2D> usedInThisPath = new Set<ICurve2D>();
					usedInThisPath.Add(c2d);
					bool connected = false;
					do
					{
						connected = false;
						if (collect.IsClosed) // precision beachten!
						{
							if (collect.Sweep > 0) res.Add(collect.MakeBorder());
							break;
						}
						ICurve2D[] found = qt.GetObjectsFromRect(new BoundingRect(collect.EndPoint, precision, precision));
						// eigentlich besten Anschluss suchen, so kann eine kleine Lücke überbrückt werden
						// obwohl ein besserer Anschluss da ist
						for (int i = 0; i < found.Length; ++i)
						{
							if (!usedInThisPath.Contains(found[i]))
							{
								if ((found[i].StartPoint | collect.EndPoint) < precision && !forwardUsed.Contains(found[i]))
								{
									forwardUsed.Add(found[i]);
									usedInThisPath.Add(found[i]);
									collect.Append(found[i].Clone());
									connected = true;
									break;
								}
								if ((found[i].EndPoint | collect.EndPoint) < precision && !backwardUsed.Contains(found[i]))
								{
									backwardUsed.Add(found[i]);
									usedInThisPath.Add(found[i]);
									collect.Append(found[i].CloneReverse(true));
									connected = true;
									break;
								}
							}
						}
					} while (connected);
				}
				if (!backwardUsed.Contains(c2d))
				{
					backwardUsed.Add(c2d);
					Path2D collect = new Path2D(new ICurve2D[] { c2d.CloneReverse(true) });
					Set<ICurve2D> usedInThisPath = new Set<ICurve2D>();
					usedInThisPath.Add(c2d);
					bool connected = false;
					do
					{
						connected = false;
						if (collect.IsClosed) // precision beachten!
						{
							if (collect.Sweep > 0) res.Add(collect.MakeBorder());
							break;
						}
						ICurve2D[] found = qt.GetObjectsFromRect(new BoundingRect(collect.EndPoint, precision, precision));
						for (int i = 0; i < found.Length; ++i)
						{
							if (!usedInThisPath.Contains(found[i]))
							{
								if ((found[i].StartPoint | collect.EndPoint) < precision && !forwardUsed.Contains(found[i]))
								{
									forwardUsed.Add(found[i]);
									usedInThisPath.Add(found[i]);
									collect.Append(found[i].Clone());
									connected = true;
									break;
								}
								if ((found[i].EndPoint | collect.EndPoint) < precision && !backwardUsed.Contains(found[i]))
								{
									backwardUsed.Add(found[i]);
									usedInThisPath.Add(found[i]);
									collect.Append(found[i].CloneReverse(true));
									connected = true;
									break;
								}
							}
						}
					} while (connected);
				}
			}
			return res.ToArray();
		}

		internal void CalcRanges(RangeCounter rcLength, RangeCounter rcAngle)
		{
			if (segment.Length == 1)
			{
				rcLength.Add(segment[0].Length);
				return; // hier macht ein Winkel keinen Sinn
			}
			GeoVector2D lastDir = segment[segment.Length - 1].MiddleDirection;
			for (int i = 0; i < segment.Length; ++i)
			{
				GeoVector2D dir = segment[i].MiddleDirection;
				SweepAngle sw = new SweepAngle(lastDir, dir);
				lastDir = dir;
				rcAngle.Add(sw.Radian);
				rcLength.Add(segment[i].Length);
			}
		}

		internal double[] Code()
		{   // Liefert Folge von Paaren: Längen und Abknick-Winkel
			double[] res = new double[segment.Length * 2];
			GeoVector2D lastDir = segment[segment.Length - 1].MiddleDirection;
			for (int i = 0; i < segment.Length; i++)
			{
				GeoVector2D dir = segment[i].MiddleDirection;
				SweepAngle sw = new SweepAngle(lastDir, dir);
				res[2 * i] = segment[i].Length;
				res[2 * i + 1] = sw.Radian;
				lastDir = dir;
			}
			return res;
		}

		/// <summary>
		/// returns triples: x or y of intersectionPoint, parameter on border, intersection angle
		/// </summary>
		/// <param name="hor"></param>
		/// <param name="xy"></param>
		/// <returns></returns>
		internal double[] GetOrthoIntersection(bool hor, double xy, double precision)
		{
			List<double> res = new List<double>();
			Line2D l2d;
			if (hor) l2d = new Line2D(new GeoPoint2D(extent.Left, xy), new GeoPoint2D(extent.Right, xy));
			else l2d = new Line2D(new GeoPoint2D(xy, extent.Bottom), new GeoPoint2D(xy, extent.Top));
			GeoPoint2DWithParameter[] ips = this.GetIntersectionPoints(l2d, precision);
			for (int i = 0; i < ips.Length; i++)
			{
				double md = this.GetMinDistance(ips[i].p);
				if (md > precision) continue; // Schnittpunkte an Spitzen nicht erlauben, wenn zu weit außerhalb
				if (hor) res.Add(ips[i].p.x);
				else res.Add(ips[i].p.y);
				double par = ips[i].par1;
				res.Add(par);
				res.Add(this.DirectionAt(par).Angle.Radian);
			}
			return res.ToArray();
		}

		internal static Border MakePolygon(params GeoPoint2D[] p)
		{
			ICurve2D[] segments = new ICurve2D[p.Length];
			for (int i = 0; i < segments.Length; i++)
			{
				segments[i] = new Line2D(p[i], p[(i + 1) % p.Length]);
			}
			return new Border(segments);
		}
		/// <summary>
		/// Returns the convex hull of this Border
		/// </summary>
		/// <returns></returns>
		public Border ConvexHull()
		{
			Border clone = this.Clone();
			clone.flatten(); // to avoid polyLines
			List<ICurve2D> segments = new List<ICurve2D>(clone.Segments);
			// we cannot have segments with inflection points for the following algorithm, so we split those segments
			for (int i = segments.Count - 1; i >= 0; --i)
			{
				double[] infl = segments[i].GetInflectionPoints();
				if (infl != null && infl.Length > 0)
				{
					Array.Sort(infl);
					double lastPos = 1.0;
					for (int j = infl.Length - 1; j >= 0; --j)
					{
						ICurve2D part = segments[i].Trim(infl[j], lastPos);
						if (part != null) segments.Insert(i + 1, part);
						lastPos = infl[j];
					}
					segments[i] = segments[i].Trim(0, infl[0]);
				}
			}
			bool found = false;
			//do
			//{   // now remove all inner loops
			//    found = false;
			//    for (int seg1 = 0; seg1 < segments.Count; seg1++)
			//    {
			//        for (int seg2 = 0; seg2 < segments.Count; seg2++)
			//        {
			//            if (seg2 == (seg1 + 1) % segments.Count) continue;
			//            if (Precision.IsEqual(segments[seg1].EndPoint, segments[seg2].StartPoint))
			//            {
			//                List<ICurve2D> part1 = new List<ICurve2D>();
			//                List<ICurve2D> part2 = new List<ICurve2D>();
			//                if (seg1 > seg2)
			//                {
			//                    for (int i = 0; i < segments.Count; i++)
			//                    {
			//                        if (i <= seg1 && i >= seg2) part1.Add(segments[i]);
			//                        else part2.Add(segments[i]);
			//                    }
			//                }
			//                else
			//                {
			//                    for (int i = 0; i < segments.Count; i++)
			//                    {
			//                        if (i <= seg1 || i >= seg2) part1.Add(segments[i]);
			//                        else part2.Add(segments[i]);
			//                    }
			//                }
			//                double a1 = SignedArea(part1);
			//                double a2 = SignedArea(part2);
			//                if (a1 > 0 && a2 > 0) // we have an outer loop and an inner loop, remove the inner loop
			//                {
			//                    segments.Clear();
			//                    if (a1 > a2) segments.AddRange(part1);
			//                    else segments.AddRange(part2);
			//                    found = true;
			//                    break;
			//                }
			//            }
			//        }
			//    }
			//} while (found);
			// 1. remove segments, which are an inner bulge
			for (int i = 0; i < segments.Count; i++)
			{
				if (segments[i].Sweep < 0)
				{
					segments[i] = new Line2D(segments[i].StartPoint, segments[i].EndPoint);
				}
			}
			do
			{
				found = false;
				// 2. from segments, which represent an outer bulge 
				for (int i = 0; i < segments.Count; i++)
				{
					if (segments[i].Sweep > 0)
					{   // this is some kind of a bulge to the outside
						// find objects which lie to the right of the beam [startPoint,startDirection] or [endPoint,endDirection]
						double endPos = 1.0; // minimal position of segments that touch the beam [pointAt(endPos), directionAt(endPos)] in the forward direction
						int endSegment = -1; // the segment to endPos
						double onEndSegment = 0.0; // the position on the endSegment
						double startPos = 0.0;
						int startSegment = -1;
						double onStartSegment = 0.0;
						for (int j = 0; j < segments.Count; j++)
						{
							if (i == j) continue;
							if (!Geometry.OnLeftSide(segments[j].StartPoint, segments[i].EndPoint, segments[i].EndDirection) ||
								!Geometry.OnLeftSide(segments[j].StartPoint, segments[i].StartPoint, segments[i].StartDirection))
							{   // check the startPoint of segment j (the endpoint is not checked, since it is the startpoint of j+1)
								GeoPoint2D[] tps = segments[i].TangentPoints(segments[j].StartPoint, segments[i].PointAt(0.5));
								for (int k = 0; k < tps.Length; k++)
								{
									double pos = segments[i].PositionOf(tps[k]);
									if (pos > 1e-6 && pos < 1 - 1e-6)
									{
										GeoVector2D dir = segments[i].DirectionAt(pos);
										double lpos = Geometry.LinePar(tps[k], dir, segments[j].StartPoint);
										if (lpos > 0)
										{
											if (pos < endPos)
											{
												endPos = pos;
												endSegment = j;
												onEndSegment = 0.0; // startpoint of segment j
											}
										}
										else
										{
											if (pos > startPos)
											{
												startPos = pos;
												startSegment = j;
												onStartSegment = 0.0;
											}
										}
									}
								}
							}
							if (!(segments[j] is Line2D))
							{   // if segment j is not a line, try to find a tangent line between segment i and segment j
								// to enhance performance we could check, whether segments[j] is party to the right side of the two beams, don't know what is faster
								GeoPoint2D[] tps = Curves2D.TangentLines(segments[i], segments[j]);
								for (int k = 0; k < tps.Length; k += 2)
								{
									double pos = segments[i].PositionOf(tps[k]);
									if (pos > 1e-6 && pos < 1 - 1e-6)
									{
										double posj = segments[j].PositionOf(tps[k + 1]);
										if (posj > 1e-6 && posj < 1 - 1e-6)
										{
											GeoVector2D dir = segments[i].DirectionAt(pos);
											double lpos = Geometry.LinePar(tps[k], dir, tps[k + 1]);
											if (lpos > 0)
											{
												if (pos < endPos)
												{
													endPos = pos;
													endSegment = j;
													onEndSegment = posj; // startpoint of segment j
												}
											}
											else
											{
												if (pos > startPos)
												{
													startPos = pos;
													startSegment = j;
													onStartSegment = posj;
												}
											}
										}
									}
								}
							}
						}
						if (startSegment >= 0)
						{
							GeoPoint2D sp = segments[startSegment].PointAt(onStartSegment);
							GeoPoint2D ep = segments[i].PointAt(startPos);
							for (int ii = 0; ii < segments.Count; ii++)
							{
								int nn = (startSegment + ii) % segments.Count;
								if (nn == i) break;
								if (Geometry.DistPL(segments[nn].EndPoint, ep, sp) > Precision.eps)
								{   // there is a vertex between startSegment and current segment (i, segment with bulge to the outside) which is on the right side of the new line
									startSegment = -1; // startSegment is not valid
									break;
								}
							}
						}
						if (endSegment >= 0)
						{
							GeoPoint2D sp = segments[i].PointAt(endPos);
							GeoPoint2D ep = segments[endSegment].PointAt(onEndSegment);
							for (int ii = 0; ii < segments.Count; ii++)
							{
								int nn = (i + ii) % segments.Count;
								if (nn == endSegment) break;
								if (Geometry.DistPL(segments[nn].EndPoint, ep, sp) > Precision.eps)
								{   // there is a vertex between startSegment and current segment (i, segment with bulge to the outside) which is on the right side of the new line
									endSegment = -1; // startSegment is not valid
									break;
								}
							}
						}
						if (endSegment >= 0 && startSegment >= 0)
						{
							if (endPos > startPos)
							{
								// the segment j is a bulge which has two tangents. We need the inner part of segment j and the part of the border from onEndSegment to onStartSegment
								// and the two tangents to make a new Border
								// Make a border from the segments, split it at 4 positions, take the first and third part plus the two tangents and make a new segment array
								Border tmp = new Border(segments.ToArray());
								double[] positions = new double[4];
								positions[0] = tmp.GetParameter(segments[i].PointAt(endPos));
								positions[1] = tmp.GetParameter(segments[endSegment].PointAt(onEndSegment));
								positions[2] = tmp.GetParameter(segments[startSegment].PointAt(onStartSegment));
								positions[3] = tmp.GetParameter(segments[i].PointAt(startPos));
								List<List<ICurve2D>> parts = tmp.SplitCyclical(positions);
								if (parts.Count == 4)
								{
									segments.Clear();
									segments.AddRange(parts[1]);
									segments.Add(new Line2D(parts[1].Last().EndPoint, parts[3].First().StartPoint));
									segments.AddRange(parts[3]);
									segments.Add(new Line2D(parts[3].Last().EndPoint, parts[1].First().StartPoint));
									found = true;
									break;
								}
							}
						}
						else if (endSegment >= 0)
						{
							Border tmp = new Border(segments.ToArray());
							double[] positions = new double[2];
							positions[0] = tmp.GetParameter(segments[i].PointAt(endPos));
							positions[1] = tmp.GetParameter(segments[endSegment].PointAt(onEndSegment));
							List<List<ICurve2D>> parts = tmp.SplitCyclical(positions);
							if (parts.Count == 2)
							{
								segments.Clear();
								segments.AddRange(parts[1]);
								segments.Add(new Line2D(parts[1].Last().EndPoint, parts[1].First().StartPoint));
								found = true;
								break;
							}
						}
						else if (startSegment >= 0)
						{
							GeoPoint2D sp = segments[startSegment].PointAt(onStartSegment);
							GeoPoint2D ep = segments[i].PointAt(startPos);
							Border tmp = new Border(segments.ToArray());
							double[] positions = new double[2];
							positions[0] = tmp.GetParameter(segments[startSegment].PointAt(onStartSegment));
							positions[1] = tmp.GetParameter(segments[i].PointAt(startPos));
							List<List<ICurve2D>> parts = tmp.SplitCyclical(positions);
							if (parts.Count == 2)
							{
								segments.Clear();
								segments.AddRange(parts[1]);
								segments.Add(new Line2D(parts[1].Last().EndPoint, parts[1].First().StartPoint));
								found = true;
								break;
							}
						}
					}
				}
			} while (found);
			do
			{   // now remove all line pairs which have concave angles
				found = false;
				for (int seg1 = 0; seg1 < segments.Count; seg1++)
				{
					int seg2 = (seg1 + 1) % segments.Count;
					if (segments[seg1] is Line2D && segments[seg2] is Line2D)
					{
						if (new SweepAngle(segments[seg1].EndDirection, segments[seg2].StartDirection) < 0)
						{
							segments[seg1] = new Line2D(segments[seg1].StartPoint, segments[seg2].EndPoint);
							segments.RemoveAt(seg2);
							found = true;
							break;
						}
					}
				}
			} while (found);

			do
			{   // now remove all inner loops
				found = false;
				for (int seg1 = 0; seg1 < segments.Count; seg1++)
				{
					for (int seg2 = 0; seg2 < segments.Count; seg2++)
					{
						if (seg2 == (seg1 + 1) % segments.Count) continue;
						if (Precision.IsEqual(segments[seg1].EndPoint, segments[seg2].StartPoint))
						{
							List<ICurve2D> part1 = new List<ICurve2D>();
							List<ICurve2D> part2 = new List<ICurve2D>();
							if (seg1 > seg2)
							{
								for (int i = 0; i < segments.Count; i++)
								{
									if (i <= seg1 && i >= seg2) part1.Add(segments[i]);
									else part2.Add(segments[i]);
								}
							}
							else
							{
								for (int i = 0; i < segments.Count; i++)
								{
									if (i <= seg1 || i >= seg2) part1.Add(segments[i]);
									else part2.Add(segments[i]);
								}
							}
							double a1 = SignedArea(part1);
							double a2 = SignedArea(part2);
							if (a1 > 0 && a2 > 0) // we have an outer loop and an inner loop, remove the inner loop
							{
								segments.Clear();
								if (a1 > a2) segments.AddRange(part1);
								else segments.AddRange(part2);
								found = true;
								break;
							}
						}
					}
				}
			} while (found);

			return new Border(segments.ToArray());
		}
		/// <summary>
		/// Finds the rectangle with the smallest area enclosing this Border.
		/// </summary>
		public SmallestEnclosingRect GetSmallestEnclosingRectangle()
		{
			Border ch = this.ConvexHull();
			double minSize = double.MaxValue;
			ModOp2D toMinSize = ModOp2D.Null;
			BoundingRect ext = BoundingRect.EmptyBoundingRect;
			for (int i = 0; i < ch.segment.Length; i++)
			{
				if (ch.segment[i] is Line2D l && l.Length > Precision.eps)
				{
					ModOp2D m = ModOp2D.Fit(new GeoPoint2D[] { l.StartPoint, l.EndPoint }, new GeoPoint2D[] { GeoPoint2D.Origin, new GeoPoint2D(1, 0) }, false);
					BoundingRect br = ch.GetModified(m).Extent;
					double size = br.Size;
					if (size < minSize)
					{
						minSize = size;
						toMinSize = m;
						ext = br;
					}
				}
			}
			if (toMinSize.IsNull)
			{
				// there are no lines in the convex hull, i.e. it is only convex arcs or curves, no idea how to find minimum rectangle except for iteration                
				ext = ch.Extent;
				SmallestEnclosingRect sbr = new SmallestEnclosingRect(ext.GetLowerLeft(),
																	  ext.Width * GeoVector2D.XAxis,
																	  ext.Height,
																	  ext.Width);
				return sbr;
			}
			else
			{
				ModOp2D mi = toMinSize.GetInverse();
				SmallestEnclosingRect sbr = new SmallestEnclosingRect(mi * ext.GetLowerLeft(),
																	  mi * (ext.Width * GeoVector2D.XAxis),
																	  mi.Factor * ext.Height,
																	  mi.Factor * ext.Width);
				return sbr;
			}
		}

		public void GetObjectData(IJsonWriteData data)
		{
			data.AddProperty("Segments", segment);
		}

		public void SetObjectData(IJsonReadData data)
		{
			segment = data.GetProperty<ICurve2D[]>("Segments");
			data.RegisterForSerializationDoneCallback(this);
		}
		void IJsonSerializeDone.SerializationDone(CADability.JsonSerialize jsonSerialize)
		{
			for (int i = 0; i < segment.Length; i++)
			{
				jsonSerialize.InvokeSerializationDoneCallback(segment[i]);
			}
			bool reversed;
			Recalc(out reversed); // this also calculates the extent
		}

		//private static double MaxDistance(ICurve2D curve1, ICurve2D curve2)
		//{
		//    double d1 = curve1.MinDistance()
		//}
		//public double SmallestEnclosingRectangle(out GeoPoint point, out GeoVector length, out GeoVector width)
		//{
		//    for (int j = 0; j < segment.Length; j++)
		//    {
		//        double maxdist = 0.0;
		//        for (int i = j+1; i < segment.Length; i++)
		//        {
		//            segment[i]
		//        }
		//        GeoPoint2D p = segment[j].StartPoint;
		//        double minDist = double.MaxValue;
		//        for (int i = 0; i < segment.Length; ++i)
		//        {
		//            minDist = Math.Min(minDist, segment[i].MinDistance(p));
		//        }

		//    }
		//    return minDist;

		//}
	}

	/// <summary>
	/// Contains information about the smallest enclosing rect
	/// </summary>
	public readonly struct SmallestEnclosingRect
	{
		/// <summary>
		/// Fill class with information about enclosing rect
		/// </summary>
		/// <param name="fixPoint">Fixpoint</param>
		/// <param name="basisDir">Basis Direction</param>
		/// <param name="height">Height</param>
		/// <param name="width">Width</param>
		internal SmallestEnclosingRect(GeoPoint2D fixPoint, GeoVector2D basisDir, double height, double width)
		{
			this.FixPoint = fixPoint;
			this.BasisDir = basisDir;
			this.Height = height;
			this.Width = width;
			this.Area = height * width;
		}

		/// <summary>
		/// Fix point of the bounding rect (lower left)
		/// </summary>
		public GeoPoint2D FixPoint { get; }
		/// <summary>
		/// Basis direction
		/// </summary>
		public GeoVector2D BasisDir { get; }
		/// <summary>
		/// Height
		/// </summary>
		public double Height { get; }
		/// <summary>
		/// Width
		/// </summary>
		public double Width { get; }
		/// <summary>
		/// Area
		/// </summary>
		public double Area { get; }
	}


	public class BorderBuilderException : System.ApplicationException
	{
		public enum BorderBuilderExceptionType { General, NoSegments };
		public BorderBuilderExceptionType ExceptionType;
		internal BorderBuilderException(string message, BorderBuilderExceptionType tp)
			: base(message)
		{
			ExceptionType = tp;
		}
	}
	/// <summary>
	/// Klasse zum Erzeugen von Border Objekten.
	/// </summary>

	public class BorderBuilder
	{
		private ArrayList segment;
		private BoundingRect currentExtent;
		private double precision;
		public BorderBuilder()
		{
			segment = new ArrayList();
			currentExtent = BoundingRect.EmptyBoundingRect;
			precision = -1.0; // nicht gesetzt
		}
		public void Clear()
		{
			segment.Clear();
			currentExtent = BoundingRect.EmptyBoundingRect;
			precision = -1.0; // nicht gesetzt
		}
		public double Precision
		{
			get { return precision; }
			set { precision = Math.Abs(value); }
		}
		/// <summary>
		/// Fügt das im Parameter gegebene Segment zu. ACHTUNG: die Methode <see cref="BuildBorder"/> BuildBorder
		/// dreht dieses Segment möglicherweise um. Wenn das nicht passieren darf, dann hier mit
		/// Clone arbeiten!
		/// </summary>
		/// <param name="ToAdd">zuzufügendes Segment</param>
		/// <returns></returns>
		public bool AddSegment(ICurve2D ToAdd)
		{
			if (segment.Count == 0)
			{
				segment.Add(ToAdd);
				currentExtent.MinMax(ToAdd.GetExtent());
				return true;
			}
			else
			{
				double prec = precision;
				if (prec < 0.0) prec = (currentExtent.Width + currentExtent.Height) * 1e-8;
				if (Geometry.Dist(((ICurve2D)segment[segment.Count - 1]).EndPoint, ToAdd.StartPoint) < prec)
				{
					segment.Add(ToAdd);
					currentExtent.MinMax(ToAdd.GetExtent());
					return true;
				}
				else
				{
					return false;
				}
			}
		}
		public void Connect(ICurve2D ToAdd)
		{
			if (!AddSegment(ToAdd))
			{
				double prec = precision;
				if (prec < 0.0) prec = (currentExtent.Width + currentExtent.Height) * 1e-8;
				double ds = Geometry.Dist(((ICurve2D)segment[segment.Count - 1]).EndPoint, ToAdd.StartPoint);
				double de = Geometry.Dist(((ICurve2D)segment[segment.Count - 1]).EndPoint, ToAdd.EndPoint);
				if (ds < de)
				{
				}
			}
		}
		//internal void AddWire2D(CndOCas.Wire2D w2d)
		//{
		//    for (int i=0; i<w2d.Count; ++i)
		//    {
		//        AddSegment(GeneralCurve2D..FromOcas(w2d.Item(i)));
		//    }
		//}
		public Border BuildBorder(bool forceClosed = false)
		{
			if (segment.Count == 0) throw new BorderBuilderException("No segments", BorderBuilderException.BorderBuilderExceptionType.NoSegments);
			double prec = precision;
			if (prec < 0.0) prec = (currentExtent.Width + currentExtent.Height) * 1e-8;
			//bool IsClosed = false;
			//if (Geometry.Dist(((ICurve2D)segment[0]).StartPoint, ((ICurve2D)segment[segment.Count - 1]).EndPoint) < prec)
			//{
			//    IsClosed = true;
			//}
			// Border res = new Border(segment, currentExtent, IsClosed);
			ICurve2D[] ss = (ICurve2D[])segment.ToArray(typeof(ICurve2D));
			Border res = new Border(ss, forceClosed);

			return res;
		}
		public bool IsOriented
		{
			get
			{
				if (!IsClosed) return false;
				double d = 0.0; // die akkumulierte Winkeldifferenz
				GeoVector2D lastDir = (segment[segment.Count - 1] as ICurve2D).EndDirection;
				for (int i = 0; i < segment.Count; ++i)
				{
					SweepAngle vertexdiff = new SweepAngle(lastDir, (segment[i] as ICurve2D).StartDirection);
					if (Math.Abs(Math.PI - Math.Abs(vertexdiff.Radian)) < 1e-6)
					{   // tangentiale Übergänge: diese Lösung ist nicht sehr gut:
						// wir schauen kurz vor dem gemeinsamen Punkt und nehmen das als Entscheidungsgrundlage.
						// Ein besseres Verfahren wäre ein genügend kleiner Kreis (Hälfte der Eckpunktabstände)
						// um den gemeinsamen Punkt und Schnittpunkte nehmen
						GeoVector2D lastdir1;
						if (i == 0) lastdir1 = (segment[segment.Count - 1] as ICurve2D).DirectionAt(0.9);
						else lastdir1 = (segment[i - 1] as ICurve2D).DirectionAt(0.9);
						GeoVector2D thisdir1 = (segment[i] as ICurve2D).DirectionAt(0.1);
						SweepAngle vertexdiff1 = new SweepAngle(lastdir1, thisdir1);
						if (vertexdiff1 < 0.0) vertexdiff = -Math.PI;
						else vertexdiff = Math.PI;
					}
					d += vertexdiff; // der ist immer zwischen -pi und +pi
					d += (segment[i] as ICurve2D).Sweep;
					lastDir = (segment[i] as ICurve2D).EndDirection;
				}
				// d muss +2*pi oder -2*pi sein, alles andere deutet auf eine innere Schleife hin
				// leider ist es nicht so, dass wenn d==+-2*pi ist es keine Selbstüberschneidung gibt.
				return d > 0.0;
			}
		}
		public bool IsClosed
		{
			get
			{
				return Geometry.Dist(StartPoint, EndPoint) < precision;
			}
		}
		public GeoPoint2D StartPoint
		{
			get
			{
				return (segment[0] as ICurve2D).StartPoint;
			}
		}
		public GeoPoint2D EndPoint
		{
			get
			{
				return (segment[segment.Count - 1] as ICurve2D).EndPoint;
			}
		}
		public GeoVector2D EndDirection
		{
			get
			{
				return (segment[segment.Count - 1] as ICurve2D).EndDirection;
			}
		}
		public ICurve2D LastCurve
		{
			get
			{
				return (segment[segment.Count - 1] as ICurve2D);
			}
		}
#if DEBUG
		GeoObjectList Debug
		{
			get
			{
				GeoObjectList res = new GeoObjectList();
				for (int i = 0; i < segment.Count; ++i)
				{
					res.Add((segment[i] as ICurve2D).MakeGeoObject(Plane.XYPlane));
				}
				return res;
			}
		}
#endif
	}

	internal class BorderAreaComparer : IComparer<Border>
	{
		public BorderAreaComparer() { }
		#region IComparer Members

		public int Compare(Border b1, Border b2)
		{
			try
			{
				return b1.Area.CompareTo(b2.Area);
			}
			catch (Exception e)
			{
				if (e is ThreadAbortException) throw (e);
				return 0;
			}
		}

		#endregion
	}
}