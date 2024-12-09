using CADability.Curve2D;
using CADability.Shapes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.Serialization;

namespace CADability
{
    /// <summary>
    /// BoundingRect is an axis oriented rectangle or a 2-dimensional bounding box. It is implemented as a "struct" (value type)
    /// so assignements always make a copy.
    /// </summary>
    [Serializable()]
    public struct BoundingRect : IQuadTreeInsertable, IComparable<BoundingRect>, ISerializable, IEquatable<BoundingRect>
    {
        public double Left;
        public double Right;
        public double Bottom;
        public double Top;
        /// <summary>
        /// Creates a new BoundingRect with the provided center, half width and half height
        /// </summary>
        /// <param name="Center">Center of the BoundingRect</param>
        /// <param name="HalfWidth">Half width</param>
        /// <param name="HalfHeight">Half height</param>
        public BoundingRect(GeoPoint2D Center, double HalfWidth, double HalfHeight)
        {
            if (HalfWidth > 0)
            {
                Left = Center.x - HalfWidth;
                Right = Center.x + HalfWidth;
            }
            else
            {
                Left = Center.x + HalfWidth;
                Right = Center.x - HalfWidth;
            }
            if (HalfHeight > 0)
            {
                Bottom = Center.y - HalfHeight;
                Top = Center.y + HalfHeight;
            }
            else
            {
                Bottom = Center.y + HalfHeight;
                Top = Center.y - HalfHeight;
            }
        }
        /// <summary>
        /// Creates a new BoundingRect with the provided limits.
        /// </summary>
        /// <param name="Left">Left limit</param>
        /// <param name="Bottom">Bottom limit</param>
        /// <param name="Right"></param>
        /// <param name="Top"></param>
        public BoundingRect(double Left, double Bottom, double Right, double Top)
        {
            this.Left = Left;
            this.Right = Right;
            this.Bottom = Bottom;
            this.Top = Top;
        }
        /// <summary>
        /// Creates a new BoundingRect which contains the provided points
        /// </summary>
        /// <param name="p">List of points to define the rectangle</param>
        public BoundingRect(params GeoPoint2D[] p)
        {
            Left = System.Double.MaxValue;
            Right = System.Double.MinValue;
            Bottom = System.Double.MaxValue;
            Top = System.Double.MinValue;
            for (int i = 0; i < p.Length; ++i)
            {
                if (p[i].x < Left) Left = p[i].x;
                if (p[i].x > Right) Right = p[i].x;
                if (p[i].y < Bottom) Bottom = p[i].y;
                if (p[i].y > Top) Top = p[i].y;
            }
        }
        /// <summary>
        /// Creates a new BoundingRect from the provided System.Drawing.rectangle
        /// </summary>
        /// <param name="r">das gegebene Rechteck</param>
        public BoundingRect(Rectangle r)
        {
            Left = r.Left;
            Right = r.Right;
            // das GDI Rechteck geht oft von oben nach unten
            if (r.Top < r.Bottom)
            {
                Bottom = r.Top;
                Top = r.Bottom;
            }
            else
            {
                Bottom = r.Bottom;
                Top = r.Top;
            }
        }
        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="r"></param>
        public BoundingRect(BoundingRect r)
        {
            Left = r.Left;
            Right = r.Right;
            Bottom = r.Bottom;
            Top = r.Top;
        }
        /// <summary>
        /// Returns an empty BoundingRect which is convenient as a start for the <see cref="MinMax"/> method.
		/// </summary>
		static public BoundingRect EmptyBoundingRect = new BoundingRect(System.Double.MaxValue, System.Double.MaxValue, System.Double.MinValue, System.Double.MinValue);
        static public BoundingRect InfinitBoundingRect = new BoundingRect(System.Double.MinValue, System.Double.MinValue, System.Double.MaxValue, System.Double.MaxValue);
        static public BoundingRect HalfInfinitBoundingRect = new BoundingRect(System.Double.MinValue / 2.0, System.Double.MinValue / 2.0, System.Double.MaxValue / 2.0, System.Double.MaxValue / 2.0);

