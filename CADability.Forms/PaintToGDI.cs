using CADability.Attribute;
using CADability.Curve2D;
using CADability.GeoObject;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using Wintellect.PowerCollections;

namespace CADability.Forms
{

    public class PaintToGDI : IDisposable, IPaintTo3D
    {
        class DisplayList : IPaintTo3DList
        {   // eine Liste ist einfach ein Bitmap
            // im Falle von Zoom oder Größenänderung wird die Displayliste ungültig, dafür fehlt noch der Mechanismus
            public System.Drawing.Bitmap bitmap;
            public Graphics graphics;
            public DisplayList(int width, int height)
            {
                bitmap = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                graphics = Graphics.FromImage(bitmap);
                Brush br = new SolidBrush(Color.FromArgb(0, 0, 0, 0)); // transparent
                graphics.FillRectangle(br, 0, 0, width, height);
            }
            public void Close()
            {
                graphics.Dispose();
                //? bitmap.Save("tmp.jpg", System.Drawing.Imaging.ImageFormat.Jpeg);
            }
            #region IPaintTo3DList Members
            string name;
            string IPaintTo3DList.Name
            {
                get
                {
                    return name;
                }
                set
                {
                    name = value;
                }
            }
            System.Collections.Generic.List<IPaintTo3DList> IPaintTo3DList.containedSubLists
            {
                set { }
            }
            public void Dispose()
            {
                bitmap.Dispose();
            }
            #endregion
        }
        protected Projection projection;
        Stack<Projection> projectionStack;
        protected Graphics graphics;
        protected GraphicsPath graphicsPath; // wenn!=null wird darauf gezeichnet
        DisplayList displayList;
        Graphics oldGraphics;
        bool thinLinesOnly;
        private static Set<string> fontFamilyNames;
        internal static Set<string> FontFamilyNames
        {
            get
            {
                if (fontFamilyNames == null)
                {
                    FontFamily[] ff = FontFamily.Families;
                    fontFamilyNames = new Set<string>();
                    for (int i = 0; i < ff.Length; i++)
                    {
                        fontFamilyNames.Add(ff[i].Name.ToUpper());
                    }
                }
                return fontFamilyNames;
            }
        }

        public PaintToGDI(Projection projection, Graphics graphics)
            : this()
        {
            this.graphics = graphics;
            this.projection = projection;
            this.precision = projection.Precision;
            paintSurfaces = projection.ShowFaces;
            bool singleBitText = Settings.GlobalSettings.GetBoolValue("GDI.SingleBitText", false);
            if (singleBitText) graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixel;
        }
        protected PaintToGDI()
        {
            projectionStack = new Stack<Projection>();
        }

        /// <summary>
        /// Gets or sets the projection
        /// </summary>
        public Projection Projection
        {
            get
            {
                return projection;
            }
            set
            {
                projection = value;
            }
        }
        /// <summary>
        /// Sets the center of the 2D area
        /// </summary>
        /// <param name="center"></param>
        public void SetCenter(GeoPoint2D center)
        {
            double factor, dx, dy;
            projection.GetPlacement(out factor, out dx, out dy);
            projection.SetPlacement(factor, center.x, center.y);
        }
        public void SetScaling(double scalingFactor)
        {
            double factor, dx, dy;
            projection.GetPlacement(out factor, out dx, out dy);
            projection.SetPlacement(scalingFactor, dx, dy);
        }
        /// <summary>
        /// Fits the provided rectangle to the drawing area
        /// </summary>
        /// <param name="fitTo">Rectangle in the projected world coordinate system</param>
        /// <param name="distort">Distort the projection if different aspect ratio</param>
        public void Fit(BoundingRect fitTo, bool distort)
        {
            projection.SetPlacement(graphics.ClipBounds, fitTo);
        }
        public bool ThinLinesOnly
        {
            get
            {
                return thinLinesOnly;
            }
            set
            {
                thinLinesOnly = value;
            }
        }
        #region IPaintTo3D Members
        internal class TransformWobble
        {
            private Graphics graphics;
            private Matrix previousTransform;
            int mode;
            float offset;
            public TransformWobble(Graphics graphics)
                : this(graphics, 1.0)
            {
            }
            public TransformWobble(Graphics graphics, double offset)
            {
                this.graphics = graphics;
                previousTransform = graphics.Transform;
                this.offset = (float)(2.0 * offset);
                graphics.TranslateTransform((float)offset, (float)offset, MatrixOrder.Append);
                mode = 0;
            }
            public TransformWobble Next()
            {
                switch (++mode)
                {
                    case 1:
                        graphics.TranslateTransform(0.0f, -offset, MatrixOrder.Append);
                        return this;
                    case 2:
                        graphics.TranslateTransform(-offset, 0.0f, MatrixOrder.Append);
                        return this;
                    case 3:
                        graphics.TranslateTransform(0.0f, offset, MatrixOrder.Append);
                        return this;
                    default:
                        graphics.Transform = previousTransform;
                        return null;
                }
            }
        }

        internal class Transform : IDisposable
        {
            private Graphics graphics;
            private Matrix previousTransform;
            public Transform(Graphics graphics, Matrix Transform)
            {
                this.graphics = graphics;
                previousTransform = graphics.Transform;
                graphics.Transform = Transform;
            }
            public Transform(Graphics graphics, Matrix Transform, bool Append)
            {
                this.graphics = graphics;
                previousTransform = graphics.Transform;
                Matrix newTransform = previousTransform.Clone();
                if (Append) newTransform.Multiply(Transform, MatrixOrder.Append);
                else newTransform.Multiply(Transform, MatrixOrder.Prepend);
                graphics.Transform = newTransform;
            }
            #region IDisposable Members

