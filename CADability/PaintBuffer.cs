using System;
using System.Drawing;
using Point = System.Drawing.Point;

namespace CADability
{
    /// <summary>
    /// Remnant of the former GDI based display implementation. Originally this class held one bitmap per <see cref="DrawingAspect"/>
    /// (background, drawing, selection, active objects), accumulated the invalid rectangles of each aspect via the InvalidateXxx
    /// methods and composed the bitmaps into the window on the paint event, raising a repaint event for each aspect whose invalid
    /// area was not empty. Today only the <see cref="DrawingAspect"/> enumeration (used by the views to invalidate and to register
    /// paint handlers for a specific aspect) and the static rectangle helpers <see cref="Union"/> and
    /// <see cref="RectangleFromPoints"/> are still in use; the invalidation and scrolling methods have no effect on the display.
    /// </summary>

    public class PaintBuffer
    {
        /// <summary>
        /// The layers of the display of a view, which are painted on top of each other and can be invalidated separately: Background
        /// (background color and grid), Drawing (the model), Select (the marks of the selected objects), Active (the feedback of the
        /// running action) and All (all of them). Used with <see cref="IView.Invalidate"/> and <see cref="IView.SetPaintHandler"/>.
        /// </summary>
        public enum DrawingAspect { Background, Drawing, Select, Active, All }
        //public delegate void Repaint(Rectangle Extent,PaintToGDIDeprecated PaintToDrawing); 
        //public event Repaint RepaintBackgroundEvent;
        //public event Repaint RepaintDrawingEvent;
        //public event Repaint RepaintSelectEvent;
        //public event Repaint RepaintActiveEvent;
        //		private Region DrawingInvalid;
        //		private Region SelectInvalid;
        //		private Region ActiveInvalid;
        // Region führt bei zu vielen Aufrufen zum Stack Overflow
        private Rectangle BackgroundInvalid;
        private Rectangle DrawingInvalid;
        private Rectangle SelectInvalid;
        private Rectangle ActiveInvalid;
        
        /// <summary>
        /// Returns the smallest rectangle which contains both given rectangles. An empty rectangle is ignored, i.e. the other
        /// rectangle is returned.
        /// </summary>
        /// <param name="r1">First rectangle (pixel coordinates)</param>
        /// <param name="r2">Second rectangle (pixel coordinates)</param>
        /// <returns>The union of the two rectangles</returns>
        static public Rectangle Union(Rectangle r1, Rectangle r2)
        {
            if (r1.IsEmpty) return r2;
            if (r2.IsEmpty) return r1;
            int left = Math.Min(r1.Left, r2.Left);
            int top = Math.Min(r1.Top, r2.Top);
            int right = Math.Max(r1.Right, r2.Right);
            int bottom = Math.Max(r1.Bottom, r2.Bottom);
            return Rectangle.FromLTRB(left, top, right, bottom);
        }

        static public Rectangle RectangleFromPoints(params Point[] PointList)
        {
            if (PointList.Length == 0) return Rectangle.Empty;
            int left = int.MaxValue;
            int right = int.MinValue;
            int top = int.MaxValue;
            int bottom = int.MinValue;
            foreach (Point p in PointList)
            {
                if (p.X < left) left = p.X;
                if (p.X > right) right = p.X;
                if (p.Y < top) top = p.Y;
                if (p.Y > bottom) bottom = p.Y;
            }
            return Rectangle.FromLTRB(left, top, right, bottom);
        }

        public void InvalidateBackground(Rectangle ext)
        {
            BackgroundInvalid = Union(BackgroundInvalid, ext);
        }

        public void InvalidateDrawing(Rectangle ext)
        {
            DrawingInvalid = Union(DrawingInvalid, ext);
        }

        public void InvalidateSelect(Rectangle ext)
        {
            SelectInvalid = Union(SelectInvalid, ext);
        }

        public void InvalidateActive(Rectangle ext)
        {
            ActiveInvalid = Union(ActiveInvalid, ext);
        }

        public void ForceInvalidateAll()
        {
        }
        //private void Scroll(Bitmap ToScroll, ref Rectangle InvalidRect, int OffsetX, int OffsetY)
        //{
        //    System.Drawing.Graphics gr = System.Drawing.Graphics.FromImage(ToScroll);
        //    System.Drawing.Bitmap ToScrollClone = (System.Drawing.Bitmap)ToScroll.Clone();
        //    gr.Clear(Color.FromArgb(0, 0, 0, 0));
        //    gr.DrawImageUnscaled(ToScrollClone, OffsetX, OffsetY);
        //    Rectangle clr = Ctrl.ClientRectangle;
        //    Rectangle invalid;

        //    if (OffsetX == 0)
        //    {
        //        if (OffsetY > 0) invalid = new Rectangle(clr.Left, clr.Top, clr.Width, OffsetY);
        //        else invalid = new Rectangle(clr.Left, clr.Bottom + OffsetY, clr.Width, -OffsetY);
        //    }
        //    else if (OffsetY == 0)
        //    {
        //        if (OffsetX < 0) invalid = new Rectangle(clr.Right + OffsetX, clr.Top, -OffsetX, clr.Height);
        //        else invalid = new Rectangle(clr.Left, clr.Top, OffsetX, clr.Height);
        //    }
        //    else
        //    {
        //        invalid = clr; // dann halt alles, wenn schief gescrollt wurde
        //    }

        //    if (!InvalidRect.IsEmpty) InvalidRect.Offset(OffsetX, OffsetY);
        //    InvalidRect = Union(InvalidRect, invalid);
        //}
        public void HScroll(int Offset)
        {
            if (Offset == 0) return;
            //Scroll(Background, ref BackgroundInvalid, Offset, 0);
            //Scroll(MainDrawing, ref DrawingInvalid, Offset, 0);
            //Scroll(ActiveObjects, ref ActiveInvalid, Offset, 0);
            //Scroll(Select, ref SelectInvalid, Offset, 0);
        }
        public void VScroll(int Offset)
        {
            if (Offset == 0) return;
            //Scroll(Background, ref BackgroundInvalid, 0, Offset);
            //Scroll(MainDrawing, ref DrawingInvalid, 0, Offset);
            //Scroll(ActiveObjects, ref ActiveInvalid, 0, Offset);
            //Scroll(Select, ref SelectInvalid, 0, Offset);
        }
    }
}