        /// <summary>
        /// Returns an BoundingRect form 0 to 1 in both directions
        /// </summary>
        static public BoundingRect UnitBoundingRect
        {
            get
            {
                BoundingRect res;
                res.Left = 0.0;
                res.Right = 1.0;
                res.Bottom = 0.0;
                res.Top = 1.0;
                return res;
            }
        }
        /// <summary>
		/// Makes this BoundingRect an empty BoundingRect
		/// </summary>
		public void MakeEmpty()
        {
            Left = System.Double.MaxValue;
            Right = System.Double.MinValue;
            Bottom = System.Double.MaxValue;
            Top = System.Double.MinValue;
        }
        /// <summary>
        /// Returns true, if this BoundingRect is empty.
        /// </summary>
        /// <returns>true, if empty.</returns>
        public bool IsEmpty()
        {
            return Left == System.Double.MaxValue &&
                Right == System.Double.MinValue &&
                Bottom == System.Double.MaxValue &&
                Top == System.Double.MinValue;
        }
        /// <summary>
        /// Returns true if the BoundingRect is infinite. I.e. left and bottom equal Double.MinValue 
        /// and right and top equal Double.MaxValue 
        /// </summary>
        public bool IsInfinite
        {
            get
            {
                return (Left == System.Double.MinValue &&
                Right == System.Double.MaxValue) ||
                (Bottom == System.Double.MinValue &&
                Top == System.Double.MaxValue);
            }
        }
        // nur für Kompatibilität mit altem CONDOR Code. Besser eliminieren!
        internal void MakeInvalid() { MakeEmpty(); }
        internal bool IsInvalid() { return IsEmpty(); }
        /// <summary>
        /// Adapts the size of this bounding rectangle to include the provided point.
        /// </summary>
        /// <param name="p">Point to be included</param>
        public void MinMax(GeoPoint2D p)
        {   // arbeitet auch gut mit Empty, da jede Abfrage zutrifft
            if (p.x < Left) Left = p.x;
            if (p.x > Right) Right = p.x;
            if (p.y < Bottom) Bottom = p.y;
            if (p.y > Top) Top = p.y;
        }
        /// <summary>
        /// Adapts the size of this bounding rectangle to include the provided points.
        /// </summary>
        /// <param name="p">Points to be included</param>
        public void MinMax(params GeoPoint2D[] p)
        {   // arbeitet auch gut mit Empty, da jede Abfrage zutrifft
            for (int i = 0; i < p.Length; i++)
            {
                if (p[i].x < Left) Left = p[i].x;
                if (p[i].x > Right) Right = p[i].x;
                if (p[i].y < Bottom) Bottom = p[i].y;
                if (p[i].y > Top) Top = p[i].y;
            }
        }
        /// <summary>
        /// The location of a point relative to a bounding rectangle
        /// </summary>
        public enum Position
        {
            /// <summary>
            /// The point is inside the rectangle
            /// </summary>
            inside,
            /// <summary>
            /// The point is outside the rectangle
            /// </summary>
            outside,
            /// <summary>
            /// The point resides on one of the bounding lines
            /// </summary>
            onframe
        }
        /// <summary>
        /// Returns the position of the provided point with a given precision
        /// </summary>
        /// <param name="p">The point to test</param>
        /// <param name="frameWidth">The precision: if the distance to a bounding line is smaller 
        /// than this value, the point is considered as <see cref="Position.onframe"/></param>
        /// <returns>The position of the point</returns>
		public Position GetPosition(GeoPoint2D p, double frameWidth)
        {
            if (p.x < Left - frameWidth) return Position.outside;
            if (p.x > Right + frameWidth) return Position.outside;
            if (p.y < Bottom - frameWidth) return Position.outside;
            if (p.y > Top + frameWidth) return Position.outside;
            if (p.x > Left + frameWidth &&
                p.x < Right - frameWidth &&
                p.y > Bottom + frameWidth &&
                p.y < Top - frameWidth)
                return Position.inside;
            return Position.onframe;
        }
        /// <summary>
        /// Adapts the size of this bounding rectangle to include the provided rectangle.
        /// </summary>
        /// <param name="r">Rectangle to be included</param>
        public void MinMax(BoundingRect r)
        {   // arbeitet auch gut mit Invalid, da jede Abfrage zutrifft
            if (r.Left < Left) Left = r.Left;
            if (r.Right > Right) Right = r.Right;
            if (r.Bottom < Bottom) Bottom = r.Bottom;
            if (r.Top > Top) Top = r.Top;
        }
        internal double GetWidth() { return Right - Left; }
        internal double GetHeight() { return Top - Bottom; }
        /// <summary>
        /// Returns the center of the rectangle
        /// </summary>
        /// <returns></returns>
        public GeoPoint2D GetCenter() { return new GeoPoint2D((Left + Right) / 2, (Bottom + Top) / 2); }
        /// <summary>
        /// Returns the lower left point of the rectangle
        /// </summary>
        /// <returns></returns>
        public GeoPoint2D GetLowerLeft() { return new GeoPoint2D(Left, Bottom); }
        /// <summary>
        /// Returns the upper left point of the rectangle
        /// </summary>
        /// <returns></returns>
        public GeoPoint2D GetUpperLeft() { return new GeoPoint2D(Left, Top); }
        /// <summary>
        /// Returns the center of the left line of the rectangle
        /// </summary>
        /// <returns></returns>
        public GeoPoint2D GetMiddleLeft() { return new GeoPoint2D(Left, (Bottom + Top) / 2.0); }
        /// <summary>
        /// Returns the center of the right line of the rectangle
        /// </summary>
        /// <returns></returns>
        public GeoPoint2D GetMiddleRight() { return new GeoPoint2D(Right, (Bottom + Top) / 2.0); }
        /// <summary>
        /// Returns the center of the bottom line of the rectangle
        /// </summary>
        /// <returns></returns>
        public GeoPoint2D GetLowerMiddle() { return new GeoPoint2D((Left + Right) / 2.0, Bottom); }
        /// <summary>
        /// Returns the center of the upper line of the rectangle
        /// </summary>
        /// <returns></returns>
        public GeoPoint2D GetUpperMiddle() { return new GeoPoint2D((Left + Right) / 2.0, Top); }
        /// <summary>
        /// Returns the lower right point of the rectangle
        /// </summary>
        /// <returns></returns>
        public GeoPoint2D GetLowerRight() { return new GeoPoint2D(Right, Bottom); }
        /// <summary>
        /// Returns the upper right point of the rectangle
        /// </summary>
        /// <returns></returns>
        public GeoPoint2D GetUpperRight() { return new GeoPoint2D(Right, Top); }
        /// <summary>
        /// Inflates the rectangle by the provided value which may also be negative (for shrinking)
        /// </summary>
        /// <param name="d">Value for infaltion</param>
        public void Inflate(double d)
        {
            if (!IsEmpty())
            {
                Left -= d;
                Right += d;
                Bottom -= d;
                Top += d;
            }
        }
        /// <summary>
        /// Inflates the rectangle by a factor relative to its <see cref="BoundingRect.Size"/>.
        /// </summary>
        /// <param name="factor"></param>
        public void InflateRelative(double factor)
        {
            double d = Size * factor - Size;
            if (!IsEmpty())
            {
                Left -= d;
                Right += d;
                Bottom -= d;
                Top += d;
            }
        }
        /// <summary>
        /// Inflates the rectangle with different values in width an height
        /// </summary>
        /// <param name="width">Inflation of the width</param>
        /// <param name="height">Inflation of the height</param>
        public void Inflate(double width, double height)
        {
            if (!IsEmpty())
            {
                Left -= width;
                Right += width;
                Bottom -= height;
                Top += height;
            }
        }
        /// <summary>
        /// Modifies the rectangle by the provided <see cref="ModOp2D"/>. The resulting rectangle contains the modified
        /// vertices of this rectangle
        /// </summary>
        /// <param name="m">Modification to use</param>
        public void Modify(ModOp2D m)
        {
            GeoPoint2D p1 = m * new GeoPoint2D(Left, Bottom);
            GeoPoint2D p2 = m * new GeoPoint2D(Left, Top);
            GeoPoint2D p3 = m * new GeoPoint2D(Right, Bottom);
            GeoPoint2D p4 = m * new GeoPoint2D(Right, Top);
            Left = Right = p1.x;
            Bottom = Top = p1.y;
            MinMax(p2);
            MinMax(p3);
            MinMax(p4);
        }
        /// <summary>
        /// Moves the rectangle by the provided offset
        /// </summary>
        /// <param name="offset">Offset to move</param>
        public void Move(GeoVector2D offset)
        {
            Left += offset.x;
            Right += offset.x;
            Bottom += offset.y;
            Top += offset.y;
        }
        internal void norm()
        {
            double h;
            if (Left > Right)
            {
                h = Left;
                Left = Right;
                Right = h;
            }
            if (Bottom > Top)
            {
                h = Bottom;
                Bottom = Top;
                Top = h;
            }
        }
        /// <summary>
        /// Determins whether r1 is contained in r2.
        /// </summary>
        /// <param name="r1">Left operand</param>
        /// <param name="r2">Right operand</param>
        /// <returns>true, if r1 is contained in r2.</returns>
        public static bool operator <(BoundingRect r1, BoundingRect r2)
        {
            if (r1.Left > r2.Left && r1.Right < r2.Right && r1.Bottom > r2.Bottom && r1.Top < r2.Top) return true;
            return false;
        }
        /// <summary>
        /// Determins whether r2 is contained in r1.
        /// </summary>
        /// <param name="r1">Left operand</param>
        /// <param name="r2">Right operand</param>
        /// <returns>true, if r2 is contained in r1.</returns>
        public static bool operator >(BoundingRect r1, BoundingRect r2) { return !(r2 <= r1); }
        /// <summary>
        /// Determins whether the provided point is contained in the provided rectangle
        /// </summary>
        /// <param name="p">The point to test</param>
        /// <param name="r">The rectangle to test with</param>
        /// <returns>true if contained</returns>
		public static bool operator <(GeoPoint2D p, BoundingRect r)
        {
            if (p.x > r.Left && p.x < r.Right && p.y > r.Bottom && p.y < r.Top) return true;
            return false;
        }
        public bool ContainsLb(GeoPoint2D p)
        {   // die linke und untere Grenze werden mit einbezogen, die rechte obere nicht
            return p.x >= Left && p.x < Right && p.y >= Bottom && p.y < Top;
        }
        /// <summary>
        /// Determins whether the provided point is outside of the provided ractangle
        /// </summary>
        /// <param name="p">The point to test</param>
        /// <param name="r">The rectangle to test with</param>
        /// <returns>true if outside</returns>
        public static bool operator >(GeoPoint2D p, BoundingRect r) { return !(p <= r); }
        /// <summary>
        /// Determins whether r1 is contained in r2. It may also contact one ore multiple sides of the rectangle.
        /// </summary>
        /// <param name="r1">Left operand</param>
        /// <param name="r2">Right operand</param>
        /// <returns>true, if r1 is contained in r2.</returns>
        public static bool operator <=(BoundingRect r1, BoundingRect r2)
        {
            if (r1.Left >= r2.Left && r1.Right <= r2.Right && r1.Bottom >= r2.Bottom && r1.Top <= r2.Top) return true;
            return false;
        }
        /// <summary>
        /// Determins whether r2 is contained in r1. It may also contact one ore multiple sides of the rectangle.
        /// </summary>
        /// <param name="r1">Left operand</param>
        /// <param name="r2">Right operand</param>
        /// <returns>true, if r2 is contained in r1.</returns>
        public static bool operator >=(BoundingRect r1, BoundingRect r2) { return !(r2 < r1); }
        /// <summary>
        /// Determins whether the provided point is contained in the provided rectangle. It may also reside on one
        /// of the bounding lines.
        /// </summary>
        /// <param name="p">The point to test</param>
        /// <param name="r">The rectangle to test with</param>
        /// <returns>true if contained</returns>
        public static bool operator <=(GeoPoint2D p, BoundingRect r)
        {
            if (p.x >= r.Left && p.x <= r.Right && p.y >= r.Bottom && p.y <= r.Top) return true;
            return false;
        }
        /// <summary>
        /// Determins whether the provided point is outside of the provided ractangle. It may also reside on one
        /// of the bounding lines.
        /// </summary>
        /// <param name="p">The point to test</param>
        /// <param name="r">The rectangle to test with</param>
        /// <returns>true if outside</returns>
        public static bool operator >=(GeoPoint2D p, BoundingRect r) { return !(p < r); }
        public static bool operator ==(BoundingRect r1, BoundingRect r2)
        {
            return r1.Left == r2.Left && r1.Right == r2.Right && r1.Bottom == r2.Bottom && r1.Top == r2.Top;
        }
        public static bool operator !=(BoundingRect r1, BoundingRect r2)
        {
            return r1.Left != r2.Left || r1.Right != r2.Right || r1.Bottom != r2.Bottom || r1.Top != r2.Top;
        }
        /// <summary>
        /// Returns true if the two rectangles are disjoint (do not overlap)
        /// </summary>
        /// <param name="b1">First rectangle</param>
        /// <param name="b2">Second rectangle</param>
        /// <returns>true if disjoint</returns>
        public static bool Disjoint(BoundingRect b1, BoundingRect b2) // keine gemeinsame Fläche
        {   // der operator != kann hier nicht verwendet werden, denn er muss das Gegenteil von == sein
            return b1.Right <= b2.Left || b1.Top <= b2.Bottom || b2.Right <= b1.Left || b2.Top <= b1.Bottom;
        }
        /// <value>
        /// The width of the rectangle
        /// </value>
        public double Width
        {
            get
            {
                return Right - Left;
            }
        }
        /// <value>
        /// The height of the rectangle
        /// </value>
        public double Height
        {
            get
            {
                return Top - Bottom;
            }
        }
        public double Size
        {
            get
            {
                return Math.Abs(Top - Bottom) + Math.Abs(Right - Left);
            }
        }
        /// <summary>
        /// Returns a rectangle which is scaled by the provided factor. The center remaines fixed.
        /// </summary>
        /// <param name="rect">The initial rectangle</param>
        /// <param name="Factor">The scalingfactor</param>
        /// <returns>the scaled rectangle</returns>
        public static BoundingRect operator *(BoundingRect rect, double Factor)
        {
            GeoPoint2D Center = rect.GetCenter();
            double HalfWidth = rect.Width * Factor / 2.0;
            double HalfHeight = rect.Height * Factor / 2.0;
            return new BoundingRect(Center, HalfWidth, HalfHeight);
        }
        public static BoundingRect operator *(ModOp2D m, BoundingRect rect)
        {
            BoundingRect res = new BoundingRect(rect);
            res.Modify(m);
            return res;
        }
        /// <summary>
		/// Returns an inflated or deflated rectangle
		/// </summary>
		/// <param name="rect">The initial rectangle</param>
		/// <param name="offset">The offset for the inflation (may be negaitve)</param>
		/// <returns>The inflated rectangle</returns>
        public static BoundingRect operator +(BoundingRect rect, double offset)
        {
            return new BoundingRect(rect.Left - offset, rect.Bottom - offset, rect.Right + offset, rect.Top + offset);
        }
        public static BoundingRect operator +(BoundingRect r1, BoundingRect r2)
        {
            BoundingRect res = r1;
            res.MinMax(r2);
            return res;
        }
        /// <summary>
		/// Typecast to a System.Drawing.Rectangle
		/// </summary>
		/// <param name="r">The initial rectangle</param>
		/// <returns>The casted rectangle</returns>
        public static explicit operator Rectangle(BoundingRect r)
        {   // BoundingRect ist immer Top>Bottom, Rectangle umgekehrt
            // zumindest für den Zweck, wie es hier gebraucht wird.
            int left = (int)Math.Floor(r.Left);
            int top = (int)Math.Floor(r.Bottom);
            int right = (int)Math.Ceiling(r.Right);
            int bottom = (int)Math.Ceiling(r.Top);
            // +1 in Breite und Höhe, damit es beim Repaint u.s.w. stimmt
            return new Rectangle(left, top, right - left + 1, bottom - top + 1);
        }
        /// <summary>
        /// Returns a <see cref="Border"/> that consists of the four lines of this ractangle.
        /// </summary>
        /// <returns>The border</returns>
        public Border ToBorder()
        {
            Polyline2D p2d = new Polyline2D(new GeoPoint2D[] { new GeoPoint2D(Left, Bottom), new GeoPoint2D(Right, Bottom), new GeoPoint2D(Right, Top), new GeoPoint2D(Left, Top), new GeoPoint2D(Left, Bottom) });
            return new Border(p2d);
        }
        public RectangleF ToRectangleF()
        {
            return new RectangleF((float)Left, (float)Bottom, (float)(Width), (float)(Height));
        }
        internal static BoundingRect Common(BoundingRect b1, BoundingRect b2)
        {
            BoundingRect res = new BoundingRect(Math.Max(b1.Left, b2.Left), Math.Max(b1.Bottom, b2.Bottom),
                Math.Min(b1.Right, b2.Right), Math.Min(b2.Top, b1.Top));
            return res;
        }
        internal static BoundingRect Unite(BoundingRect b1, BoundingRect b2)
        {
            BoundingRect res = new BoundingRect(Math.Min(b1.Left, b2.Left), Math.Min(b1.Bottom, b2.Bottom),
                Math.Max(b1.Right, b2.Right), Math.Max(b2.Top, b1.Top));
            return res;
        }