            public void Dispose()
            {
                this.graphics.Transform = previousTransform;
            }

            #endregion
        }

        bool paintSurfaces = false;
        bool IPaintTo3D.PaintSurfaces
        {
            get { return paintSurfaces; }
        }

        bool paintEdges = true;
        bool IPaintTo3D.PaintEdges
        {
            get { return paintEdges; }
        }

        bool paintSurfaceEdges = true;
        bool IPaintTo3D.PaintSurfaceEdges
        {
            get
            {
                return paintSurfaceEdges;
            }
            set
            {
                paintSurfaceEdges = value;
            }
        }

        bool useLineWidth;
        bool IPaintTo3D.UseLineWidth
        {
            get
            {
                return useLineWidth;
            }
            set
            {
                useLineWidth = value;
            }
        }

        double precision;
        double IPaintTo3D.Precision
        {
            get
            {
                return precision;
            }
            set
            {
                precision = value;
            }
        }

        double IPaintTo3D.PixelToWorld
        {
            get { return projection.DeviceToWorldFactor; }
        }

        bool selectMode = false;
        bool selectOnlyOutline = false;

        bool IPaintTo3D.SelectMode
        {
            get
            {
                return selectMode;
            }
            set
            {
                selectMode = value;
                selectOnlyOutline = Settings.GlobalSettings.GetBoolValue("Select.PaintOutlineOnly", false);
                // DEBUG!!!
                // selectOnlyOutline = true;
            }
        }

        Color selectColor;
        Color IPaintTo3D.SelectColor
        {
            get
            {
                return selectColor;
            }
            set
            {
                selectColor = value;
            }
        }

        bool IPaintTo3D.DelayText
        {
            get
            {
                throw new Exception("The method or operation is not implemented.");
            }
            set
            {
                throw new Exception("The method or operation is not implemented.");
            }
        }

        bool IPaintTo3D.DelayAll
        {
            get
            {
                throw new Exception("The method or operation is not implemented.");
            }
            set
            {
                throw new Exception("The method or operation is not implemented.");
            }
        }

        bool IPaintTo3D.TriangulateText
        {
            get { return false; }
            set { }
        }

        bool IPaintTo3D.DontRecalcTriangulation
        {
            get
            {
                return false;
            }
            set
            {
                throw new Exception("The method or operation is not implemented.");
            }
        }
        PaintCapabilities IPaintTo3D.Capabilities
        {
            get
            {
                // aber nicht PaintCapabilities.ZoomIndependentDisplayList
                return PaintCapabilities.CanDoArcs | PaintCapabilities.CanFillPaths;
            }
        }

        internal void Init(System.Windows.Forms.Control ctrl)
        {
            ctrl.Paint += new System.Windows.Forms.PaintEventHandler(OnPaint);
        }

        void OnPaint(object sender, System.Windows.Forms.PaintEventArgs e)
        {
            this.graphics = e.Graphics;
        }

        internal void Disconnect(System.Windows.Forms.Control ctrl)
        {
            ctrl.Paint -= new System.Windows.Forms.PaintEventHandler(OnPaint);
        }

        void IPaintTo3D.MakeCurrent()
        {

        }

        Color color;
        void IPaintTo3D.SetColor(System.Drawing.Color color, int lockColor)
        {
            if (color.ToArgb() == avoidColor.ToArgb())
                color = Color.FromArgb(255 - avoidColor.R, 255 - avoidColor.G, 255 - avoidColor.B);
            else
                this.color = color;
        }

        Color avoidColor;
        void IPaintTo3D.AvoidColor(Color color)
        {
            avoidColor = color;
        }

        LineWidth lineWidth;
        void IPaintTo3D.SetLineWidth(LineWidth lineWidth)
        {
            this.lineWidth = lineWidth;
        }

        LinePattern pattern;
        void IPaintTo3D.SetLinePattern(LinePattern pattern)
        {
            this.pattern = pattern;
        }

        void IPaintTo3D.Polyline(GeoPoint[] points)
        {
            try
            {
                PointF[] pointsf = new PointF[points.Length];
                for (int i = 0; i < points.Length; i++)
                {   // hier ist halt die Frage: enthält die Projektion schon die ganze Zoom/Scroll Info
                    // oder wird das nach graphics gesteckt. Aber das ist ja auch egal hier.
                    pointsf[i] = projection.ProjectF(points[i]);
                }
                if (graphicsPath != null)
                {
                    graphicsPath.AddLines(pointsf);
                }
                else
                {
                    using (Pen pen = MakePen())
                    {
                        graphics.DrawLines(pen, pointsf);
                    }
                }
            }
            catch (OverflowException)
            {   // Werte offensichtlich zu groß, wenn man extrem weit hineinzoomt
            }
        }

