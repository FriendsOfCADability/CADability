using System;
using System.Drawing;
using MouseEventArgs = CADability.Substitutes.MouseEventArgs;
using DragEventArgs = CADability.Substitutes.DragEventArgs;
using MouseButtons = CADability.Substitutes.MouseButtons;
using DragDropEffects = CADability.Substitutes.DragDropEffects;
using Keys = CADability.Substitutes.Keys;

namespace CADability.Actions
{
    /// <summary>
    /// Mouse action of a <see cref="LayoutView"/> that lets the user change the area of a
    /// <see cref="LayoutPatch"/> on the paper by dragging: dragging inside the area moves the model within
    /// it, dragging outside moves the whole area with its model, dragging an edge moves that edge. The
    /// cursor shows which of these would happen. The action is set while the patch's property entry has the
    /// focus in the control center and removes itself when another action takes over or the views change.
    /// Its id is "ModifyLayoutPatch".
    /// </summary>
    internal class ModifyLayoutPatch : CADability.Actions.Action
    {
        private LayoutView layoutView;
        private LayoutPatch layoutPatch;
        private enum Position { inside, outside, left, right, bottom, top }
        private Position currentPosition;
        private GeoPoint2D downPos;
        private GeoPoint2D lastPos;
        public ModifyLayoutPatch(LayoutPatch layoutPatch, LayoutView layoutView)
        {
            this.layoutView = layoutView;
            this.layoutPatch = layoutPatch;
            base.OnlyThisView = layoutView; // nur für diesen view wird gearbeitet
        }
#region Action members
        public override string GetID()
        {
            return "ModifyLayoutPatch";
        }
        public override void OnActivate(Action OldActiveAction, bool SettingAction)
        {
            base.OnActivate(OldActiveAction, SettingAction);
            layoutView.RepaintActionEvent += new LayoutView.RepaintActionDelegate(OnRepaint);
            //(layoutView as IView).SetPaintHandler(PaintBuffer.DrawingAspect.Active,repaintView);
            (layoutView as IView).Invalidate(PaintBuffer.DrawingAspect.Active, (layoutView as IView).Canvas.ClientRectangle);
        }

