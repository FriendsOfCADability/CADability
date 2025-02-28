using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Graphics = System.Drawing.Graphics;

namespace CADability.Forms
{
    /// <summary>
    /// Implements a Windows.Forms.Control to render a CADability view with OpenGL or GDI.
    /// </summary>
    public class CadCanvas : Control, ICanvas
    {

        private static CADability.Substitutes.DragEventArgs Subst(System.Windows.Forms.DragEventArgs v)
        {   // CADability doesn't use Windows.Forms. Substitutes contains a simple replacement of DragEventArgs (and some other Windows.Forms classes and enums).
            // here it is the case, that the property Effect has a feedback to the system, which is implemented via an event.
            void EffectChanged(Substitutes.DragEventArgs e)
            {
                v.Effect = (DragDropEffects)e.Effect;
            }
            Substitutes.DragEventArgs res = new Substitutes.DragEventArgs()
            {
                Data = v.Data,
                KeyState = v.KeyState,
                X = v.X,
                Y = v.Y,
                AllowedEffect = (CADability.Substitutes.DragDropEffects)v.AllowedEffect,
                Effect = (CADability.Substitutes.DragDropEffects)v.AllowedEffect,
            };
            res.EffectChanged += EffectChanged;
            return res;
        }
        private static CADability.Substitutes.MouseEventArgs Subst(System.Windows.Forms.MouseEventArgs v)
        {
            return new Substitutes.MouseEventArgs()
            {
                Button = (CADability.Substitutes.MouseButtons)v.Button,
                Clicks = v.Clicks,
                X = v.X,
                Y = v.Y,
                Delta = v.Delta,
                Location = v.Location
            };
        }
        private static CADability.Substitutes.PaintEventArgs Subst(System.Windows.Forms.PaintEventArgs v)
        {
            return new Substitutes.PaintEventArgs()
            {
                ClipRectangle = v.ClipRectangle,
                Graphics = v.Graphics
            };
        }
        private IView view;
        private IPaintTo3D paintTo3D;
        private Rectangle lastClientRect;
        private ToolTip toolTip;
        private string currenToolTip;
        private string currentCursor;
        private Action<int> callbackCollapsed;
        public CadCanvas() : base()
        {
            toolTip = new ToolTip();
            toolTip.InitialDelay = 500;
        }

        #region ICanvas implementation
        static Dictionary<string, Cursor> cursorTable = new Dictionary<string, Cursor>();