        internal static BoundingRect Interpolate(BoundingRect r1, BoundingRect r2, double f)
        {
            return new BoundingRect(f * r1.Left + (1 - f) * r2.Left, f * r1.Bottom + (1 - f) * r2.Bottom, f * r1.Right + (1 - f) * r2.Right, f * r1.Top + (1 - f) * r2.Top);

        }

        internal bool Contains(GeoPoint2D uv)
        {
            return (uv.x >= Left && uv.x <= Right && uv.y >= Bottom && uv.y <= Top);
        }

        internal bool ContainsEps(GeoPoint2D uv, double eps)
        {
            double dx, dy;
            if (eps < 0) // Prozentsatz
            {
                dx = -(Right - Left) * eps;
                dy = -(Top - Bottom) * eps;
            }
            else
            {
                dx = eps;
                dy = eps;
            }
            return (uv.x >= Left - dx && uv.x <= Right + dx && uv.y >= Bottom - dy && uv.y <= Top + dy);
        }


        #region IQuadTreeInsertable Members
        BoundingRect IQuadTreeInsertable.GetExtent()
        {
            return this;
        }

        bool IQuadTreeInsertable.HitTest(ref BoundingRect rect, bool includeControlPoints)
        {
            return !Disjoint(this, rect);
        }

        object IQuadTreeInsertable.ReferencedObject
        {
            get { return null; }
        }
        #endregion