        private Pen Make1PixelPen()
        {
            Color clr;
            if (this.selectMode)
            {
                clr = selectColor;
            }
            else
            {
                clr = color;
            }

            Pen res = new Pen(clr, 1.0f);
            return res;
        }
        private Pen MakePen()
        {
            Color clr;
            if (this.selectMode)
            {
                clr = selectColor;
            }
            else
            {
                clr = color;
            }
            Pen res;
            if (lineWidth != null && lineWidth.Scale == LineWidth.Scaling.Device)
                res = new Pen(clr, (float)(lineWidth.Width));
            else if (lineWidth != null)
                res = new Pen(clr, (float)(lineWidth.Width * projection.WorldToDeviceFactor));
            else
                res = new Pen(clr, 1.0f);
            // hier könnte man einen zoom-unabhängigen Faktor implementieren
            if (thinLinesOnly) res.Width = 1.0f;
            if (pattern != null && pattern.Pattern.Length > 0)
            {
                float[] fpattern = new float[pattern.Pattern.Length];
                // float w = (float)Math.Round((double)res.Width); was sollte das Round? unlogisch und Probleme bei Mauell
                float w = res.Width;
                if (w == 0.0f) w = 1.0f;
                double fct = 1;
                if (pattern.Scale != LinePattern.Scaling.Device) fct = projection.WorldToDeviceFactor;
                float offsetNull = 0.0f; // wenn aus der 0 eine 1 wird (um einen Punkt zu zeichnen) muss der folgende Abstand entsprechend kleiner werden.
                for (int i = 0; i < fpattern.Length; i++)
                {
                    fpattern[i] = Math.Max(0.0f, (float)(pattern.Pattern[i] * fct / w) - offsetNull);
                    if (fpattern[i] == 0.0)
                    {
                        fpattern[i] = 1.0f;
                        offsetNull = 1.0f;
                    }
                    else
                    {
                        offsetNull = 0.0f;
                    }
                }
                res.DashPattern = fpattern;
                res.DashOffset = 0.5f; // eine halbe Strichstärke versetzt ist wichtig wg. Mauells Raster
            }
            // ACHTUNG: das sollte einstellbar sein:
            res.StartCap = LineCap.Round;
            res.EndCap = LineCap.Round;
            return res;
        }

        void IPaintTo3D.FilledPolyline(GeoPoint[] points)
        {
        }

        void IPaintTo3D.Points(GeoPoint[] points, float size, PointSymbol pointSymbol)
        {
        }

        void IPaintTo3D.Triangle(GeoPoint[] vertex, GeoVector[] normals, int[] indextriples)
        {
            using (Brush brush = new SolidBrush(color))
            {
                PointF[] vertexf = new PointF[vertex.Length];
                for (int i = 0; i < vertex.Length; i++)
                {
                    vertexf[i] = projection.ProjectF(vertex[i]);
                }
                for (int i = 0; i < indextriples.Length; i = i + 3)
                {
                    PointF[] plg = new PointF[3];
                    plg[0] = vertexf[indextriples[i]];
                    plg[1] = vertexf[indextriples[i + 1]];
                    plg[2] = vertexf[indextriples[i + 2]];
                    graphics.FillPolygon(brush, plg);
                }
            }
        }

        void IPaintTo3D.PrepareText(string fontName, string textString, FontStyle fontStyle)
        {

        }

        void IPaintTo3D.PrepareIcon(System.Drawing.Bitmap icon)
        {
        }

        void IPaintTo3D.Text(GeoVector lineDirection, GeoVector glyphDirection, GeoPoint location, string fontName,
            string textString, FontStyle fontStyle, CADability.GeoObject.Text.AlignMode alignment,
            CADability.GeoObject.Text.LineAlignMode lineAlignment)
        {
            // graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixel; 
            // das wäre für Kastenholz, beim selektieren von kleinen Schriften keine Ränder
            bool makeLine = false;
            PointF[] pnt = new PointF[3];
            pnt[0] = projection.ProjectF(location + glyphDirection);
            pnt[1] = projection.ProjectF(location + lineDirection + glyphDirection);
            pnt[2] = projection.ProjectF(location);
            RectangleF unitRect = new RectangleF(new PointF(0.0f, 0.0f), new SizeF(1.0f, 1.0f));
            Matrix transform = new Matrix(unitRect, pnt);
            if (transform.IsInvertible)
            {
                using (new Transform(graphics, transform))
                {
                    graphics.Transform.TransformPoints(pnt);
                    float df = Math.Abs(pnt[2].X - pnt[0].X) + Math.Abs(pnt[2].Y - pnt[0].Y);
                    if (df > 2)
                    {
                        Brush brush;
                        if (selectMode)
                        {
                            brush = new SolidBrush(selectColor);
                        }
                        else
                        {
                            brush = new SolidBrush(color);
                        }
                        FontFamily ff;
                        if (FontFamilyNames.Contains(fontName.ToUpper()))
                        {
                            ff = new FontFamily(fontName);
                        }
                        else
                        {
                            ff = new FontFamily(System.Drawing.Text.GenericFontFamilies.SansSerif);
                        }
                        //try
                        //{
                        //    ff = new FontFamily(fontName);
                        //}
                        //catch (System.ArgumentException)
                        //{
                        //    ff = new FontFamily(System.Drawing.Text.GenericFontFamilies.SansSerif);
                        //}
                        int em = ff.GetEmHeight(fontStyle);
                        int ls = ff.GetLineSpacing(fontStyle);
                        int dc = ff.GetCellDescent(fontStyle);
                        int ac = ff.GetCellAscent(fontStyle);
                        Font font = new Font(ff, 1.0f, fontStyle, GraphicsUnit.Pixel);
                        // Alignement ist wohl genau vertauscht mit LineAlignement/Alignement in WindowsForms
                        StringFormat sf = new StringFormat(StringFormatFlags.NoClip);
                        PointF loc = new PointF(0.0f, 0.0f);
                        switch (lineAlignment)
                        {
                            case Text.LineAlignMode.Left:
                                sf.Alignment = StringAlignment.Near;
                                break;
                            case Text.LineAlignMode.Center:
                                sf.Alignment = StringAlignment.Center;
                                break;
                            case Text.LineAlignMode.Right:
                                sf.Alignment = StringAlignment.Far;
                                break;
                        }
                        switch (alignment)
                        {
                            case Text.AlignMode.Bottom: loc.Y = -(ac + dc - em) / (float)em; break;
                            case Text.AlignMode.Baseline: loc.Y = (em - ac) / (float)em; break;
                            case Text.AlignMode.Center: loc.Y = -(ac / 2 + dc / 2 - em) / (float)em; break;
                            case Text.AlignMode.Top: loc.Y = 1.0f; break;
                                //case Text.AlignMode.Bottom:
                                //    sf.LineAlignment = StringAlignment.Near;
                                //    break;
                                //case Text.AlignMode.Baseline: // hier müsste man den Punkt verschieben
                                //    sf.LineAlignment = StringAlignment.Near;
                                //    //loc.Y = 
                                //    break;
                                //case Text.AlignMode.Center:
                                //    sf.LineAlignment = StringAlignment.Center;
                                //    break;
                                //case Text.AlignMode.Top:
                                //    sf.LineAlignment = StringAlignment.Far;
                                //    break;
                        }
                        sf.LineAlignment = StringAlignment.Near;
                        //System.Diagnostics.Trace.WriteLine("Text: " + selectMode.ToString() + ", " + transform.OffsetX.ToString() + ", " + transform.Elements[0].ToString() + ", "+loc.Y.ToString());
                        graphics.DrawString(textString, font, brush, loc, sf);
                    }
                    else
                    {
                        makeLine = true;       // zu klein, ein Strich?
                    }
                }
            }
            else
            {
                makeLine = true;
            }
            if (makeLine)
            {
                PointF le = projection.ProjectF(location + lineDirection);
                PointF ls = projection.ProjectF(location);
                if (Math.Abs(ls.X - le.X) + Math.Abs(ls.Y - le.Y) < 2.0)
                {
                    le.X = ls.X + 2;
                }
                Pen pen = Make1PixelPen();
                try
                {
                    graphics.DrawLine(pen, ls, le);
                }
                catch (System.OverflowException)
                {
                    // kann nicht dargestellt werden, da irgndwas zu groß ist
                }
                pen.Dispose();
            }
        }

