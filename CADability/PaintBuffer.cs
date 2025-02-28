using System;
#if WEBASSEMBLY
using CADability.WebDrawing;
using Point = CADability.WebDrawing.Point;
#else
using System.Drawing;
using Point = System.Drawing.Point;
#endif

namespace CADability
{
    /// <summary>
    /// Klasse, die die Darstellung auf dem Bildschirm realisiert.
    /// Die Klasse hält (z.Z.) 4 Bitmaps, die nacheinander und transparent
    /// in das Fenster (genauer in das Graphics Objekt des Parameters e beim 
    /// Aufruf von Compose) kopiert werden. Für jedes Bitmap gibt es ein eigenes
    /// "Invalid" Rechteck bzw. Region. Der Ablauf ist wie folgt:
    /// 1. irgend etwas ändert sich (z.B das Raster, die Markierung, ein 
    /// geometrisches Objekt u.s.w.). Das muss zur Folge haben, dass InvalidateXxx
    /// für den entsprechenden Aspekt (z.B. InvalidateDrawing für die 
    /// geometrischen Objekte, InvalidateSelect für die Markierung) aufgerufen 
    /// wird. Die Invalid-Bereiche werden für die einzelnen Aspekte akkumuliert.
    /// Irgendwann erfolgt der Aufruf von Compose (gewöhnlich ausgelöst durch den 
    /// Paint Event des Controls)Dort versucht nun diese Klasse das Bild neu 
    /// zusammenzusetzten. Ist der Invalid-Bereich eines Aspektes leer, dann ist
    /// das zugehörige Bitmap aktuell, d.h. kann so verwendet werden. Wenn nicht,
    /// so muss der Bereich neu gezeichnet werden. Compose löst den RepaintXxxEvent
    /// (z.B. RepaintDrawingEvent) aus. Die Handler dieses Events (gewöhnlich 
    /// nur einer) bekommen ein PaintToGDI Objekt, mit dessen Hilfe sie zeichnen
    /// können. 
    /// </summary>

    public class PaintBuffer
    {
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
        /// liefert einRechteck, welches die beiden im Parameter gegeben Rechtecke umfasst
        /// </summary>
        /// <param name="r1"></param>
        /// <param name="r2"></param>
        /// <returns></returns>
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