        #region IComparable<BoundingRect> Members
        int IComparable<BoundingRect>.CompareTo(BoundingRect other)
        {   // der Größe nach sortieren
            double d1 = Width * Height;
            double d2 = other.Width * other.Height;
            return d1.CompareTo(d2);
        }
        #endregion
        /// <summary>
        /// Returns true, when this rectangle and the provided rect overlap
        /// </summary>
        /// <param name="rect"></param>
        /// <returns></returns>
        public bool Interferes(ref BoundingRect rect)
        {
            if (rect.Right < Left) return false;
            if (rect.Left > Right) return false;
            if (rect.Bottom > Top) return false;
            if (rect.Top < Bottom) return false;
            return true;
        }
        internal bool Overlaps(BoundingRect rect)
        {
            if (rect.Right <= Left) return false;
            if (rect.Left >= Right) return false;
            if (rect.Bottom >= Top) return false;
            if (rect.Top <= Bottom) return false;
            return true;
        }
#if DEBUG
        internal DebuggerContainer Debug
        {
            get
            {
                DebuggerContainer res = new DebuggerContainer();
                res.Add(new Line2D(new GeoPoint2D(Left, Bottom), new GeoPoint2D(Right, Bottom)));
                res.Add(new Line2D(new GeoPoint2D(Right, Bottom), new GeoPoint2D(Right, Top)));
                res.Add(new Line2D(new GeoPoint2D(Right, Top), new GeoPoint2D(Left, Top)));
                res.Add(new Line2D(new GeoPoint2D(Left, Top), new GeoPoint2D(Left, Bottom)));
                return res;
            }
        }
        internal Polyline2D DebugAsPolyline
        {
            get
            {
                Polyline2D res = new Polyline2D(new GeoPoint2D[] { new GeoPoint2D(Left, Bottom), new GeoPoint2D(Right, Bottom), new GeoPoint2D(Right, Top), new GeoPoint2D(Left, Top), new GeoPoint2D(Left, Bottom) });
                return res;
            }
        }
#endif
        internal static BoundingRect Intersect(BoundingRect b1, BoundingRect b2)
        {
            BoundingRect res = new BoundingRect();
            res.Left = Math.Max(b1.Left, b2.Left);
            res.Bottom = Math.Max(b1.Bottom, b2.Bottom);
            res.Right = Math.Min(b1.Right, b2.Right);
            res.Top = Math.Min(b1.Top, b2.Top);
            return res;
        }