        void IPaintTo3D.List(IPaintTo3DList paintThisList)
        {
            DisplayList dl = paintThisList as DisplayList;
            graphics.DrawImageUnscaled(dl.bitmap, 0, 0);
        }

        void IPaintTo3D.SelectedList(IPaintTo3DList paintThisList, int wobbleRadius)
        {
            DisplayList dl = paintThisList as DisplayList;
            graphics.DrawImageUnscaled(dl.bitmap, 0, 0);
            // dl.bitmap.Save("tmp.jpg", System.Drawing.Imaging.ImageFormat.Jpeg); 
        }

        void IPaintTo3D.Nurbs(GeoPoint[] poles, double[] weights, double[] knots, int degree)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        void IPaintTo3D.Line2D(int sx, int sy, int ex, int ey)
        {
            using (new Transform(graphics, new Matrix())) // Identität 
            {
                try
                {
                    Pen pen = MakePen(); // ein besonderes MakePen mit 1 pixel
                    graphics.DrawLine(pen, sx, sy, ex, ey);
                    pen.Dispose();
                }
                catch (System.OverflowException)
                {
                    // kann nicht dargestellt werden, da irgndwas zu groß ist
                }
            }
        }

        void IPaintTo3D.Line2D(PointF p1, PointF p2)
        {
            using (new Transform(graphics, new Matrix())) // Identität 
            {
                try
                {
                    Pen pen = MakePen(); // ein besonderes MakePen mit 1 pixel
                    graphics.DrawLine(pen, p1, p2);
                    pen.Dispose();
                }
                catch (System.OverflowException)
                {
                    // kann nicht dargestellt werden, da irgndwas zu groß ist
                }
            }
        }

        void IPaintTo3D.FillRect2D(PointF p1, PointF p2)
        {
            using (new Transform(graphics, new Matrix())) // Identität 
            {
                try
                {
                    Brush brush = MakeSolidBrush();
                    RectangleF rect = new RectangleF(Math.Min(p1.X, p2.X), Math.Min(p1.Y, p2.Y), Math.Abs(p2.X - p1.X), Math.Abs(p2.Y - p1.Y));
                    graphics.FillRectangle(brush, rect);
                    brush.Dispose();
                }
                catch (System.OverflowException)
                {
                    // kann nicht dargestellt werden, da irgndwas zu groß ist
                }

            }
        }

        void IPaintTo3D.Point2D(int x, int y)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        void IPaintTo3D.DisplayIcon(GeoPoint p, System.Drawing.Bitmap icon)
        {
            PointF pf = projection.ProjectF(p);
            System.Drawing.Point point = new System.Drawing.Point((int)(pf.X - icon.Width / 2.0 + 0.5), (int)(pf.Y - icon.Height / 2.0 + 0.5));
            try
            {
                graphics.DrawImageUnscaled(icon, point);
            }
            catch (System.OverflowException) { }
        }
        void IPaintTo3D.DisplayBitmap(GeoPoint p, System.Drawing.Bitmap bitmap)
        {
        }
        void IPaintTo3D.PrepareBitmap(System.Drawing.Bitmap bitmap, int xoffset, int yoffset)
        {
        }
        void IPaintTo3D.PrepareBitmap(System.Drawing.Bitmap bitmap)
        {
        }
        void IPaintTo3D.RectangularBitmap(System.Drawing.Bitmap bitmap, GeoPoint location, GeoVector directionWidth, GeoVector directionHeight)
        {
            PointF[] destPoints = new PointF[3];
            destPoints[0] = projection.ProjectF(location + directionHeight);
            destPoints[1] = projection.ProjectF(location + directionHeight + directionWidth);
            destPoints[2] = projection.ProjectF(location);
            graphics.DrawImage(bitmap, destPoints);
        }