        Rectangle ICanvas.ClientRectangle => base.ClientRectangle;
        public IFrame Frame { get; set; }
        string ICanvas.Cursor
        {
            get
            {
                return currentCursor;
            }
            set
            {   // if cursorTable doesn't contain the cursor, try to get it from the resources or from System.Windows.Forms.Cursors
                if (!cursorTable.TryGetValue(value, out Cursor cursor))
                {
                    Assembly ThisAssembly = Assembly.GetExecutingAssembly();
                    System.IO.Stream str = ThisAssembly.GetManifestResourceStream("CADability.Forms.Cursors." + value + ".cur");
                    if (str != null)
                    {
                        using (str)
                        {
                            if (str != null) cursor = new Cursor(str);
                        }
                    }
                    else
                    {   // the name is one of the System.Windows.Forms.Cursors properties
                        PropertyInfo propertyInfo = typeof(Cursors).GetProperty(value);
                        if (propertyInfo != null) cursor = propertyInfo.GetMethod.Invoke(null, new object[] { }) as Cursor;
                    }
                    cursorTable[value] = cursor;
                }
                currentCursor = value;
                if (cursor != null) base.Cursor = cursor; // sets the cursor for the Control
            }
        }
        IPaintTo3D ICanvas.PaintTo3D
        {
            get
            {
                return paintTo3D;
            }
        }
        void ICanvas.Invalidate()
        {
            Invalidate();
        }
        public event Action<ICanvas> OnPaintDone;
        IView ICanvas.GetView()
        {
            return view as IView;
        }
        void ICanvas.ShowView(IView toShow)
        {
            if (paintTo3D is PaintToOpenGL openGL)
            {
                openGL.Disconnect(this);
            }
            view = toShow;
            switch (toShow.PaintType)
            {
                case "GDI":
                    // create the GDI paint machine
                    this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
                    this.SetStyle(ControlStyles.OptimizedDoubleBuffer, false);
                    paintTo3D = new PaintToGDI(toShow.Projection, this.CreateGraphics());
                    break;
                case "3D":
                default:
                    {
                        // create the OpenGL machine
                        this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
                        this.SetStyle(ControlStyles.OptimizedDoubleBuffer, false);
                        this.DoubleBuffered = false;
                        PaintToOpenGL paintToOpenGL = new PaintToOpenGL(1e-6);
                        paintToOpenGL.Init(this);
                        paintTo3D = paintToOpenGL;
                    }
                    break;
            }
            view.Connect(this);
            Invalidate();
        }
        System.Drawing.Point ICanvas.PointToClient(System.Drawing.Point mousePosition)
        {
            return this.PointToClient(mousePosition);
        }
        void ICanvas.ShowContextMenu(MenuWithHandler[] contextMenu, System.Drawing.Point viewPosition, Action<int> collapsed)
        {
            ContextMenuWithHandler cm = MenuManager.MakeContextMenu(contextMenu);
            cm.UpdateCommand();
            cm.Show(this, viewPosition);
        }
        Substitutes.DragDropEffects ICanvas.DoDragDrop(GeoObjectList dragList, Substitutes.DragDropEffects all)
        {
            return (CADability.Substitutes.DragDropEffects)DoDragDrop(dragList, (DragDropEffects)all);
        }
        void ICanvas.ShowToolTip(string toDisplay)
        {
            if (currenToolTip != toDisplay)
            {
                if (toDisplay == null) toolTip.Hide(this);
                else
                {
                    if (currenToolTip != null) toolTip.Hide(this);
                    System.Drawing.Point mp = PointToClient(MousePosition);
                    mp.Y -= Font.Height;
                    toolTip.Show(toDisplay, this, mp, 2000);
                }
                currenToolTip = toDisplay;
            }
        }
        #endregion
        #region Control overrides to be forwarded to the view
        public override bool AllowDrop
        {
            get
            {
                return (view != null) ? view.AllowDrop : false;
            }
            set
            {
                base.AllowDrop = value;
            }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (view != null && paintTo3D != null)
            {
                view.OnPaint(Subst(e));
                // maybe we need because of OpenGL list
                //if (this.InvokeRequired)
                //{
                //    this.BeginInvoke((Action)delegate ()
                //    {
                //        view.OnPaint(Subst(e));
                //    });
                //}
                //else
                //{
                //    view.OnPaint(Subst(e));
                //}
            }
            else e.Graphics.FillRectangle(new SolidBrush(Color.BlanchedAlmond), e.ClipRectangle);
            OnPaintDone?.Invoke(this);
        }
        protected override void Dispose(bool disposing)
        {
            if (view != null && paintTo3D != null) (paintTo3D as PaintToOpenGL)?.Disconnect(this);
            view?.Disconnect(this);
            base.Dispose(disposing);
        }
        protected override void OnMouseClick(MouseEventArgs e)
        {   // click seems not to be processed
            base.OnMouseClick(e);
            this.Focus();
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            view?.OnMouseDown(Subst(e));
            base.OnMouseDown(e);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            view?.OnMouseMove(Subst(e));
            base.OnMouseMove(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            view?.OnMouseUp(Subst(e));
            base.OnMouseUp(e);
        }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            view?.OnMouseWheel(Subst(e));
            base.OnMouseWheel(e);
        }
        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            view?.OnMouseDoubleClick(Subst(e));
            base.OnMouseDoubleClick(e);
        }
        protected override void OnMouseEnter(EventArgs e)
        {
            view?.OnMouseEnter(e);
            base.OnMouseEnter(e);
        }
        protected override void OnMouseHover(EventArgs e)
        {
            view?.OnMouseHover(e);
            base.OnMouseHover(e);
        }
        protected override void OnMouseLeave(EventArgs e)
        {
            view?.OnMouseLeave(e);
            base.OnMouseLeave(e);
        }
        protected override void OnSizeChanged(System.EventArgs e)
        {
            if (view != null) view.OnSizeChanged(lastClientRect);
            base.OnSizeChanged(e);
            if (ClientRectangle.Width > 0 && this.ClientRectangle.Height > 0)
            {
                lastClientRect = this.ClientRectangle;
            }
        }
        protected override void OnDragDrop(DragEventArgs drgevent)
        {
            view?.OnDragDrop(Subst(drgevent));
            base.OnDragDrop(drgevent);
        }
        protected override void OnDragEnter(DragEventArgs drgevent)
        {
            view?.OnDragEnter(Subst(drgevent));
            base.OnDragEnter(drgevent);
        }
        protected override void OnDragLeave(EventArgs e)
        {
            view?.OnDragLeave(e);
            base.OnDragLeave(e);
        }
        protected override void OnDragOver(DragEventArgs drgevent)
        {
            System.Drawing.Point p = new System.Drawing.Point(drgevent.X, drgevent.Y);

            view?.OnDragOver(Subst(drgevent));
            base.OnDragOver(drgevent);
        }
        #endregion
        /* named main colors:
        White
        Silver
        Gray	
        Black
        Red	#F
        Maroon
        Yellow
        Olive
        Lime	
        Green
        Aqua	
        Teal	
        Blue	
        Navy	
        Fuchsia
        Purple
        */
    }

}