        internal bool ContainsPeriodic(GeoPoint2D uv, double up, double vp)
        {
            if (uv.x >= Left && uv.x <= Right && uv.y >= Bottom && uv.y <= Top) return true;
            if (up > 0.0)
            {
                while (Right - uv.x >= up) uv.x += up;
                while (uv.x - Left >= up) uv.x -= up;
            }
            if (vp > 0.0)
            {
                while (Top - uv.y >= vp) uv.y += vp;
                while (uv.y - Bottom >= vp) uv.y -= vp;
            }
            if (up > 0.0 || vp > 0.0) return (uv.x >= Left && uv.x <= Right && uv.y >= Bottom && uv.y <= Top);
            return false;
        }

        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        BoundingRect(SerializationInfo info, StreamingContext context)
        {
            Left = (double)info.GetValue("Left", typeof(double));
            Right = (double)info.GetValue("Right", typeof(double));
            Bottom = (double)info.GetValue("Bottom", typeof(double));
            Top = (double)info.GetValue("Top", typeof(double));
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Left", Left);
            info.AddValue("Right", Right);
            info.AddValue("Bottom", Bottom);
            info.AddValue("Top", Top);
        }

        internal BoundingRect GetModified(ModOp2D m)
        {
            BoundingRect res = new BoundingRect(this);
            res.Modify(m);
            return res;
        }