        void IPaintTo3D.SetProjection(Projection projection, BoundingCube boundingCube)
        {
            this.projection = projection;
        }

        void IPaintTo3D.Clear(Color background)
        {
            graphics.Clear(background);
        }

        void IPaintTo3D.Resize(int width, int height)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        void IPaintTo3D.OpenList(string name)
        {
            displayList = new DisplayList(projection.Width, projection.Height);
            oldGraphics = graphics;
            graphics = displayList.graphics;
        }

        IPaintTo3DList IPaintTo3D.CloseList()
        {
            displayList.Close();
            graphics = oldGraphics;
            IPaintTo3DList res = displayList;
            displayList = null;
            return res;
        }

        void IPaintTo3D.OpenPath()
        {
            graphicsPath = new GraphicsPath();
            graphicsPath.FillMode = FillMode.Alternate;
            // ab jetzt auf graphicsPath zeichnen
        }
        void IPaintTo3D.CloseFigure()
        {
            graphicsPath.CloseFigure();
        }
        void IPaintTo3D.ClosePath(System.Drawing.Color color)
        {
            Brush br = null;
            Pen pn = null;
            if (color.ToArgb() == avoidColor.ToArgb())
            {
                br = MakeSolidBrush(Color.FromArgb(255 - avoidColor.R, 255 - avoidColor.G, 255 - avoidColor.B));
            }
            else
            {
                br = MakeSolidBrush(color);
            }
            if (selectMode && selectOnlyOutline)
            {
                pn = MakePen();
                graphics.DrawPath(pn, graphicsPath);
            }
            else
            {
                graphics.FillPath(br, graphicsPath);
            }
            graphicsPath = null; // ab jetzt wieder direkt auf GDI zeichnen
            if (br != null) br.Dispose();
            if (pn != null) pn.Dispose();
        }