        public override void OnInactivate(Action NewActiveAction, bool RemovingAction)
        {
            if (!RemovingAction)
            {   // es wird von einer anderen Aktion überladen, als Finger weg!
                RemoveThisAction();
            }
            layoutView.RepaintActionEvent -= new LayoutView.RepaintActionDelegate(OnRepaint);
            //(layoutView as IView).RemovePaintHandler(PaintBuffer.DrawingAspect.Active, repaintView);
            base.OnInactivate(NewActiveAction, RemovingAction);
            if ((layoutView as IView).Canvas!= null)
            {
                (layoutView as IView).Invalidate(PaintBuffer.DrawingAspect.Active, (layoutView as IView).Canvas.ClientRectangle);
            }
        }
        private void setCursor(IView vw)
        {
            switch (currentPosition)
            {
                case Position.inside:
                    vw.SetCursor("SizeAll");
                    break;
                case Position.outside:
                    vw.SetCursor("NoMove2D");
                    break;
                case Position.left:
                case Position.right:
                    vw.SetCursor("SizeWE");
                    break;
                case Position.bottom:
                case Position.top:
                    vw.SetCursor("SizeNS");
                    break;
            }
        }
        public override void OnMouseDown(MouseEventArgs e, IView vw)
        {
            lastPos = downPos = layoutView.screenToLayout * new GeoPoint2D(e.X, e.Y);
        }
        public override void OnMouseMove(MouseEventArgs e, IView vw)
        {
            if (e.Button == MouseButtons.None)
            {
                GeoPoint2D p = layoutView.screenToLayout * new GeoPoint2D(e.X, e.Y);
                double fr = Math.Abs(layoutView.screenToLayout * 3); // 3 Pixel ModOp2D
                                                                     // p im LayoutSystem
                BoundingRect ext;
                if (layoutPatch.Area == null)
                {
                    ext = new BoundingRect(0.0, 0.0, layoutView.Layout.PaperWidth, layoutView.Layout.PaperHeight);
                }
                else
                {
                    ext = layoutPatch.Area.Extent;
                }
                BoundingRect.Position pos = ext.GetPosition(p, fr);
                if (pos == BoundingRect.Position.inside)
                {
                    currentPosition = Position.inside;
                }
                else if (pos == BoundingRect.Position.outside)
                {
                    currentPosition = Position.outside;
                }
                else
                {
                    if (Math.Abs(ext.Bottom - p.y) < fr) currentPosition = Position.bottom;
                    if (Math.Abs(ext.Top - p.y) < fr) currentPosition = Position.top;
                    if (Math.Abs(ext.Left - p.x) < fr) currentPosition = Position.left;
                    if (Math.Abs(ext.Right - p.x) < fr) currentPosition = Position.right;
                }
                setCursor(vw);
            }
            else if (e.Button == MouseButtons.Left)
            {
                setCursor(vw);
                GeoPoint2D p = layoutView.screenToLayout * new GeoPoint2D(e.X, e.Y);
                BoundingRect ext;
                if (layoutPatch.Area == null)
                {
                    ext = new BoundingRect(0.0, 0.0, layoutView.Layout.PaperWidth, layoutView.Layout.PaperHeight);
                }
                else
                {
                    ext = layoutPatch.Area.Extent;
                }
                double xPos = 0.0;
                double yPos = 0.0;
                switch (currentPosition)
                {
                    case Position.inside:
                        xPos = p.x - lastPos.x;
                        yPos = p.y - lastPos.y;
                        lastPos = p;
                        break;
                    case Position.outside:
                        xPos = p.x - lastPos.x;
                        yPos = p.y - lastPos.y;
                        ext.Move(new GeoVector2D(xPos, yPos));
                        lastPos = p;
                        break;
                    case Position.left:
                        ext.Left = p.x;
                        break;
                    case Position.right:
                        ext.Right = p.x;
                        break;
                    case Position.bottom:
                        ext.Bottom = p.y;
                        break;
                    case Position.top:
                        ext.Top = p.y;
                        break;
                }
                layoutPatch.Area = ext.ToBorder();
                if (xPos != 0.0 || yPos != 0.0) layoutView.Layout.MovePatch(layoutPatch, xPos, yPos);
                (layoutView as IView).Invalidate(PaintBuffer.DrawingAspect.Active, (layoutView as IView).Canvas.ClientRectangle);
            }
        }
        public override void OnMouseUp(MouseEventArgs e, IView vw)
        {
            (layoutView as IView).Invalidate(PaintBuffer.DrawingAspect.Active, (layoutView as IView).Canvas.ClientRectangle);
            layoutPatch.RefreshShowProperties();
            layoutView.Repaint();
        }
        public override void OnViewsChanged()
        {
            RemoveThisAction();
            base.OnViewsChanged();
        }
#endregion
        void OnRepaint(IView View, IPaintTo3D paintTo3D)
        {
            BoundingRect ext;
            if (layoutPatch.Area == null)
            {
                ext = new BoundingRect(0.0, 0.0, layoutView.Layout.PaperWidth, layoutView.Layout.PaperHeight);
            }
            else
            {
                ext = layoutPatch.Area.Extent;
            }
            LayoutView lv = View as LayoutView;
            if (lv != null)
            {
                GeoPoint2D clipll = lv.layoutToScreen * ext.GetLowerLeft();
                GeoPoint2D clipur = lv.layoutToScreen * ext.GetUpperRight();
                paintTo3D.SetColor(Color.LightGray);
                paintTo3D.FillRect2D(clipll.PointF, clipur.PointF);
            }
        }
    }
}
