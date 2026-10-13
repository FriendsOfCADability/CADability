using CADability.UserInterface;
#if WEBASSEMBLY
using CADability.WebDrawing;
#else
using System.Drawing;
#endif

using MouseEventArgs = CADability.Substitutes.MouseEventArgs;
using DragEventArgs = CADability.Substitutes.DragEventArgs;
using MouseButtons = CADability.Substitutes.MouseButtons;
using DragDropEffects = CADability.Substitutes.DragDropEffects;
using Keys = CADability.Substitutes.Keys;

namespace CADability.Actions
{
    /// <summary>
    /// 
    /// </summary>

    public class ZoomAction : Action, IIntermediateConstruction
    {
        private int Mode; // 0: 1. Punkt setzen, 2: 2. Punkt setzen, -1: nicht aktiv
        Point FirstPoint, SecondPoint;
        private IView activeView; // nur dort wird gezoomt. Dort, wo der erste Punkt gesetzt wurde
        public ZoomAction()
        {
            Mode = -1;
            activeView = null;
        }
        private void RepaintZoomRect(Rectangle IsInvalid, IView View, IPaintTo3D PaintToActive)
        {
            if (View != activeView) return; // nur ein Fadenkreuz bzw. Rechteck
            Color bckgnd = Frame.GetColorSetting("Colors.Background", Color.AliceBlue);
            Color infocolor;
            if (bckgnd.GetBrightness() > 0.5) infocolor = Color.Black;
            else infocolor = Color.White;
            Rectangle ClipRect = View.Canvas.ClientRectangle;
            switch (Mode)
            {
                case 0: // Fadenkreuz zeichnen
                    PaintToActive.SetColor(infocolor);
                    PaintToActive.Line2D(FirstPoint.X, ClipRect.Top, FirstPoint.X, ClipRect.Bottom);
                    PaintToActive.Line2D(ClipRect.Left, FirstPoint.Y, ClipRect.Right, FirstPoint.Y);
                    break;
                case 1: // Rechteck zeichnen
                    PaintToActive.SetColor(infocolor);
                    PaintToActive.Line2D(FirstPoint.X, FirstPoint.Y, SecondPoint.X, FirstPoint.Y);
                    PaintToActive.Line2D(SecondPoint.X, FirstPoint.Y, SecondPoint.X, SecondPoint.Y);
                    PaintToActive.Line2D(SecondPoint.X, SecondPoint.Y, FirstPoint.X, SecondPoint.Y);
                    PaintToActive.Line2D(FirstPoint.X, SecondPoint.Y, FirstPoint.X, FirstPoint.Y);
                    break;
            }
        }
        /// <summary>
        /// Implements <see cref="Action.GetID"/>.
        /// </summary>
        public override string GetID()
        {
            return "Zoom[LeaveSelectProperties]";
        }
        /// <summary>
        /// Implements <see cref="Action.OnSetAction"/>. If you override this method
        /// don't forget to call the bas implementation.
        /// </summary>
        public override void OnSetAction()
        {
            Mode = 0;
            base.OnSetAction();
        }
        /// <summary>
        /// Overrides <see cref="CADability.Actions.Action.OnRemoveAction ()"/>
        /// </summary>
		public override void OnRemoveAction()
        {
            Mode = -1;
        }
        /// <summary>
        /// Implements <see cref="Action.OnMouseMove"/>. If you override this method
        /// don't forget to call the bas implementation.
        /// </summary>
        /// <param name="e">MouseEventArgs of the call</param>
        /// <param name="vw">the IView which contains the mouse</param>
        public override void OnMouseMove(MouseEventArgs e, IView vw)
        {
            switch (Mode)
            {
                case 0:
                    activeView = vw;
                    vw.Invalidate(PaintBuffer.DrawingAspect.Active, Rectangle.FromLTRB(vw.DisplayRectangle.Left, FirstPoint.Y, vw.DisplayRectangle.Right, FirstPoint.Y + 1));
                    vw.Invalidate(PaintBuffer.DrawingAspect.Active, Rectangle.FromLTRB(FirstPoint.X, vw.DisplayRectangle.Top, FirstPoint.X + 1, vw.DisplayRectangle.Bottom));
                    FirstPoint = new Point(e.X, e.Y);
                    vw.Invalidate(PaintBuffer.DrawingAspect.Active, Rectangle.FromLTRB(vw.DisplayRectangle.Left, FirstPoint.Y, vw.DisplayRectangle.Right, FirstPoint.Y + 1));
                    vw.Invalidate(PaintBuffer.DrawingAspect.Active, Rectangle.FromLTRB(FirstPoint.X, vw.DisplayRectangle.Top, FirstPoint.X + 1, vw.DisplayRectangle.Bottom));
                    break;
                case 1:
                    Rectangle Clip = PaintBuffer.RectangleFromPoints(FirstPoint, SecondPoint, new Point(e.X, e.Y));
                    Clip.Width += 1;
                    Clip.Height += 1;
                    vw.Invalidate(PaintBuffer.DrawingAspect.Active, Clip);
                    SecondPoint = new Point(e.X, e.Y);
                    break;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.Actions.Action.OnMouseUp (MouseEventArgs, IView)"/>
        /// </summary>
        /// <param name="e"></param>
        /// <param name="vw"></param>
		public override void OnMouseUp(MouseEventArgs e, IView vw)
        {
            OnMouseMove(e, vw); // damit wird Invalidate... aufgerufen
            switch (Mode)
            {
                case 0:
                    Mode = 1;
                    SecondPoint = FirstPoint;
                    break;
                case 1:
                    Mode = -1;
                    BoundingRect mm = BoundingRect.EmptyBoundingRect;
                    mm.MinMax(vw.Projection.PointWorld2D(FirstPoint));
                    mm.MinMax(vw.Projection.PointWorld2D(SecondPoint));
                    base.RemoveThisAction(); // damit werden auch die PaintHandler abgemeldet
                                             // und es gibt noch ungültige Bereiche im Active. D.h. es wird gelöscht.
                    vw.ZoomToRect(mm);
                    break;
            }
        }
        /// <summary>
        /// Implements <see cref="Action.OnMouseDown"/>. If you override this method
        /// don't forget to call the bas implementation.
        /// </summary>
        /// <param name="e">MouseEventArgs of the call</param>
        /// <param name="vw">the IView which contains the mouse</param>
        public override void OnMouseDown(MouseEventArgs e, IView vw)
        {
            if (Mode == 0 && Frame.GetBooleanSetting("ZoomAction.SingleClick", false))
            {
                OnMouseUp(e, vw); // schaltet in Modus 1 weiter
            }
        }

        //		private Control FindFocus(Control startwith)
        //		{
        // kleine Tool um rauszufinden wer gerade den focus hat
        //			if (startwith.Focused) return startwith;
        //			for (int i=0; i<startwith.Controls.Count; ++i)
        //			{
        //				Control f = FindFocus(startwith.Controls[i]);
        //				if (f!=null) return f;
        //			}
        //			return null;
        //		}
        /// <summary>
        /// Implements <see cref="Action.OnActivate"/>. If you override this method
        /// don't forget to call the bas implementation.
        /// </summary>
        public override void OnActivate(Action OldActiveAction, bool SettingAction)
        {
            foreach (IView vw in Frame.AllViews)
            {
                //vw.SetPaintHandler(PaintBuffer.DrawingAspect.Active, new RepaintView(RepaintZoomRect));
                vw.SetPaintHandler(PaintBuffer.DrawingAspect.Active, new PaintView(RepaintZoomRect));
            }
            base.OnActivate(OldActiveAction, SettingAction);
            Frame.SetControlCenterFocus("Action", null, false, false);
        }
        /// <summary>
        /// Implements <see cref="Action.OnInactivate"/>. If you override this method
        /// don't forget to call the bas implementation.
        /// </summary>
        /// <param name="NewActiveAction">the action that will become active</param>
        /// <param name="RemovingAction">true: if called because the action is removed, false: if called when a new action is set on top of this action</param>
        public override void OnInactivate(Action NewActiveAction, bool RemovingAction)
        {
            foreach (IView vw in Frame.AllViews)
            {
                vw.InvalidateAll();
                //vw.RemovePaintHandler(PaintBuffer.DrawingAspect.Active,new RepaintView(RepaintZoomRect));
            }
            if (!RemovingAction)
            {
                base.RemoveThisAction();
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.Actions.Action.OnEscape ()"/>
        /// </summary>
		public override bool OnEscape()
        {
            base.RemoveThisAction();
            return true;
        }

#region IIntermediateConstruction Members

        IPropertyEntry IIntermediateConstruction.GetHandledProperty()
        {
            return null;
        }

        bool IIntermediateConstruction.Succeeded
        {
            get { return true; }
        }

#endregion
    }
}