        private Brush MakeSolidBrush()
        {
            return new SolidBrush(color);
        }
        private Brush MakeSolidBrush(Color color)
        {
            return new SolidBrush(color);
        }
        void IPaintTo3D.Arc(GeoPoint center, GeoVector majorAxis, GeoVector minorAxis, double startParameter, double sweepParameter)
        {
            try
            {
                GeoVector normal = majorAxis ^ minorAxis; // normale der Ebene des Bogens
                // wird der Bogen von vorne oder hinten betrachtet?
                // statt projection.Direction besser die Richtung zum Mittelpunkt (bei perspektivischer Projektion)
                double sc = projection.Direction.Normalized * normal.Normalized;
                if (Math.Abs(sc) < 1e-6)
                {   // eine Linie
                    GeoVector dir = normal ^ projection.Direction; // Richtung der Linie
                    double pmin, pmax;
                    double par = Geometry.LinePar(center, dir, center + Math.Cos(startParameter) * majorAxis + Math.Sin(startParameter) * minorAxis);
                    pmin = pmax = par;
                    par = Geometry.LinePar(center, dir, center + Math.Cos(startParameter + sweepParameter) * majorAxis + Math.Sin(startParameter + sweepParameter) * minorAxis);
                    if (par < pmin) pmin = par;
                    if (par > pmax) pmax = par;
                    // fehlt noch: jetzt noch die Achsenpunkt abprüfen...
                    (this as IPaintTo3D).Polyline(new GeoPoint[] { center + pmin * dir, center + pmax * dir });
                }
                else
                {
                    GeoPoint2D center2d = projection.Project(center);
                    GeoVector2D maj2D = projection.Project(center + majorAxis) - center2d;
                    GeoVector2D min2D = projection.Project(center + minorAxis) - center2d;
                    if (maj2D.IsNullVector() || min2D.IsNullVector())
                    {   // eigentlich auch eine Linie
                        return;
                    }
                    GeoPoint2D sp = center2d + Math.Cos(startParameter) * maj2D + Math.Sin(startParameter) * min2D;
                    GeoPoint2D ep = center2d + Math.Cos(startParameter + sweepParameter) * maj2D + Math.Sin(startParameter + sweepParameter) * min2D;
                    bool counterclock = sweepParameter > 0.0;
                    //if (normal.z > 0.0) counterclock = !counterclock;
                    EllipseArc2D ea2d = EllipseArc2D.Create(center2d, maj2D, min2D, sp, ep, counterclock);
                    ea2d.MakePositivOriented();
                    GeoVector2D prmaj2D, prmin2D;
                    // Geometry.PrincipalAxis(maj2D, min2D, out prmaj2D, out prmin2D);
                    prmaj2D = ea2d.MajorAxis;
                    prmin2D = ea2d.MinorAxis;
                    Angle rot = prmaj2D.Angle;
                    //ModOp2D toHorizontal = ModOp2D.Rotate(center2d, -rot.Radian);
                    ModOp2D fromHorizontal = ModOp2D.Rotate(center2d, rot.Radian);
                    SweepAngle swapar = new SweepAngle(ea2d.StartPoint - center2d, ea2d.EndPoint - center2d);
                    if (counterclock && swapar < 0) swapar += 360;
                    if (!counterclock && swapar > 0) swapar -= 360;
                    float swPar = (float)(ea2d.axisSweep / Math.PI * 180);
                    float stPar = (float)(ea2d.axisStart / Math.PI * 180);
                    if (sweepParameter >= Math.PI * 2.0 || sweepParameter <= -Math.PI * 2.0)
                    {   // Notlösung wg. ERSACAD und wg. Nürnberger 5.12.13
                        swPar = 360.0f;
                    }
                    try
                    {
                        Matrix r = fromHorizontal.Matrix2D;
                        double maxRad = prmaj2D.Length;
                        double minRad = prmin2D.Length;
                        if ((maxRad + minRad) * Projection.WorldToDeviceFactor < 1)
                        {   // sonst gibt es ein out of memory exception
                            if (graphicsPath != null)
                            {   // kann auch schräge Ellipsen zufügen mit Transformation
                                GraphicsPath tmpPath = new GraphicsPath();
                                tmpPath.AddLine((float)center2d.x, (float)center2d.y, (float)center2d.x, (float)center2d.y);
                                tmpPath.Transform(r);
                                graphicsPath.AddPath(tmpPath, true);
                            }
                            else
                            {
                                Pen drawPen = MakePen();
                                using (drawPen)
                                {
                                    using (new Transform(graphics, r, false))
                                    {   // wenigstens einen Pixel darstellen
                                        graphics.DrawLine(drawPen, (float)(center2d.x - 0.5), (float)center2d.y, (float)(center2d.x + 0.5), (float)center2d.y);
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (graphicsPath != null)
                            {   // kann auch schräge Ellipsen zufügen mit Transformation
                                GraphicsPath tmpPath = new GraphicsPath();
                                tmpPath.AddArc((float)(center2d.x - maxRad), (float)(center2d.y - minRad), (float)(2.0 * maxRad), (float)(2.0 * minRad), stPar, swPar);
                                tmpPath.Transform(r);
                                graphicsPath.AddPath(tmpPath, true);
                            }
                            else
                            {
                                Pen drawPen = MakePen();
                                using (drawPen)
                                {
                                    using (new Transform(graphics, r, false))
                                    {
                                        graphics.DrawArc(drawPen, (float)(center2d.x - maxRad), (float)(center2d.y - minRad), (float)(2.0 * maxRad), (float)(2.0 * minRad), stPar, swPar);
                                    }
                                }
                            }
                        }
                    }
                    catch (ArgumentException)
                    {
                    }
                    catch (ModOpException)
                    {
                    }
                    catch (OutOfMemoryException)
                    {
                    }
                }
            }
            catch
            {   // damit es sicher durchläuft. z.B. eine Ellipse, bei der beide Achsen dieselbe Richtung haben, erzeugt eine ModOp Exception
            }
        }
        //void IPaintTo3D.Arc(GeoPoint center, GeoVector majorAxis, GeoVector minorAxis, double startParameter, double sweepParameter)
        //{   // Das Problem ist, dass GDI nur achsenorientierte Ellipsen zeichnen kann.
        //    // Wir müssen die Drehung hier rausrechnen, denn die zu verwendende Matrix 
        //    // darf nicht skalieren wegen der Strichstärke und der Strichlierung

        //    GeoVector normal = majorAxis ^ minorAxis; // normale der Ebene des Bogens
        //    // wird der Bogen von vorne oder hinten betrachtet?
        //    // statt projection.Direction besser die Richtung zum Mittelpunkt (bei perspektivischer Projektion)
        //    double sc = projection.Direction.Normalized * normal.Normalized;
        //    if (Math.Abs(sc) < 1e-6)
        //    {   // eine Linie
        //        GeoVector dir = normal ^ projection.Direction; // Richtung der Linie
        //        double pmin, pmax;
        //        double par = Geometry.LinePar(center, dir, center + Math.Cos(startParameter) * majorAxis + Math.Sin(startParameter) * minorAxis);
        //        pmin = pmax = par;
        //        par = Geometry.LinePar(center, dir, center + Math.Cos(startParameter + sweepParameter) * majorAxis + Math.Sin(startParameter + sweepParameter) * minorAxis);
        //        if (par < pmin) pmin = par;
        //        if (par > pmax) pmax = par;
        //        // fehlt noch: jetzt noch die Achsenpunkt abprüfen...
        //        (this as IPaintTo3D).Polyline(new GeoPoint[] { center + pmin * dir, center + pmax * dir });
        //    }
        //    else
        //    {
        //        GeoPoint2D center2d = projection.Project(center);
        //        GeoVector2D maj2D = projection.Project(center + majorAxis) - center2d;
        //        GeoVector2D min2D = projection.Project(center + minorAxis) - center2d;
        //        if (maj2D.IsNullVector() || min2D.IsNullVector())
        //        {   // eigentlich auch eine Linie
        //            return;
        //        }
        //        GeoVector2D prmaj2D, prmin2D;
        //        Geometry.PrincipalAxis(maj2D, min2D, out prmaj2D, out prmin2D);
        //        GeoPoint2D sp = center2d + Math.Cos(startParameter) * maj2D + Math.Sin(startParameter) * min2D;
        //        GeoPoint2D ep = center2d + Math.Cos(startParameter + sweepParameter) * maj2D + Math.Sin(startParameter + sweepParameter) * min2D;
        //        Angle rot = prmaj2D.Angle;
        //        ModOp2D toHorizontal = ModOp2D.Rotate(center2d, -rot.Radian);
        //        ModOp2D fromHorizontal = ModOp2D.Rotate(center2d, rot.Radian);
        //        sp = toHorizontal * sp; // Start- und Endpunkt bezüglich der horizontalen Ellipse
        //        ep = toHorizontal * ep;
        //        Angle startAngle = new Angle(sp, center2d);
        //        Angle EndAngle = new Angle(ep, center2d);
        //        float stPar = (float)startAngle.Degree;
        //        SweepAngle swapar = new SweepAngle(sp - center2d, ep - center2d);
        //        float swPar = (float)(swapar.Degree);
        //        if (swapar == 0.0 && sweepParameter > Math.PI) swPar = 360.0f;
        //        // float stPar = (float)(startParameter / Math.PI * 180.0);
        //        // float swPar = (float)(sweepParameter / Math.PI * 180.0);
        //        try
        //        {
        //            //ModOp2D toUnit = ModOp2D.Fit(new GeoPoint2D[] { center2d, center2d + maj2D, center2d + min2D }, new GeoPoint2D[] { GeoPoint2D.Origin, new GeoPoint2D(1.0, 0.0), new GeoPoint2D(0.0, 1.0) }, true);
        //            //Matrix r = toUnit.GetInverse().Matrix2D;
        //            Matrix r = fromHorizontal.Matrix2D;
        //            double maxRad = prmaj2D.Length;
        //            double minRad = prmin2D.Length;
        //            if (graphicsPath != null)
        //            {   // kann auch schräge Ellipsen zufügen mit Transformation
        //                GraphicsPath tmpPath = new GraphicsPath();
        //                tmpPath.AddArc((float)(center2d.x - maxRad), (float)(center2d.y - minRad), (float)(2.0 * maxRad), (float)(2.0 * minRad), stPar, swPar);
        //                tmpPath.Transform(r);
        //                graphicsPath.AddPath(tmpPath, true);
        //            }
        //            else
        //            {
        //                Pen drawPen = MakePen();
        //                using (drawPen)
        //                {
        //                    using (new Transform(graphics, r, false))
        //                    {
        //                        graphics.DrawArc(drawPen, (float)(center2d.x - maxRad), (float)(center2d.y - minRad), (float)(2.0 * maxRad), (float)(2.0 * minRad), stPar, swPar);
        //                    }
        //                }
        //            }
        //        }
        //        catch (ArgumentException)
        //        {
        //        }
        //        catch (ModOpException)
        //        {
        //        }
        //    }

        //    //try
        //    //{
        //    //    GeoPoint2D center2d = projection.Project(center);
        //    //    GeoVector2D maj2D = projection.Project(center + majorAxis) - center2d;
        //    //    GeoVector2D min2D = projection.Project(center + minorAxis) - center2d;
        //    //    double orient1 = GeoVector2D.Orientation(maj2D, min2D);
        //    //    double orient2 = majorAxis.x * minorAxis.y - majorAxis.y * minorAxis.x;
        //    //    GeoPoint2D sp = center2d + Math.Cos(startParameter) * maj2D + Math.Sin(startParameter) * min2D;
        //    //    GeoPoint2D ep = center2d + Math.Cos(startParameter + sweepParameter) * maj2D + Math.Sin(startParameter + sweepParameter) * min2D;
        //    //    bool orient = sweepParameter <= 0;
        //    //    if (Math.Sign(orient1) != Math.Sign(orient2)) orient = !orient;
        //    //    EllipseArc2D ea2d = EllipseArc2D.Create(center2d, maj2D, min2D, sp, ep, orient);
        //    //    GeoVector2D majax, minax;
        //    //    // GeoPoint2D left, right, bottom, top;
        //    //    // Geometry.PrincipalAxis(center2d, maj2D, min2D, out majax, out minax, out left, out right, out bottom, out top, false);
        //    //    majax = ea2d.majorAxis;
        //    //    minax = ea2d.minorAxis;
        //    //    double majorRadius = majax.Length;
        //    //    double minorRadius = minax.Length;
        //    //    double majorsin = majax.y / majorRadius;
        //    //    double majorcos = majax.x / majorRadius;
        //    //    float fStartAng = (float)(ea2d.axisStart / Math.PI * 180);
        //    //    float fSweepAng = (float)(ea2d.axisSweep / Math.PI * 180);
        //    //    if (sweepParameter == Math.PI * 2.0) fSweepAng = 360.0f;
        //    //    Matrix r = new Matrix((float)(majorcos), (float)(majorsin),
        //    //        (float)(-majorsin), (float)(majorcos),
        //    //        (float)((-center2d.x * majorcos + center2d.y * majorsin + center2d.x)),
        //    //        (float)((-center2d.x * majorsin - center2d.y * majorcos + center2d.y)));
        //    //    //double x = majorRadius * Math.Cos(startParameter);
        //    //    //double y = minorRadius * Math.Sin(startParameter);
        //    //    //double spar = Math.Atan2(y, x);
        //    //    //x = majorRadius * Math.Cos(startParameter+sweepParameter);
        //    //    //y = minorRadius * Math.Sin(startParameter+sweepParameter);
        //    //    //double epar = Math.Atan2(y, x);
        //    //    //double sw = epar - spar;
        //    //    //if (sw > 0.0 && sweepParameter < 0.0) sw -= Math.PI * 2.0;
        //    //    //if (sw < 0.0 && sweepParameter > 0.0) sw += Math.PI * 2.0;
        //    //    //float fStartAng = -(float)(spar / Math.PI * 180.0);
        //    //    //float fSweepAng = -(float)(sw / Math.PI * 180.0);
        //    //    if (graphicsPath != null)
        //    //    {   // kann auch schräge Ellipsen zufügen mit Transformation
        //    //        GraphicsPath tmpPath = new GraphicsPath();
        //    //        tmpPath.AddArc((float)(center2d.x - majorRadius), (float)(center2d.y - minorRadius), (float)(2.0 * majorRadius), (float)(2.0 * minorRadius), fStartAng, fSweepAng);
        //    //        tmpPath.Transform(r);
        //    //        graphicsPath.AddPath(tmpPath, true);
        //    //    }
        //    //    else
        //    //    {
        //    //        Pen drawPen = MakePen();
        //    //        using (new Transform(graphics, r, false))
        //    //        {
        //    //            //if (displaySelected)
        //    //            //{
        //    //            //    using (Pen selectPen = drawPen.Clone() as Pen)
        //    //            //    {
        //    //            //        selectPen.Color = this.gDIResources.SelectColor;
        //    //            //        double d = Math.Max(1.0, selectPen.Width * this.Projection.WorldToDeviceFactor / 10);
        //    //            //        for (TransformWobble wb = new TransformWobble(Graphics, d); wb != null; wb = wb.Next())
        //    //            //        {
        //    //            //            Graphics.DrawEllipse(selectPen, (float)(center.x - majorRadius), (float)(center.y - minorRadius), (float)(2.0 * majorRadius), (float)(2.0 * minorRadius));
        //    //            //        }
        //    //            //    }
        //    //            //}
        //    //            graphics.DrawArc(drawPen, (float)(center2d.x - majorRadius), (float)(center2d.y - minorRadius), (float)(2.0 * majorRadius), (float)(2.0 * minorRadius), fStartAng, fSweepAng);
        //    //        }
        //    //    }
        //    //}
        //    //catch (ApplicationException)
        //    //{ // 
        //    //}
        //}
        IPaintTo3DList IPaintTo3D.MakeList(System.Collections.Generic.List<IPaintTo3DList> sublists)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        void IPaintTo3D.FreeUnusedLists()
        {
            throw new Exception("The method or operation is not implemented.");
        }

        void IPaintTo3D.UseZBuffer(bool use)
        {

        }

        void IPaintTo3D.Blending(bool on)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        void IPaintTo3D.FinishPaint()
        {
            throw new Exception("The method or operation is not implemented.");
        }

        void IPaintTo3D.PaintFaces(PaintTo3D.PaintMode paintMode)
        {

        }

        IDisposable IPaintTo3D.FacesBehindEdgesOffset
        {
            get { throw new Exception("The method or operation is not implemented."); }
        }

        bool IPaintTo3D.IsBitmap => false;

        void IPaintTo3D.Dispose()
        {
            throw new Exception("The method or operation is not implemented.");
        }

        void IPaintTo3D.PushState()
        {
            throw new Exception("The method or operation is not implemented.");
        }

        void IPaintTo3D.PopState()
        {
            throw new Exception("The method or operation is not implemented.");
        }

        void IPaintTo3D.PushMultModOp(ModOp insertion)
        {
            projectionStack.Push(projection.Clone());
            projection = projection.PrependModOp(insertion);
        }

        void IPaintTo3D.PopModOp()
        {
            projection = projectionStack.Pop();
        }

        void IPaintTo3D.SetClip(Rectangle clipRectangle)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        #endregion

        #region IDisposable Members
        public virtual void Dispose()
        {
        }

        void IPaintTo3D.PreparePointSymbol(PointSymbol pointSymbol)
        {
            
        }
        #endregion
    }

    /// <summary>
    /// Class to create a bitmap and draw IGeoObjects or Models on the it.
    /// GDI+ is used for drawing, not OpenGL.
    /// </summary>

    public class PaintToBitmap : PaintToGDI
    {
        System.Drawing.Bitmap bitmap;
        /// <summary>
        /// Creates a bitmap with the specified size to draw on it.
        /// </summary>
        /// <param name="width">Width of the bitmap in pixel</param>
        /// <param name="height">Height of the Bitmap in pixel</param>
        public PaintToBitmap(int width, int height)
        {
            bitmap = new System.Drawing.Bitmap(width, height);
            graphics = Graphics.FromImage(bitmap);
            graphics.SetClip(new RectangleF(0.0f, 0.0f, (float)width, (float)height));
            projection = new Projection(Projection.StandardProjection.FromTop); // nur eine Voreinstellung
            projection.GetOpenGLProjection(0, width, 0, height, BoundingCube.UnitBoundingCube); // zur Bestimmung von clientRect in Projection
            Brush wbr = new SolidBrush(Color.White);
            graphics.FillRectangle(wbr, new RectangleF(0.0f, 0.0f, (float)width, (float)height));
            (this as IPaintTo3D).Precision = 0.5; // weniger als ein pixel
        }
        /// <summary>
        /// Paints a GeoObject onto the bitmap
        /// </summary>
        /// <param name="go"></param>
        public void Paint(IGeoObject go)
        {
            go.PaintTo3D(this);
        }
        /// <summary>
        /// Draws a Model onto the bitmap.
        /// </summary>
        /// <param name="m"></param>
        public void Paint(Model m)
        {
            foreach (IGeoObject go in m)
            {
                go.PaintTo3D(this);
            }
        }
        /// <summary>
        /// Gets the Bitmap
        /// </summary>
        public System.Drawing.Bitmap Bitmap
        {
            get
            {
                //graphics.Dispose();
                //graphics = null;
                return bitmap;
            }
        }
        /// <summary>
        /// Disposes bitmap and graphics objects
        /// </summary>
        public override void Dispose()
        {
            bitmap.Dispose();
            if (graphics != null) graphics.Dispose();
            base.Dispose();
        }
    }

}