        internal bool IsClose(double v1, double v2, bool horizontal)
        {
            if (horizontal) return Math.Abs(v2 - v1) < (Right - Left) * 1e-6;
            else return Math.Abs(v2 - v1) < (Top - Bottom) * 1e-6;
        }

        /// <summary>
        /// Checks, whether the x difference of the twop points is less tah width*1e-6 and the y difference is less than height*1e-6.
        /// This is a good check for identity of points on the uv system of surfaces
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <returns></returns>
        internal bool IsClose(GeoPoint2D p1, GeoPoint2D p2)
        {
            return Math.Abs(p1.x - p2.x) < (Right - Left) * 1e-6 && Math.Abs(p1.y - p2.y) < (Top - Bottom) * 1e-6;
        }

        /// <summary>
        /// Returns a point on the bound of the rectangle going from lower left (parameter 0.0) counterclockwise (lower right is at parameter 1.0)
        /// back to lower left (at parameter 4.0)
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        internal GeoPoint2D PointAt(double v)
        {
            v = v % 4.0;
            if (v < 1) return new GeoPoint2D(Left + v * (Right - Left), Bottom);
            else if (v < 2) return new GeoPoint2D(Right, Bottom + (v - 1) * (Top - Bottom));
            else if (v < 3) return new GeoPoint2D(Left + (3 - v) * (Right - Left), Top);
            else if (v <= 4) return new GeoPoint2D(Left, Bottom + (4 - v) * (Top - Bottom));
            else throw new ApplicationException("this cannot happen!"); // this cannot happen
        }
        /// <summary>
        /// Returns the position of the point p, which is assumed to be on the bound of this rectangle.
        /// 0 is lower left, 1 is lower right and so on
        /// </summary>
        /// <param name="p">the point to check</param>
        /// <returns>the parameter which is between 0 and 4</returns>
        internal double PositionOf(GeoPoint2D p)
        {
            double ld = Math.Abs(p.x - Left);
            double rd = Math.Abs(p.x - Right);
            double bd = Math.Abs(p.y - Bottom);
            double td = Math.Abs(p.y - Top);
            if (ld < rd && ld < bd && ld < td)
            {   // closest to the left side
                double d = (p.y - Bottom) / (Top - Bottom);
                return Math.Min(4.0, 4.0 - d);
            }
            else if (rd < bd && rd < td)
            {   // closest to the right side
                double d = (p.y - Bottom) / (Top - Bottom);
                return 1.0 + d;
            }
            else if (bd < td)
            {   // closest to the bottom side
                double d = (p.x - Left) / (Right - Left);
                return Math.Max(0.0, d);
            }
            else
            {   // closest to the top
                double d = (p.x - Left) / (Right - Left);
                return 3.0 - d;
            }
        }
        /// <summary>
        /// Returns vertices of lines going from v1 to v2 (in the meaning of PointAt)
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <returns></returns>
        internal GeoPoint2D[] GetLines(double v1, double v2)
        {
            List<GeoPoint2D> res = new List<GeoPoint2D>();
            res.Add(PointAt(v1));
            int iend = (int)Math.Floor(v2);
            if (v2 < v1) iend += 4;
            int istart = (int)Math.Ceiling(v1);
            if (v1 == istart) istart = (istart + 1);
            if (v2 == iend) --iend;
            for (int i = istart; i <= iend; i++)
            {
                res.Add(PointAt(i % 4));
            }
            res.Add(PointAt(v2));
            return res.ToArray();
        }
        #endregion

        public bool Equals(BoundingRect other)
        {
            return Left.Equals(other.Left) && Right.Equals(other.Right) && Bottom.Equals(other.Bottom) && Top.Equals(other.Top);
        }

        public override bool Equals(object obj)
        {
            return obj is BoundingRect other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Left.GetHashCode();
                hashCode = (hashCode * 397) ^ Right.GetHashCode();
                hashCode = (hashCode * 397) ^ Bottom.GetHashCode();
                hashCode = (hashCode * 397) ^ Top.GetHashCode();
                return hashCode;
            }
        }
    }

}
