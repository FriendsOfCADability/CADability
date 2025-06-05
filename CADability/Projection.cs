using System;
using System.Runtime.Serialization;
using CADability.GeoObject;
using CADability.Shapes;
using System.Collections.Generic;
#if WEBASSEMBLY
using CADability.WebDrawing;
#else
using System.Drawing;
#endif
using MathNet.Numerics.LinearAlgebra.Double;

namespace CADability
{
    /// <summary>
    /// A parallel or perspective projection which is used to convert the 3-dimensional world coordinate system to a 2-dimensional
    /// screen coordinate system or to a normalized 3-dimensional cube for the paint or rendering interface (<see cref="IPaintTo3D"/>).
    /// Contains also additional hints for the paint interface and the drawing plane.
    /// </summary>
    [Serializable]
    public class Projection : ISerializable, IDeserializationCallback
    {
        /// <summary>
        /// Class defining the span or scope in the world coordinate space defined by a axis aligned rectangle 
        /// in a view or on the screen. The area defined by this object is either a rectangular prism of infinite 
        /// length in the direction of the view (in case of the parallel view) or a frustum (in case of a
        /// perspective view). The pickarea is created by the <see cref="Projection.GetPickSpace(Rectangle)"/>, 
        /// <see cref="Projection.GetPickSpace(RectangleF)"/> or <see cref="Projection.GetPickSpace(BoundingRect)"/>.
        /// </summary>
        public class PickArea
        {
            Matrix4 toUnitBox;
            bool isPerspective;
            Plane[] bounds;
            GeoPoint frontCenter;
            GeoVector direction;
            Projection projection;
            internal PickArea(Projection projection, RectangleF viewRect)
            {
                this.projection = projection;
                if (!projection.IsInitialized) projection.Initialize();
                if (projection.clientRect.Width > 0 && projection.clientRect.Height > 0)
                {
                    // bringe das Rechteck in den OpenGL Würfel
                    double left = 2.0 * (double)viewRect.Left / (double)projection.clientRect.Width - 1.0;
                    double right = 2.0 * (double)viewRect.Right / (double)projection.clientRect.Width - 1.0;
                    double top = 2.0 * (double)(projection.clientRect.Height - viewRect.Top) / (double)projection.clientRect.Height - 1.0;
                    double bottom = 2.0 * (double)(projection.clientRect.Height - viewRect.Bottom) / (double)projection.clientRect.Height - 1.0;
                    // das gesuchte Ergebnis ist im Prinzip die OpenGLProjection, jedoch mit einer Verschiebung und Skalierung 
                    // in x und y. Z wird schon richtig auf 0 bis 1 abgebildet
                    // fx*left + dx = 0, fx*right + dx = 1;
                    // fy*bottom + dy = 0, fy*top + dy = 1;
                    try
                    {
                        Matrix mx = DenseMatrix.OfArray(new double[,] { { left, 1 }, { right, 1 } });
                        Vector sx = (Vector)mx.Solve(new DenseVector(new double[] { 1, 0 }));
                        Matrix my = DenseMatrix.OfArray(new double[,] { { top, 1 }, { bottom, 1 } });
                        Vector sy = (Vector)my.Solve(new DenseVector(new double[] { 1, 0 }));
                        // Achtung: z geht von -1 bis +1, UnitBox nur von 0 bis 1, deshalb hir z mit 0.5 Faktor und 0.5 Verschiebung versehen
                        toUnitBox = new Matrix4(new double[,] { { sx[0], 0, 0, sx[1] }, { 0, sy[0], 0, sy[1] }, { 0, 0, 0.5, 0.5 }, { 0, 0, 0, 1 } }) * projection.openGlMatrix;
                        isPerspective = projection.isPerspective;
                        frontCenter = projection.inverseOpenGLMatrix * new GeoPoint((left + right) / 2, (top + bottom) / 2, -1.0);
                        GeoPoint fb = projection.inverseOpenGLMatrix * new GeoPoint((left + right) / 2, (top + bottom) / 2, 1.0);
                        direction = fb - frontCenter;
                        direction.Norm();
                    }
                    catch (ApplicationException) { }
                    // OpenGl Frustum Größe scheint von -1 bis 1 zu gehen in alle 3 Richtungen
                    // wobei z==-1 bei der Kamera liegt, z==1 am entfernten Punkt auf der hinteren Clipebene

                    //GeoPoint dbg = toUnitBox * GeoPoint.Origin;

                    //// DEBUG: 
                    //dbg = projection.openGlMatrix * GeoPoint.Origin;
                    //dbg = projection.openGlMatrix * new GeoPoint(20, 20, 20);

                    //dbg = projection.inverseOpenGLMatrix * new GeoPoint((left + right) / 2.0, (bottom + top) / 2.0, 0.5);
                    //GeoPoint dbg1 = ToUnitBox * dbg;
                    //// die Ebenen sehen noch nicht gut aus...
                    //for (int i = 0; i < Bounds.Length; ++i)
                    //{
                    //    GeoPoint dbg2 = Bounds[i].ToLocal(dbg);
                    //}
                }
            }
            /// <summary>
            /// Returns true, if the area is limited
            /// </summary>
            public bool Limited
            {
                get
                {
                    return isPerspective;
                }
            }
            internal Plane[] Bounds
            {
                get
                {
                    if (bounds == null)
                    {
                        Matrix4 inv = toUnitBox.GetInverse();
                        if (inv.IsValid)
                        {
                            if (isPerspective)
                            {
                                bounds = new Plane[6];
                            }
                            else
                            {
                                bounds = new Plane[4];
                            }
                            // die Ebenen sollen alle nach außen zeigen
                            bounds[0] = new Plane(inv * GeoPoint.Origin, inv * (GeoPoint.Origin + GeoVector.XAxis), inv * (GeoPoint.Origin + GeoVector.ZAxis));
                            bounds[1] = new Plane(inv * GeoPoint.Origin, inv * (GeoPoint.Origin + GeoVector.YAxis), inv * (GeoPoint.Origin - GeoVector.ZAxis));
                            bounds[2] = new Plane(inv * (GeoPoint.Origin + GeoVector.XAxis), inv * (GeoPoint.Origin + GeoVector.XAxis + GeoVector.YAxis), inv * (GeoPoint.Origin + GeoVector.XAxis + GeoVector.ZAxis));
                            bounds[3] = new Plane(inv * (GeoPoint.Origin + GeoVector.YAxis), inv * (GeoPoint.Origin + GeoVector.XAxis + GeoVector.YAxis), inv * (GeoPoint.Origin + GeoVector.YAxis - GeoVector.ZAxis));
                            if (isPerspective)
                            {
                                bounds[4] = new Plane(inv * GeoPoint.Origin, inv * (GeoPoint.Origin + GeoVector.XAxis), inv * GeoPoint.Origin - GeoVector.YAxis);
                                bounds[5] = new Plane(inv * (GeoPoint.Origin + GeoVector.ZAxis), inv * (GeoPoint.Origin + GeoVector.ZAxis + GeoVector.XAxis), inv * (GeoPoint.Origin + GeoVector.ZAxis + GeoVector.YAxis));
                            }
                        }
                    }
                    return bounds;
                }
            }
            public Matrix4 ToUnitBox
            {
                get
                {
                    return toUnitBox;
                }
            }
            /// <summary>
            /// Gets the center of the front rectangle, i.e. the point you are looking from
            /// </summary>
            public GeoPoint FrontCenter
            {
                get { return frontCenter; }
            }
            public GeoVector Direction
            {
                get
                {
                    return direction;
                }
            }
            /// <summary>
            /// Returns the projection associated with this area
            /// </summary>
            public Projection Projection
            {
                get
                {
                    return projection;
                }
            }
#if DEBUG
            public CADability.GeoObject.GeoObjectList Debug
            {
                get
                {   // die UnitBox ist von 0 bis 1 in alle Richtungen, oder?
                    CADability.GeoObject.GeoObjectList res = new CADability.GeoObject.GeoObjectList();
                    Matrix4 fromUnitBox = toUnitBox.GetInverse();
                    if (fromUnitBox.IsValid)
                    {
                        CADability.GeoObject.Face fc1 = CADability.GeoObject.Face.MakeFace(fromUnitBox * new GeoPoint(0, 0, 0), fromUnitBox * new GeoPoint(1, 0, 0), fromUnitBox * new GeoPoint(1, 0, 1), fromUnitBox * new GeoPoint(0, 0, 1));
                        CADability.GeoObject.Face fc2 = CADability.GeoObject.Face.MakeFace(fromUnitBox * new GeoPoint(0, 1, 0), fromUnitBox * new GeoPoint(1, 1, 0), fromUnitBox * new GeoPoint(1, 1, 1), fromUnitBox * new GeoPoint(0, 1, 1));
                        CADability.GeoObject.Face fc3 = CADability.GeoObject.Face.MakeFace(fromUnitBox * new GeoPoint(0, 0, 0), fromUnitBox * new GeoPoint(0, 1, 0), fromUnitBox * new GeoPoint(0, 1, 1), fromUnitBox * new GeoPoint(0, 0, 1));
                        CADability.GeoObject.Face fc4 = CADability.GeoObject.Face.MakeFace(fromUnitBox * new GeoPoint(1, 0, 0), fromUnitBox * new GeoPoint(1, 1, 0), fromUnitBox * new GeoPoint(1, 1, 1), fromUnitBox * new GeoPoint(1, 0, 1));
                        res.Add(fc1);
                        res.Add(fc2);
                        res.Add(fc3);
                        res.Add(fc4);
                    }
                    return res;
                }
            }
#endif
        }
        /* Gleichzeitige Unterstüzung von Parallelprojektion und Zentralprojektion:
         * Die wichtigen Daten sind in perspectiveProjection bzw. unscaledProjection gespeichert.
         * Zoomen und Scrollen wird zusätzlich durch placementFactor/X/Y realisiert
         * Die rohe Parallelprojektion (ohne Zoom und Scroll) ist lediglich durch eine Richtung 
         * und durch die senkrechte (wo ist oben) gegeben
         * Die entsprechende Zentralprojektion ust durch das Zentrum (Beobachtungspunkt) und die Richtung
         * und auch wiederum die senkrechte gegeben.
         * Der Öffnungswinkel und placementFactor haben den gleichen Effekt
         * Ein Problem besteht darin, dass bei der Parallelprojektion alle Daten unabhängig von der Fenstergröße berechnet werden
         * können, bei der Zentralprojektion dagegen nicht
        */
        private bool isPerspective; // ist Zentralprojektion
        private Matrix4 perspectiveProjection;
        private Matrix4 inversePerspectiveProjection;
        private ModOp unscaledProjection;
        private ModOp inverseProjection;
        private double placementFactor;
        private double placementX, placementY;

        private Matrix4 openGlMatrix; // bildet alles auf den Würfel [-1,1][-1,1][0,1] ab
        private Matrix4 inverseOpenGLMatrix;
        private Rectangle clientRect; // das Fenster
        private BoundingCube extent; // Größe des Modells

        private double Matrix00, Matrix01, Matrix02, Matrix03, Matrix10, Matrix11, Matrix12, Matrix13;
        /// <summary>
        /// Some Attributes refer to paper bound dimensions: e.g. the linewidth or textsize 
        /// may be specified in mm on the paper. So we need the posibility to transform linear
        /// dimensions from layout to world. This is doune by the LayoutFactor;
        /// </summary>
        public double LayoutFactor;
        private GeoVector direction; // Richtung der Projektion
        private GeoPoint perspectiveCenter;
        private Plane drawingPlane;
        private double precision;
        private Grid grid;
        private bool showHiddenLines;
        private bool showFaces; // true: Face2D sollen erzeugt werden, false Edge2D sollen erzeugt werden
        private bool showDrawingPlane; // Zeichenebene anzeigen
        /// <summary>
        /// A factor for the display line width
        /// </summary>
        public double LineWidthFactor; // lineWidth*LineWidthFactor ist Breite in Pixel
        /// <summary>
        /// Flag indicating the use of line width
        /// </summary>
        public bool UseLineWidth; // false: alles dünn, steuert in OpenGL "GL_LINE_SMOOTH"
        /// <summary>
        /// Returns true if this projection is a perspective projection in contrast to a parallel projection
        /// </summary>
        public bool IsPerspective
        {
            get
            {
                return isPerspective;
            }
        }
        /// <summary>
        /// Delegate definition for the <see cref="ProjectionChangedEvent"/>
        /// </summary>
        /// <param name="sender">Object issuing the event</param>
        /// <param name="args">Empy event arguments</param>
        public delegate void ProjectionChangedDelegate(Projection sender, EventArgs args);
        /// <summary>
        /// Event beeing issued when the projection changes
        /// </summary>
        public event ProjectionChangedDelegate ProjectionChangedEvent;
        /// <summary>
        /// Creates a default projection, a parallel projection from top
        /// </summary>
        private Projection()
        {
            placementFactor = 1.0;
            placementX = placementY = 0.0;
            showHiddenLines = false;
            showFaces = true;
            LayoutFactor = 1.0;
            grid = new Grid();
            LineWidthFactor = 10.0;
            UseLineWidth = Settings.GlobalSettings.GetBoolValue("View.UseLineWidth", true);
        }
        /// <summary>
        /// Creates a clone of the provided projection
        /// </summary>
        /// <param name="copyFrom">Projection being cloned</param>
        public Projection(Projection copyFrom)
        {
            placementFactor = copyFrom.placementFactor;
            placementX = copyFrom.placementX;
            placementY = copyFrom.placementY;
            showHiddenLines = copyFrom.showHiddenLines;
            LayoutFactor = copyFrom.LayoutFactor;
            unscaledProjection = copyFrom.unscaledProjection;
            inverseProjection = copyFrom.inverseProjection;
            direction = copyFrom.direction;
            drawingPlane = copyFrom.drawingPlane;
            grid = copyFrom.grid;
            LineWidthFactor = copyFrom.LineWidthFactor;
            UseLineWidth = copyFrom.UseLineWidth;
            clientRect = copyFrom.clientRect;
            showFaces = copyFrom.showFaces;

            Matrix00 = copyFrom.Matrix00;
            Matrix01 = copyFrom.Matrix01;
            Matrix02 = copyFrom.Matrix02;
            Matrix03 = copyFrom.Matrix03;
            Matrix10 = copyFrom.Matrix10;
            Matrix11 = copyFrom.Matrix11;
            Matrix12 = copyFrom.Matrix12;
            Matrix13 = copyFrom.Matrix13;

            SetCoefficients();
        }
        /// <summary>
        /// Returns a clone of this projection
        /// </summary>
        /// <returns>The clone</returns>
        public Projection Clone()
        {
            return new Projection(this);
        }
        public static Projection FromTop { get { return new Projection(StandardProjection.FromTop); } }
        /// <summary>
        /// Returns a PickArea, <paramref name="viewRect"/> is in window coordinates
        /// </summary>
        /// <param name="viewRect">The defining rectangle</param>
        /// <returns>The area inside the rectangle</returns>
        public PickArea GetPickSpace(Rectangle viewRect)
        {
            return new PickArea(this, viewRect);
        }
        /// <summary>
        /// Returns a PickArea, <paramref name="viewRect"/> is in window coordinates
        /// </summary>
        /// <param name="viewRect">The defining rectangle</param>
        /// <returns>The area inside the rectangle</returns>
        public PickArea GetPickSpace(RectangleF viewRect)
        {
            return new PickArea(this, viewRect);
        }
        /// <summary>
        /// Returns a PickArea, <paramref name="rectWorld2D"/> is in 2-d world coordinates
        /// </summary>
        /// <param name="rectWorld2D">The defining rectangle</param>
        /// <returns>The area inside the rectangle</returns>
        public PickArea GetPickSpace(BoundingRect rectWorld2D)
        {
            return new PickArea(this, World2DToWindow(rectWorld2D));
        }
        internal void SetExtent(BoundingCube boundingCube)
        {
            extent = boundingCube;
        }

        private void SetCoefficients()
        {
            if (isPerspective)
            {
                Matrix offset = ModOp.Translate(2 * (placementX - clientRect.Left / 2.0) / (clientRect.Width), -2 * (placementY - clientRect.Top / 2.0) / (clientRect.Height), 0).ToMatrix();
                Matrix scale = DenseMatrix.OfArray(new double[,] { { placementFactor, 0, 0, 0 }, { 0, placementFactor, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } });
                openGlMatrix = new Matrix4(offset * scale * perspectiveProjection.Matrix);
                inverseOpenGLMatrix = openGlMatrix.GetInverse();
            }
            else
            {
                double[,] p = unscaledProjection.Matrix;
                // die Platzierung hat folgende Form:
                // f  0 0 x
                // 0 -f 0 y
                // 0  0 0 0
                Matrix00 = placementFactor * p[0, 0];
                Matrix01 = placementFactor * p[0, 1];
                Matrix02 = placementFactor * p[0, 2];
                Matrix03 = placementFactor * p[0, 3] + placementX;
                Matrix10 = -placementFactor * p[1, 0];
                Matrix11 = -placementFactor * p[1, 1];
                Matrix12 = -placementFactor * p[1, 2];
                Matrix13 = -placementFactor * p[1, 3] + placementY;
                inverseProjection = unscaledProjection.GetInverse();
                CalcOpenGlMatrix();
            }
            projectionPlane = null; // ist zu stark hier, aber lohnt eine Unterscheidung nach nur Placement oder nicht?
            if (ProjectionChangedEvent != null) ProjectionChangedEvent(this, EventArgs.Empty);
        }
        /// <summary>
        /// Creates a new parallel projection with the provided direction and the direction of the vector that should become the vertical direction.
        /// </summary>
        /// <param name="Direction">View direction</param>
        /// <param name="TopDirection">Vertical direction</param>
        public Projection(GeoVector Direction, GeoVector TopDirection)
            : this()
        {
            GeoVector xdir = Direction ^ TopDirection;
            GeoVector ydir = xdir ^ Direction;
            try
            {
                CoordSys Dst = new CoordSys(new GeoPoint(0.0, 0.0, 0.0), xdir, ydir);
                unscaledProjection = ModOp.Transform(Dst, CoordSys.StandardCoordSys);
                direction = Direction;
                drawingPlane = new Plane(new GeoPoint(0.0, 0.0, 0.0), xdir, ydir);
                SetCoefficients();
            }
            catch (CoordSysException) { } // ungültige Richtungen
        }
        /// <summary>
        /// Creates a new parallel projection to the provided coordinate system
        /// </summary>
        /// <param name="ProjectTo">The target system</param>
        public Projection(CoordSys ProjectTo)
            : this()
        {
            unscaledProjection = ModOp.Transform(ProjectTo, CoordSys.StandardCoordSys);
            direction = -ProjectTo.Normal;
            SetCoefficients();
        }
        /// <summary>
        /// Sets the view direction and the vertical direction of this projection.
        /// </summary>
        /// <param name="Direction">View direction</param>
        /// <param name="TopDirection">Vertical direction</param>
        /// <param name="extent">Extend of the model in world coordinates</param>
        public void SetDirection(GeoVector Direction, GeoVector TopDirection, BoundingCube extent)
        {
            this.extent = extent;
            GeoVector xdir = Direction ^ TopDirection;
            GeoVector ydir = xdir ^ Direction;
            try
            {
                CoordSys Dst = new CoordSys(new GeoPoint(0.0, 0.0, 0.0), xdir, ydir);
                unscaledProjection = ModOp.Transform(Dst, CoordSys.StandardCoordSys);
                direction = Direction;
                // warum hier die drawingPLane setzen? Früher wurde das zwangsweise gemacht,
                // jetzt nur noch wenn die drawingPlane sinst verschwinden würde...
                if (CADability.Precision.IsPerpendicular(drawingPlane.Normal, Direction, false))
                {
                    drawingPlane = new Plane(drawingPlane.Location, xdir, ydir);
                }
                isPerspective = false;
                SetCoefficients();
            }
            catch (CoordSysException) { } // ungültige Richtungen
        }
        public void SetDirectionAnimated(GeoVector viewDirection, GeoVector TopDirection, Model model, bool zoomTotal, ICanvas ctrl, GeoPoint fixPoint)
        {
            if (!fixPoint.IsValid) zoomTotal = true;
            Rectangle clientRect = ctrl.ClientRectangle;
            float cx = (clientRect.Right + clientRect.Left) / 2.0f;
            float cy = (clientRect.Bottom + clientRect.Top) / 2.0f;

            GeoVector actTopDirection = WindowToWorld(new PointF(cx, clientRect.Top)) - WindowToWorld(new PointF(cx, cy));

            ctrl.OnPaintDone += CondorCtrl_OnPaintDone;
            animStartTime = System.Environment.TickCount;
            animStartDirection = Direction;
            animEndDirection = viewDirection;
            animEndRect = animStartRect = BoundingRectWorld2d(clientRect.Left, clientRect.Right, clientRect.Bottom, clientRect.Top);

            animStartTopDirection = actTopDirection.Normalized;

            animEndTopDirection = TopDirection;

            // hier muss man schon mal die Endprojektion berechnen, um den richtigen Extent zu bekommen
            Projection pr = this.Clone();
            if (CADability.Precision.IsNullVector(animEndDirection ^ new GeoVector(0.0, 0.0, 1.0)))
            {
                pr.SetDirection(animEndDirection, new GeoVector(0.0, 1.0, 0.0), extent);
            }
            else
            {
                GeoVector2D updown = pr.Project2D(new GeoPoint(0, 0, 1)) - pr.Project2D(GeoPoint.Origin);
                if (updown.y <= 0)
                {
                    pr.SetDirection(animEndDirection, new GeoVector(0.0, 0.0, 1.0), extent);
                }
                else
                {
                    pr.SetDirection(animEndDirection, new GeoVector(0.0, 0.0, -1.0), extent);
                }
            }
            if (zoomTotal)
            {
                animEndRect = model.GetExtentForZoomTotal(pr) * 1.1;
            }
            else
            {   // we would need the fixpoint here to position the animEndRect
                GeoPoint2D cnt2dstart = PointWorld2D(ProjectF(fixPoint));
                GeoPoint2D cnt2dend = PointWorld2D(pr.ProjectF(fixPoint));
                GeoVector2D offset = cnt2dstart - cnt2dend;
                animEndRect.Move(-offset);
            }
            ctrl.Invalidate(); // damit wird (ohne Veränderung) neu gezeichnet und onPaintDone aufgerufen und der Animationszyklus gestartet
        }
        private int animStartTime;
        private GeoVector animStartDirection, animEndDirection;
        private BoundingRect animStartRect, animEndRect;
        private GeoVector animEndTopDirection, animStartTopDirection;
        private void CondorCtrl_OnPaintDone(ICanvas ctrl)
        {
            int dt = System.Environment.TickCount - animStartTime;
            // dt maximal 500 ms
            double f = Math.Min(1.0, dt / 500.0);
            GeoVector middleDirection = 0.5 * (animEndDirection ^ animStartDirection);
            if (middleDirection.IsNullVector())
            {
                middleDirection = 0.5 * (GeoVector.ZAxis ^ animStartDirection);
                if (middleDirection.IsNullVector())
                    middleDirection = 0.5 * (GeoVector.YAxis ^ animStartDirection);
            }
            GeoVector viewDirection = f * animEndDirection + (1 - f) * animStartDirection + (0.5 - Math.Abs(f - 0.5)) * middleDirection;
            GeoVector topDir = (f * animEndTopDirection + (1 - f) * animStartTopDirection).Normalized; ;
            SetDirection(viewDirection, topDir, extent);
            BoundingRect zoomTo;
            if (animStartRect != animEndRect)
            {
                BoundingRect middleRect = BoundingRect.Unite(animStartRect, animEndRect);
                if (f < 0.5)
                    zoomTo = BoundingRect.Interpolate(animStartRect, middleRect, (0.5 - f) * 2.0);
                else
                    zoomTo = BoundingRect.Interpolate(animEndRect, middleRect, (f - 0.5) * 2.0);
                //System.Diagnostics.Trace.WriteLine("ZoomTo: " + zoomTo.Left.ToString() + ", " + zoomTo.Right.ToString() + ", " + zoomTo.Bottom.ToString() + ", " + zoomTo.Top.ToString());
            }
            else
            {
                zoomTo = new BoundingRect(animEndRect);
            }
            SetPlacement(ctrl.ClientRectangle, zoomTo);
            ctrl.Invalidate(); // damit wird wieder Paint und somit onPaintDone aufgerufen
            if (f >= 1.0) ctrl.OnPaintDone -= CondorCtrl_OnPaintDone; // damit hat die Schleife ein Ende
        }

        /// <summary>
        /// The standard parallel projections
        /// </summary>
        public enum StandardProjection
        {
            /// <summary>
            /// Projection from top (0, 0, 1)
            /// </summary>
            FromTop,
            /// <summary>
            /// Projection from bottom (0, 0, -1)
            /// </summary>
            FromBottom,
            /// <summary>
            /// Projection from right (1, 0, 0)
            /// </summary>
            FromRight,
            /// <summary>
            /// Projection from left (-1, 0, 0)
            /// </summary>
            FromLeft,
            /// <summary>
            /// Projection from back (0, 1, 0)
            /// </summary>
            FromBack,
            /// <summary>
            /// Projection from front (0, -1, 0)
            /// </summary>
            FromFront,
            /// <summary>
            /// Projection with view direction (-1, -1, -1)
            /// </summary>
            Isometric
        }
        /// <summary>
        /// Creates a new standard projection according to <see cref="StandardProjection"/>
        /// </summary>
        /// <param name="Standard">Which direction to use</param>
        public Projection(StandardProjection Standard)
            : this()
        {
            unscaledProjection = ModOp.Identity;
            GeoVector xdir = new GeoVector(0.0, 0.0, 0.0);
            GeoVector ydir = new GeoVector(0.0, 0.0, 0.0);
            switch (Standard)
            {
                case StandardProjection.FromTop:
                    xdir = new GeoVector(1, 0, 0);
                    ydir = new GeoVector(0, 1, 0);
                    break;
                case StandardProjection.FromBottom:
                    xdir = new GeoVector(1, 0, 0);
                    ydir = new GeoVector(0, -1, 0);
                    break;
                case StandardProjection.FromRight:
                    xdir = new GeoVector(0, 1, 0);
                    ydir = new GeoVector(0, 0, 1);
                    break;
                case StandardProjection.FromLeft:
                    xdir = new GeoVector(0, -1, 0);
                    ydir = new GeoVector(0, 0, 1);
                    break;
                case StandardProjection.FromBack:
                    xdir = new GeoVector(-1, 0, 0);
                    ydir = new GeoVector(0, 0, 1);
                    break;
                case StandardProjection.FromFront:
                    xdir = new GeoVector(1, 0, 0);
                    ydir = new GeoVector(0, 0, 1);
                    break;
                case StandardProjection.Isometric:
                    xdir = new GeoVector(-1, 1, 0);
                    ydir = new GeoVector(-1, -1, 1);
                    xdir.Norm();
                    ydir.Norm();
                    break;
            }
            CoordSys Dst = new CoordSys(new GeoPoint(0.0, 0.0, 0.0), xdir, ydir);
            unscaledProjection = ModOp.Transform(Dst, CoordSys.StandardCoordSys);
            direction = xdir ^ ydir;
            drawingPlane = new Plane(new GeoPoint(0.0, 0.0, 0.0), xdir, ydir);
            SetCoefficients();
        }
        internal bool ShowHiddenLines
        {
            get { return showHiddenLines; }
            set { showHiddenLines = value; }
        }
        public bool ShowFaces
        {
            get { return showFaces; }
            set { showFaces = value; }
        }
        /// <summary>
        /// Display hint, whether to show the drawing plane
        /// </summary>
        public bool ShowDrawingPlane
        {
            get { return showDrawingPlane; }
            set { showDrawingPlane = value; }
        }
        public double Precision
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

        internal BoundingRect GetPlacement(Rectangle Destination)
        {
            return new BoundingRect(DrawingPlanePoint(new System.Drawing.Point(Destination.Left, Destination.Bottom)).To2D(), DrawingPlanePoint(new System.Drawing.Point(Destination.Right, Destination.Top)).To2D());
        }

        /// <summary>
        /// Stellt die Platzierung im Zweidimensionalen ein: Das Quellrechteck 
        /// soll in das Zielrechteck passen.
        /// </summary>
        /// <param name="Destination">das Zielrechteck, gewöhnlich ClientRect des Controls</param>
        /// <param name="Source">Das Quellrechteck in 2-dimensionalen Weltkoordinaten</param>
        public void SetPlacement(Rectangle Destination, BoundingRect Source)
        {
            SetPlacement(new RectangleF(Destination.X, Destination.Y, Destination.Width, Destination.Height), Source);
        }

        public void SetPlacement(RectangleF Destination, BoundingRect Source)
        {
            // Höhe und Breite==0 soll einen Fehler liefern!
            if (isPerspective)
            {   // Source ist im 2d system der projektionsebene
                // Destination ist i.A. das ClientRect
                GeoPoint2D ll = World2DToWindow(new GeoPoint2D(Source.Left, Source.Bottom));
                GeoPoint2D ur = World2DToWindow(new GeoPoint2D(Source.Right, Source.Top));
                GeoPoint center = ProjectionPlane.ToGlobal(Source.GetCenter());
                double width = ur.x - ll.x;
                double height = ll.y - ur.y;
                double f = Math.Min(Destination.Width / width, Destination.Height / height);
                placementFactor *= f;
                SetCoefficients();
                GeoPoint2D c2d = WorldToWindow(center);
                placementX += (Destination.Left + Destination.Right) / 2.0 - c2d.x;
                placementY += (Destination.Bottom + Destination.Top) / 2.0 - c2d.y;
                SetCoefficients();
                return;
            }
            else
            {
                if (Source.Height == 0.0) placementFactor = Destination.Width / Source.Width;
                else if (Source.Width == 0.0) placementFactor = Destination.Height / Source.Height;
                else
                {
                    placementFactor = Math.Min(Destination.Width / Source.Width, Destination.Height / Source.Height);
                }
                if (placementFactor == 0.0) placementFactor = 1.0;
                // wie ist das mit der Y-Richtung in Destination?
                placementX = (Destination.Right + Destination.Left) / 2.0 - (Source.Right + Source.Left) / 2.0 * placementFactor;
                placementY = (Destination.Top + Destination.Bottom) / 2.0 + (Source.Top + Source.Bottom) / 2.0 * placementFactor;
                SetCoefficients();
            }
        }

        public void SetPlacement(double factor, double dx, double dy)
        {
            placementFactor = factor;
            placementX = dx;
            placementY = dy;
            SetCoefficients();
        }
        /// <summary>
        /// Liefert die Werte für die Platzierung. Achtung: die Y-Werte müssen mit dem negativen
        /// Faktor multipliziert werden, denn die Platzierung dreht die y-achse um!
        /// </summary>
        /// <param name="Factor">Skalierungsfaktor, Achtung: für y negativ!</param>
        /// <param name="dx">Verschiebung in X</param>
        /// <param name="dy">Verschiebung in X</param>
        public void GetPlacement(out double Factor, out double dx, out double dy)
        {
            Factor = placementFactor;
            dx = placementX;
            dy = placementY;
        }
        public double PlacementFactor
        {
            get
            {
                return placementFactor;
            }
        }
        public void MovePlacement(double dx, double dy)
        {
            placementX += dx;
            placementY += dy;
            SetCoefficients();
        }
        public Projection PrependModOp(ModOp ToPrepend)
        {   // dieses wird verwendet während DragAndDrop für die Darstellung des gezogenen BlockRefs
            // Die implementierung berücksichtigt nur die 2D Aspekte, ist somit bestimmt nicht logisch,
            // aber es geht so. Probleme machten Objekte, die nicht in der XY Ebene liegen beim DragAndDrop
            Projection res = new Projection();
            res.clientRect = clientRect;
            res.placementFactor = placementFactor * ToPrepend.Factor;
            res.placementX = placementX + res.placementFactor * ToPrepend.Matrix[0, 3];
            res.placementY = placementY - res.placementFactor * ToPrepend.Matrix[1, 3];
            ModOp m = ModOp.Translate(-ToPrepend.Translation) * ToPrepend;
            res.unscaledProjection = unscaledProjection * m;// *ToPrepend; // Testweise *ToPrepend angefügt
            res.SetCoefficients();
            return res;
        }
        public void SetUnscaledProjection(ModOp unscaledProjection)
        {
            this.unscaledProjection = unscaledProjection;
            isPerspective = false;
            SetCoefficients();
        }
        /// <summary>
        /// Berechnet die Projektion des gegebenen Punktes ins Zweidimensionale ohne 
        /// Berücksichtigung der Skalierung und Platzierung im Zweidimensionalen, also
        /// in das zweidimensionale Weltkoordinatensystem und nicht in das 
        /// Papierkoordinatensystem.
        /// </summary>
        /// <param name="p">der zu projizierende Punkt</param>
        /// <returns>der in 2-dim projizierte Punkt</returns>
        public GeoPoint2D ProjectUnscaled(GeoPoint p)
        {
            if (isPerspective)
            {   // hier ist ja die Frage, wo is das 2d Weltsystem
                // jetzt so definiert: es ist die projectionPlane und die ist im OPenGL Würfel auf z==0.5
                return WorldToProjectionPlane(p);
                // war vorher so: return new GeoPoint2D(perspectiveProjection * p);
            }
            else
            {
                return unscaledProjection.Project(p);
            }
        }
        public GeoVector2D ProjectUnscaled(GeoVector v)
        {
            return unscaledProjection.Project(v);
        }
        public GeoPoint UnProjectUnscaled(GeoPoint2D p)
        {
            // return inverseProjection * new GeoPoint(p);
            return ProjectionPlane.ToGlobal(p);
        }
        public GeoVector UnProjectUnscaled(GeoVector2D v)
        {
            // return inverseProjection * new GeoPoint(p);
            return ProjectionPlane.ToGlobal(v);
        }
        /// <summary>
        /// Returns a <see cref="BoundingRect"/> in the 2d world coordinate system according to the provided view positions.
        /// The view coordinate system is the windows forms system.
        /// The 2d world coordinate system is for the parallel projection a plane perpendicular to the projection direction
        /// scaled the same way as the model. For the perspective projection it is the back plane of the displayed frustum of pyramid.
        /// </summary>
        /// <param name="left">left position in the view</param>
        /// <param name="right">rechter  Rand</param>
        /// <param name="bottom">unterer Rand</param>
        /// <param name="top">oberer Rand</param>
        /// <returns></returns>
        public BoundingRect BoundingRectWorld2d(int left, int right, int bottom, int top)
        {
            GeoPoint2D ll = PointWorld2D(new System.Drawing.Point(left, bottom));
            GeoPoint2D ur = PointWorld2D(new System.Drawing.Point(right, top));
            return new BoundingRect(ll.x, ll.y, ur.x, ur.y);
            // für Parallelprojektion gilt einfacher:
            //return new BoundingRect(
            //    (left - placementX) / placementFactor,
            //    -(bottom - placementY) / placementFactor,
            //    (right - placementX) / placementFactor,
            //    -(top - placementY) / placementFactor);
        }
        public BoundingRect GetExtent(CADability.GeoObject.IGeoObject go)
        {
            return go.GetExtent(this, CADability.GeoObject.ExtentPrecision.Raw);
        }
        public Rectangle DeviceRect(BoundingRect r)
        {
            Rectangle res = new Rectangle((int)Math.Round(r.Left * placementFactor + placementX),
                (int)Math.Round(-r.Top * placementFactor + placementY),
                (int)Math.Round(r.Width * placementFactor),
                (int)Math.Round(r.Height * placementFactor));
            res.Inflate(2, 2);
            return res;
        }
        public GeoPoint2D PointWorld2D(System.Drawing.Point p)
        {
            if (!openGlMatrix.IsValid) Initialize();
            GeoPoint p0 = inverseOpenGLMatrix * new GeoPoint(2.0 * (double)(p.X - clientRect.Left) / (double)clientRect.Width - 1, 2.0 * (double)(clientRect.Bottom - p.Y) / (double)clientRect.Height - 1, 0.0);
            GeoPoint p1 = inverseOpenGLMatrix * new GeoPoint(2.0 * (double)(p.X - clientRect.Left) / (double)clientRect.Width - 1, 2.0 * (double)(clientRect.Bottom - p.Y) / (double)clientRect.Height - 1, 1.0);
            return ProjectionPlane.Project(ProjectionPlane.Intersect(p1, p0 - p1));
            // für Parallelprojektion gilt einfacher:
            // return new GeoPoint2D((p.X - placementX) / placementFactor, -(p.Y - placementY) / placementFactor);
        }
        public GeoPoint2D PointWorld2D(PointF pf)
        {
            System.Drawing.Point p = new System.Drawing.Point((int)(pf.X), (int)(pf.Y));
            return PointWorld2D(p);
        }
        public double DeviceToWorldFactor { get { return 1.0 / placementFactor; } }
        public double WorldToDeviceFactor { get { return placementFactor; } }
        public ModOp UnscaledProjection
        {
            get
            {
                return unscaledProjection;
            }
        }
        public ModOp InverseProjection
        {
            get
            {
                return inverseProjection;
            }
        }
        public GeoVector Direction
        {
            get
            {
                // return direction;
                // war vorher so;
                if (IsPerspective)
                {
                    return direction;
                }
                else
                {
                    GeoPoint p0 = new GeoPoint(0.0, 0.0, -1.0);
                    GeoPoint p1 = inverseProjection * p0;
                    return new GeoVector(p1.x, p1.y, p1.z);
                }
                //GeoPoint p0 = inverseOpenGLMatrix * new GeoPoint(0.0, 0.0, -1.0);
                //GeoPoint p1 = inverseOpenGLMatrix * new GeoPoint(0.0, 0.0, 0.0);
                //return p1 - p0;
                // wenn es so gebraucht wird, dann muss man was ändern,
                // da die ViewProperty es unverändert braucht
            }
            set
            {
            }
        }
        private Plane? projectionPlane;
        public Plane ProjectionPlane
        {
            get
            {
                if (!projectionPlane.HasValue)
                {
                    if (isPerspective)
                    {
                        // die hintere Ebene des Pyramidenstumpfs. Problem ist der Nullpunkt
                        // jetzt mal so gelöst: Blickrichtung durch den Ursprung verlängert und geschnitten mit der Ebene
                        // Geht das immer?
                        // Sieht so aus, als ob die z==0 Ebene von OpenGL die vordere und z==1 Ebene die hintere ist
                        // liefert jetzt die Mittelebene. War vorher die hintere Ebene. Ist das von Bedeutung?
                        GeoPoint p0 = inversePerspectiveProjection * new GeoPoint(0, 0, 0.5);
                        GeoPoint p1 = inversePerspectiveProjection * new GeoPoint(1, 0, 0.5);
                        GeoPoint p2 = inversePerspectiveProjection * new GeoPoint(0, 1, 0.5);
                        Plane pln = new Plane(p0, p1, p2);
                        GeoPoint p3 = pln.Intersect(GeoPoint.Origin, direction);
                        pln.Location = p3;
                        projectionPlane = pln;
                    }
                    else
                    {
                        projectionPlane = new Plane(new GeoPoint(0.0, 0.0, 0.0), inverseProjection * new GeoVector(1.0, 0.0, 0.0), inverseProjection * new GeoVector(0.0, 1.0, 0.0));
                    }
                }
                return projectionPlane.Value;
            }
        }
        public Matrix4 PerspectiveProjection
        {
            get
            {
                return perspectiveProjection;
            }
        }
        public Grid Grid
        {
            get { return grid; } // da es eine Klasse ist, genügt das get
        }
        /// <summary>
        /// Berechnet die Projektion des gegebenen Punktes ins Zweidimensionale mit 
        /// Berücksichtigung der Skalierung und Platzierung im Zweidimensionalen, also
        /// in das zweidimensionale Papierkoordinatensystem und nicht in das 
        /// Weltkoordinatensystem.
        /// </summary>
        /// <param name="p">der zu projizierende Punkt</param>
        /// <returns>der in 2-dim projizierte Punkt</returns>
        public PointF ProjectF(GeoPoint p)
        {
            // return new PointF((float)(Matrix00 * p.x + Matrix01 * p.y + Matrix02 * p.z + Matrix03), (float)(Matrix10 * p.x + Matrix11 * p.y + Matrix12 * p.z + Matrix13));
            GeoPoint pogl = openGlMatrix * p;
            return new PointF((float)((pogl.x + 1) / 2 * clientRect.Width + clientRect.Left), (float)(clientRect.Bottom - (pogl.y + 1) / 2 * clientRect.Height));
        }
        public GeoPoint2D Project(GeoPoint p)
        {
            // return new PointF((float)(Matrix00 * p.x + Matrix01 * p.y + Matrix02 * p.z + Matrix03), (float)(Matrix10 * p.x + Matrix11 * p.y + Matrix12 * p.z + Matrix13));
            GeoPoint pogl = openGlMatrix * p;
            return new GeoPoint2D((float)((pogl.x + 1) / 2 * clientRect.Width + clientRect.Left), (float)(clientRect.Bottom - (pogl.y + 1) / 2 * clientRect.Height));
        }
        public GeoPoint2D Project2D(GeoPoint p)
        {
            return new GeoPoint2D((Matrix00 * p.x + Matrix01 * p.y + Matrix02 * p.z + Matrix03), (Matrix10 * p.x + Matrix11 * p.y + Matrix12 * p.z + Matrix13));
        }
        /// <summary>
        /// Liefert einen 3D Punkt in der Ebene, die durch den Urprung geht. Ist eigentlich so nicht zu gebrauchen
        /// denn es fehlt die Information wie weit vorne oder hinten der Punkt sein soll
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public GeoPoint UnProject(System.Drawing.Point p)
        {
            return inverseProjection * new GeoPoint((p.X - placementX) / placementFactor, -(p.Y - placementY) / placementFactor, 0.0);
        }
        private void SetPerspective(GeoPoint fromHere, GeoVector dir, Rectangle clientRect, BoundingCube modelExtent, GeoPoint fixPoint, bool useFixPoint)
        {
            GeoPoint2D fixPoint2D = WorldToWindow(fixPoint);

            perspectiveCenter = fromHere;
            direction = dir;
            isPerspective = true;
            this.clientRect = clientRect;

            placementX = 0.0; // das bringt die Blickrichtung genau in die Mitte
            placementY = 0.0;
            // placementFactor wird beibehalten

            GeoVector xaxis = direction ^ GeoVector.ZAxis;
            if (CADability.Precision.IsNullVector(xaxis)) xaxis = GeoVector.XAxis;
            xaxis.Norm();
            GeoVector yaxis = direction ^ xaxis;
            yaxis.Norm();
            GeoVector dbgz = xaxis ^ yaxis;
            ModOp topView = ModOp.Fit(new GeoVector[] { xaxis, yaxis, direction.Normalized }, new GeoVector[] { GeoVector.XAxis, GeoVector.YAxis, -GeoVector.ZAxis });
            topView = topView * ModOp.Translate(GeoPoint.Origin - perspectiveCenter);
            // blicke vom Ursprung nach unten
            modelExtent.Expand(1.0); // besser wie unten oder so...
            double near = double.MinValue;
            double far = double.MaxValue;
            for (int i = 0; i < modelExtent.Points.Length; ++i)
            {
                GeoPoint p = topView * modelExtent.Points[i];
                if (p.z > near) near = p.z;
                if (p.z < far) far = p.z;
            }
            near = -near;
            far = -far; // müssen positiv sein
            if (far < 0.0)
            {   // alles hinter dem Beobachtungspunkt
                far = modelExtent.Points[0] | modelExtent.Points[4]; // die Diagonale
            }
            if (near < 0.0)
            {   // es liegen Objekte hinter dem Beobachtungspunkt
                near = far / 100; // ab 1% wirds sichtbar
            }

            double ratio = (clientRect.Width) / (double)(clientRect.Height);
            double halfwidth = near;
            double pleft = -halfwidth;
            double pright = halfwidth;
            double pbottom = halfwidth / ratio;
            double ptop = -halfwidth / ratio;
            Matrix perspective = DenseMatrix.OfArray(new double[,]{
                {2*near/(pright-pleft),0,(pright+pleft)/(pright-pleft),0},
                {0,2*near/(ptop-pbottom),(ptop+pbottom)/(ptop-pbottom),0},
                {0,0,-(far+near)/(far-near),-2*far*near/(far-near)},
                {0,0,-1,0}});
            perspectiveProjection = new Matrix4(perspective * topView.ToMatrix()); // das ist die unscalierte und
            // unverschobene Projektion
            inversePerspectiveProjection = perspectiveProjection.GetInverse();
            // DEBUG:
            //GeoPoint dbgll0 = inversePerspectiveProjection * new GeoPoint(-1, -1, 0.0);
            //GeoPoint dbglr0 = inversePerspectiveProjection * new GeoPoint(-1, 1, 0.0);
            //GeoPoint dbgul0 = inversePerspectiveProjection * new GeoPoint(1, -1, 0.0);
            //GeoPoint dbgur0 = inversePerspectiveProjection * new GeoPoint(1, 1, 0.0);
            //GeoPoint dbgll1 = inversePerspectiveProjection * new GeoPoint(-1, -1, 1.0);
            //GeoPoint dbglr1 = inversePerspectiveProjection * new GeoPoint(-1, 1, 1.0);
            //GeoPoint dbgul1 = inversePerspectiveProjection * new GeoPoint(1, -1, 1.0);
            //GeoPoint dbgur1 = inversePerspectiveProjection * new GeoPoint(1, 1, 1.0);
            //double dbgw = dbgll0 | dbglr0;
            //CADability.GeoObject.Polyline pl0 = CADability.GeoObject.Polyline.Construct();
            //pl0.SetPoints(new GeoPoint[] { dbgll0, dbglr0, dbgur0, dbgul0 }, true);
            //CADability.GeoObject.Polyline pl1 = CADability.GeoObject.Polyline.Construct();
            //pl1.SetPoints(new GeoPoint[] { dbgll1, dbglr1, dbgur1, dbgul1 }, true);
            //GeoPoint cnt = inversePerspectiveProjection * new GeoPoint(0.0, 0.0, 1.0);
            // END DEBUG
            SetCoefficients(); // damit die OpenGL Matrix gesetzt wird
            if (useFixPoint)
            {
                GeoPoint2D fixPointNew = WorldToWindow(fixPoint);
                placementX = fixPoint2D.x - fixPointNew.x;
                placementY = fixPoint2D.y - fixPointNew.y;
                SetCoefficients(); // jetzt mit Verschiebung
            }
        }
        public void SetPerspective(GeoPoint fromHere, GeoVector dir, Rectangle clientRect, BoundingCube modelExtent)
        {
            SetPerspective(fromHere, dir, clientRect, modelExtent, GeoPoint.Origin, false);
        }
        public void SetPerspective(GeoPoint fromHere, GeoVector dir, Rectangle clientRect, BoundingCube modelExtent, GeoPoint fixPoint)
        {
            SetPerspective(fromHere, dir, clientRect, modelExtent, fixPoint, true);
        }
        internal void SetPerspective(GeoPoint fromHere, GeoVector direction, BoundingCube modelExtent, GeoPoint fixPoint)
        {
            SetPerspective(fromHere, direction, clientRect, modelExtent, fixPoint, true);
        }
        internal void CalcOpenGlMatrix()
        {
            if (!isPerspective)
            {
                int left = clientRect.Left;
                int right = clientRect.Right;
                int bottom = clientRect.Top;
                int top = clientRect.Bottom;
                // Liefert die OpenGl Projektion unter der Annahme, dass dort Gl.glViewport(0, 0, width, height);
                // gesetzt wurde. Da diese Projektion die Y-Achse umklappt sehen die UnProject Punkte so 
                // merkwürdig aus.
                // der Z-Wert der resultierenden projektion wird so bestimmt, dass die komplette bounding cube
                // in den bereich -1..1 reinpasst
                GeoPoint leftbottom = UnProject(new System.Drawing.Point(left, top)); // -> -1,-1,0,1
                GeoPoint rightbottom = UnProject(new System.Drawing.Point(right, top)); // -> 1,-1,0,1
                GeoPoint lefttop = UnProject(new System.Drawing.Point(left, bottom)); // -> -1,1,0,1
                GeoVector projdir = this.Direction;
                GeoVector dirx = rightbottom - leftbottom;
                GeoVector diry = lefttop - leftbottom;
                GeoPoint zmin, zmax;
                if (extent.IsEmpty || extent.Size == 0.0)
                {   // noch nichts im Modell, dann sollen diese drei Punkte den Extend ausmachen
                    extent.MinMax(leftbottom);
                    extent.MinMax(rightbottom);
                    extent.MinMax(lefttop);
                }
                // extend.Expand(1.0); 
                BoundingCube extentex = extent;
                extentex.Expand(Math.Max(1.0, extent.Size / 100.0)); // um zu vermeiden, dass die Box in einer Richtung die Dicke 0 hat (selbst 0.5 ist zu wenig)
                // bei zweidimensionalen Zeichnungen, die sehr groß sind, muss das Z im Verhältnis stehen, sonst sieht man die Markierung nicht
                if (projdir.x > 0)
                {
                    zmin.x = extentex.Xmin;
                    zmax.x = extentex.Xmax;
                }
                else
                {
                    zmin.x = extentex.Xmax;
                    zmax.x = extentex.Xmin;
                }
                if (projdir.y > 0)
                {
                    zmin.y = extentex.Ymin;
                    zmax.y = extentex.Ymax;
                }
                else
                {
                    zmin.y = extentex.Ymax;
                    zmax.y = extentex.Ymin;
                }
                if (projdir.z > 0)
                {
                    zmin.z = extentex.Zmin;
                    zmax.z = extentex.Zmax;
                }
                else
                {
                    zmin.z = extentex.Zmax;
                    zmax.z = extentex.Zmin;
                }

                // zmin -> ?,?,-1,1
                // zmax -> ?,?,1,1
                double[,] sys = new double[12, 12]; // die 16 Einträge sind die 4*3 matrix für das Ergebnis
                sys[0, 0] = leftbottom.x;
                sys[0, 1] = leftbottom.y;
                sys[0, 2] = leftbottom.z;
                sys[0, 3] = 1.0;
                sys[1, 4] = leftbottom.x;
                sys[1, 5] = leftbottom.y;
                sys[1, 6] = leftbottom.z;
                sys[1, 7] = 1.0;

                sys[2, 0] = rightbottom.x;
                sys[2, 1] = rightbottom.y;
                sys[2, 2] = rightbottom.z;
                sys[2, 3] = 1.0;
                sys[3, 4] = rightbottom.x;
                sys[3, 5] = rightbottom.y;
                sys[3, 6] = rightbottom.z;
                sys[3, 7] = 1.0;

                sys[4, 0] = lefttop.x;
                sys[4, 1] = lefttop.y;
                sys[4, 2] = lefttop.z;
                sys[4, 3] = 1.0;
                sys[5, 4] = lefttop.x;
                sys[5, 5] = lefttop.y;
                sys[5, 6] = lefttop.z;
                sys[5, 7] = 1.0;

                sys[6, 0] = projdir.x;
                sys[6, 1] = projdir.y;
                sys[6, 2] = projdir.z;
                sys[7, 4] = projdir.x;
                sys[7, 5] = projdir.y;
                sys[7, 6] = projdir.z;

                sys[8, 8] = dirx.x;
                sys[8, 9] = dirx.y;
                sys[8, 10] = dirx.z;

                sys[9, 8] = diry.x;
                sys[9, 9] = diry.y;
                sys[9, 10] = diry.z;

                sys[10, 8] = zmin.x;
                sys[10, 9] = zmin.y;
                sys[10, 10] = zmin.z;
                sys[10, 11] = 1.0;

                sys[11, 8] = zmax.x;
                sys[11, 9] = zmax.y;
                sys[11, 10] = zmax.z;
                sys[11, 11] = 1.0;

                Matrix mat = DenseMatrix.OfArray(sys);

                double[] res = new double[12];
                res[0] = -1; // Ergebnis leftbottom (x,y)
                res[1] = -1;
                res[2] = 1; // Ergebnis rightbottom (x,y)
                res[3] = -1;
                res[4] = -1; // Ergebnis lefttop (x,y)
                res[5] = 1;
                res[6] = 0; // Ergebnis projdir (x,y)
                res[7] = 0;
                res[8] = 0;// Ergebnis dirx (z)
                res[9] = 0;// Ergebnis diry (z)
                res[10] = 0; // Ergebnis zmin (nur z Komponente)
                res[11] = 1; // Ergebnis zmax (nur z Komponente)
                Vector b = new DenseVector(res);
                Vector x = (Vector)mat.Solve(b);
                if (x.IsValid())
                {
                    double[,] result = new double[4, 4];
                    result[0, 0] = x[0];
                    result[0, 1] = x[1];
                    result[0, 2] = x[2];
                    result[0, 3] = x[3];
                    result[1, 0] = x[4];
                    result[1, 1] = x[5];
                    result[1, 2] = x[6];
                    result[1, 3] = x[7];
                    result[2, 0] = x[8];
                    result[2, 1] = x[9];
                    result[2, 2] = x[10];
                    result[2, 3] = x[11];
                    result[3, 0] = 0.0;
                    result[3, 1] = 0.0;
                    result[3, 2] = 0.0;
                    result[3, 3] = 1.0;
                    openGlMatrix = new Matrix4(result);
                    inverseOpenGLMatrix = openGlMatrix.GetInverse();
                }
            }
        }
        internal void SetClientRect(Rectangle clr)
        {
            this.clientRect = clr;
        }
        public int Width
        {
            get
            {
                return clientRect.Width;
            }
        }
        public int Height
        {
            get
            {
                return clientRect.Height;
            }
        }
        public double[,] GetOpenGLProjection(int left, int right, int bottom, int top, BoundingCube extend)
        {
            if (clientRect.Width != right - left || clientRect.Height != top - bottom || !this.extent.Equals(extend))
            {
                clientRect = new Rectangle(left, bottom, right - left, top - bottom);
                this.extent = extend;
                SetCoefficients();
            }
            //if (clientRect.Width == 0 || clientRect.Height == 0)
            //{
            //    clientRect = new Rectangle(left, bottom, right - left, top - bottom);
            //    SetCoefficients();
            //}
            //else
            //{
            //    clientRect = new Rectangle(left, bottom, right - left, top - bottom);
            //}
            //this.extent = extend;
            //if (!IsPerspective)
            //{
            //    CalcOpenGlMatrix();
            //}
            return (double[,])openGlMatrix;
            
            //Unreachable code
            /*
            // Liefert die OpenGl Projektion unter der Annahme, dass dort Gl.glViewport(0, 0, width, height);
            // gesetzt wurde. Da diese Projektion die Y-Achse umklappt sehen die UnProject Punkte so 
            // merkwürdig aus.
            // der Z-Wert der resultierenden projektion wird so bestimmt, dass die komplette bounding cube
            // in den bereich -1..1 reinpasst
            GeoPoint leftbottom = UnProject(new System.Drawing.Point(left, top)); // -> -1,-1,0,1
            GeoPoint rightbottom = UnProject(new System.Drawing.Point(right, top)); // -> 1,-1,0,1
            GeoPoint lefttop = UnProject(new System.Drawing.Point(left, bottom)); // -> -1,1,0,1
            GeoVector projdir = this.Direction;
            GeoVector dirx = rightbottom - leftbottom;
            GeoVector diry = lefttop - leftbottom;
            GeoPoint zmin, zmax;
            if (extend.IsEmpty || extend.Size == 0.0)
            {   // noch nichts im Modell, dann sollen diese drei Punkte den Extend ausmachen
                extend.MinMax(leftbottom);
                extend.MinMax(rightbottom);
                extend.MinMax(lefttop);
            }
            // extend.Expand(1.0); 
            extend.Expand(Math.Max(1.0, extend.Size / 100.0)); // um zu vermeiden, dass die Box in einer Richtung die Dicke 0 hat (selbst 0.5 ist zu wenig)
            // bei zweidimensionalen Zeichnungen, die sehr groß sind, muss das Z im Verhältnis stehen, sonst sieht man die Markierung nicht
            if (projdir.x > 0)
            {
                zmin.x = extend.Xmin;
                zmax.x = extend.Xmax;
            }
            else
            {
                zmin.x = extend.Xmax;
                zmax.x = extend.Xmin;
            }
            if (projdir.y > 0)
            {
                zmin.y = extend.Ymin;
                zmax.y = extend.Ymax;
            }
            else
            {
                zmin.y = extend.Ymax;
                zmax.y = extend.Ymin;
            }
            if (projdir.z > 0)
            {
                zmin.z = extend.Zmin;
                zmax.z = extend.Zmax;
            }
            else
            {
                zmin.z = extend.Zmax;
                zmax.z = extend.Zmin;
            }

            // zmin -> ?,?,-1,1
            // zmax -> ?,?,1,1
            double[,] sys = new double[12, 12]; // die 16 Einträge sind die 4*3 matrix für das Ergebnis
            sys[0, 0] = leftbottom.x;
            sys[0, 1] = leftbottom.y;
            sys[0, 2] = leftbottom.z;
            sys[0, 3] = 1.0;
            sys[1, 4] = leftbottom.x;
            sys[1, 5] = leftbottom.y;
            sys[1, 6] = leftbottom.z;
            sys[1, 7] = 1.0;

            sys[2, 0] = rightbottom.x;
            sys[2, 1] = rightbottom.y;
            sys[2, 2] = rightbottom.z;
            sys[2, 3] = 1.0;
            sys[3, 4] = rightbottom.x;
            sys[3, 5] = rightbottom.y;
            sys[3, 6] = rightbottom.z;
            sys[3, 7] = 1.0;

            sys[4, 0] = lefttop.x;
            sys[4, 1] = lefttop.y;
            sys[4, 2] = lefttop.z;
            sys[4, 3] = 1.0;
            sys[5, 4] = lefttop.x;
            sys[5, 5] = lefttop.y;
            sys[5, 6] = lefttop.z;
            sys[5, 7] = 1.0;

            sys[6, 0] = projdir.x;
            sys[6, 1] = projdir.y;
            sys[6, 2] = projdir.z;
            sys[7, 4] = projdir.x;
            sys[7, 5] = projdir.y;
            sys[7, 6] = projdir.z;

            sys[8, 8] = dirx.x;
            sys[8, 9] = dirx.y;
            sys[8, 10] = dirx.z;

            sys[9, 8] = diry.x;
            sys[9, 9] = diry.y;
            sys[9, 10] = diry.z;

            sys[10, 8] = zmin.x;
            sys[10, 9] = zmin.y;
            sys[10, 10] = zmin.z;
            sys[10, 11] = 1.0;

            sys[11, 8] = zmax.x;
            sys[11, 9] = zmax.y;
            sys[11, 10] = zmax.z;
            sys[11, 11] = 1.0;

            Matrix mat = DenseMatrix.OfArray(sys);

            double[] res = new double[12];
            res[0] = -1; // Ergebnis leftbottom (x,y)
            res[1] = -1;
            res[2] = 1; // Ergebnis rightbottom (x,y)
            res[3] = -1;
            res[4] = -1; // Ergebnis lefttop (x,y)
            res[5] = 1;
            res[6] = 0; // Ergebnis projdir (x,y)
            res[7] = 0;
            res[8] = 0;// Ergebnis dirx (z)
            res[9] = 0;// Ergebnis diry (z)
            res[10] = 0; // Ergebnis zmin (nur z Komponente)
            res[11] = 1; // Ergebnis zmax (nur z Komponente)
            Vector b = new DenseVector(res);
            try
            {
                Vector x = (Vector)mat.Solve(b);

                double[,] result = new double[4, 4];
                result[0, 0] = x[0];
                result[0, 1] = x[1];
                result[0, 2] = x[2];
                result[0, 3] = x[3];
                result[1, 0] = x[4];
                result[1, 1] = x[5];
                result[1, 2] = x[6];
                result[1, 3] = x[7];
                result[2, 0] = x[8];
                result[2, 1] = x[9];
                result[2, 2] = x[10];
                result[2, 3] = x[11];
                result[3, 0] = 0.0;
                result[3, 1] = 0.0;
                result[3, 2] = 0.0;
                result[3, 3] = 1.0;
                openGlMatrix = new Matrix4(result);
                inverseOpenGLMatrix = openGlMatrix.GetInverse();
                return result;
            }
            catch (ApplicationException)
            {
                return new double[,] { { 1, 0, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } };
            }
            */
        }
        #region Funktionen im Zusammenhang mit DrawingPlane
        public Plane DrawingPlane
        {
            get { return drawingPlane; }
            set { drawingPlane = value; }
        }
        //public GeoPoint DrawingPlanePoint(Point p)
        //{
        //    GeoPoint p0 = new GeoPoint((p.X - placementX) / placementFactor, -(p.Y - placementY) / placementFactor, 0.0);
        //    p0 = inverseProjection * p0;
        //    return drawingPlane.Intersect(p0, Direction);
        //}
        //public GeoPoint DrawingPlanePoint(PointF p)
        //{
        //    GeoPoint p0 = new GeoPoint((p.X - placementX) / placementFactor, -(p.Y - placementY) / placementFactor, 0.0);
        //    p0 = inverseProjection * p0;
        //    return drawingPlane.Intersect(p0, Direction);
        //}
        /// <summary>
        /// Gets the position of a given point (usually a mouse position in view coordinates) in 
        /// a given plane. 
        /// </summary>
        /// <param name="pln">Plane of the requested point</param>
        /// <param name="p">Mouse position or point in view coordinates</param>
        /// <returns>Position of the point in the plane</returns>
        public GeoPoint2D PlanePoint(Plane pln, System.Drawing.Point p)
        {
            GeoPoint p0 = new GeoPoint((p.X - placementX) / placementFactor, -(p.Y - placementY) / placementFactor, 0.0);
            p0 = inverseProjection * p0;
            GeoPoint p1 = pln.Intersect(p0, direction);
            return pln.Project(p1);
        }
        /// <summary>
        /// Liefert den Raumpunkt (Welt3D) zum Welt2D Punkt gemäß Zeichenebene (DrawingPlane)
        /// </summary>
        /// <param name="p">der Welt2D Punkt</param>
        /// <returns>Welt3D Punkt</returns>
        public GeoPoint DrawingPlanePoint(GeoPoint2D w2d)
        {
            GeoPoint p0 = new GeoPoint(w2d.x, w2d.y, 0.0);
            p0 = inverseProjection * p0;
            if (CADability.Precision.IsPerpendicular(Direction, drawingPlane.Normal, false))
            {
                Plane pln = new Plane(drawingPlane.Location, drawingPlane.Normal, drawingPlane.Normal ^ Direction);
                return pln.Intersect(p0, Direction);
            }
            return drawingPlane.Intersect(p0, Direction);
        }
        #endregion
        #region Umwandlung verschiedener Koordinatensysteme
        // alle anderen Umwandlungen sollen abgeschafft werden bzw. diese Methoden hier verwenden
        /// <summary>
        /// Returns a beam corresponding to the 2-dimensional mouse position. The mouse position corresponds to a beam
        /// in the model which is seen as a point from the viewpoint. The result is in world coordinates
        /// of the model. Works for both parallel and perspective projection.
        /// </summary>
        /// <param name="p">Point to which the corresponding beam is required</param>
        /// <returns>The corresponding beam</returns>
        public Axis PointBeam(System.Drawing.Point p)
        {
            // der Bildschirm ist auf den OpenGL Einheitswürfel abgebildet: [-1,1][-1,1],[0,1]
            // Bestimme die Position der Maus in diesem Würfel und berechne daraus den Strahl
            if (!openGlMatrix.IsValid) Initialize();
            GeoPoint p0 = new GeoPoint(2.0 * (double)(p.X - clientRect.Left) / (double)clientRect.Width - 1, 2.0 * (double)(clientRect.Bottom - p.Y) / (double)clientRect.Height - 1, 0.0);
            GeoPoint p1 = new GeoPoint(p0.x, p0.y, 1.0);
            p0 = inverseOpenGLMatrix * p0;
            p1 = inverseOpenGLMatrix * p1;
            return new Axis(p0, p1); // das Ergebnis soll in Richtung der Beobachtung gehen
        }
        /// <summary>
        /// Returns the point in the worldcoordinate system corresponding to the (mouse-) position p and the drawingplane
        /// </summary>
        /// <param name="p">Point in window coordinates</param>
        /// <returns>Point in world coordinates</returns>
        public GeoPoint DrawingPlanePoint(System.Drawing.Point p)
        {
            Axis a = PointBeam(p);
            if (CADability.Precision.IsPerpendicular(a.Direction, drawingPlane.Normal, false))
            {
                Plane pln = new Plane(drawingPlane.Location, drawingPlane.Normal, drawingPlane.Normal ^ a.Direction);
                return pln.Intersect(a.Location, a.Direction);
            }
            return drawingPlane.Intersect(a.Location, a.Direction);
        }
        internal GeoPoint2D WorldToProjectionPlane(GeoPoint p)
        {   // Weltkoordinate in ProjectionPlane
            if (!openGlMatrix.IsValid) Initialize();
            GeoPoint pogl = openGlMatrix * p;
            pogl.z = 0.5; // somit in der ProjectionPlane
            return ProjectionPlane.Project(inverseOpenGLMatrix * pogl);
        }
        /// <summary>
        /// Returns the window position of a point in the world coordiate system. Point (0,0) is the top left point of the window
        /// </summary>
        /// <param name="p">Point in world coordinate system</param>
        /// <returns></returns>
        public GeoPoint2D WorldToWindow(GeoPoint p)
        {
            GeoPoint pogl = openGlMatrix * p;
            return new GeoPoint2D((pogl.x + 1) / 2 * clientRect.Width + clientRect.Left, clientRect.Bottom - (pogl.y + 1) / 2 * clientRect.Height);
        }
        public GeoPoint2D World2DToWindow(GeoPoint2D p)
        {
            GeoPoint pp = ProjectionPlane.ToGlobal(p);
            return WorldToWindow(pp);
        }
        public RectangleF World2DToWindow(BoundingRect br)
        {
            GeoPoint ul = ProjectionPlane.ToGlobal(br.GetUpperLeft());
            GeoPoint lr = ProjectionPlane.ToGlobal(br.GetLowerRight());
            // folgende beiden Zeilen nachträglich wg. connectoren eingefügt, hoffe das ist OK (29.11.09)
            GeoPoint2D ul2d = WorldToWindow(ul);
            GeoPoint2D lr2d = WorldToWindow(lr);
            RectangleF res = new RectangleF((float)ul2d.x, (float)ul2d.y, (float)(lr2d.x - ul2d.x), (float)(lr2d.y - ul2d.y));
            return res;

        }
        /// <summary>
        /// Returns the provided point in the world coordinate system
        /// </summary>
        /// <param name="windowPoint">A point in the windows coordinate system</param>
        /// <returns>Result in world coordinate system</returns>
        public GeoPoint WindowToWorld(PointF windowPoint)
        {   // zuerst den Fensterpunkt nach OpenGL umrechnen
            if (!openGlMatrix.IsValid) Initialize();
            GeoPoint pogl = new GeoPoint((windowPoint.X - clientRect.Left) / clientRect.Width * 2 - 1,
                -(windowPoint.Y - clientRect.Bottom) / clientRect.Height * 2 - 1, 0.0);
            GeoPoint res = inverseOpenGLMatrix * pogl;
            return res;
        }
        public enum ArrowMode { circleArrow, twoArrows }
        public GeoObjectList MakeArrow(GeoPoint p1, GeoPoint p2, Plane plane, ArrowMode mode)
        {
            GeoObjectList res = new GeoObjectList();
            // we need a plane which contains the line p1-p2 as the x-axis
            if (plane.IsValid())
            {
                try
                {
                    Plane arrowPlane = new Plane(p1, p2 - p1, (p2 - p1) ^ plane.Normal);
                    double headx = arrowPlane.Project(p2).x;
                    double arrowSize = (Height + Width) * 0.008 * DeviceToWorldFactor; // 1% of the display size 
                    res.Add(Line.TwoPoints(p1, p2));
                    res.Add(Face.MakeFace(new PlaneSurface(arrowPlane), new SimpleShape(Border.MakeCircle(GeoPoint2D.Origin, arrowSize))));
                    res.Add(Face.MakeFace(new PlaneSurface(arrowPlane), new SimpleShape(Border.MakePolygon(new GeoPoint2D[] { new GeoPoint2D(headx, 0), new GeoPoint2D(headx - arrowSize, arrowSize), new GeoPoint2D(headx - arrowSize, -arrowSize) }))));
                }
                catch (PlaneException) { }
            }
            return res;
        }
        /// <summary>
        /// Make an dimension arrow between p1 and p2. Dimension help line connect to d1 and d2
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <param name="d1"></param>
        /// <param name="d2"></param>
        /// <param name="plane"></param>
        /// <param name="mode"></param>
        /// <returns></returns>
        public GeoObjectList MakeArrow(GeoPoint p1, GeoPoint p2, GeoPoint d1, GeoPoint d2, Plane plane, ArrowMode mode)
        {
            GeoObjectList res = new GeoObjectList();
            // we need a plane which contains the line p1-p2 as the x-axis
            if (plane.IsValid())
            {
                try
                {
                    Plane arrowPlane = new Plane(p1, p2 - p1, (p2 - p1) ^ plane.Normal);
                    double headx = arrowPlane.Project(p2).x;
                    double arrowSize = (Height + Width) * 0.008 * DeviceToWorldFactor; // 1% of the display size 
                    res.Add(Line.TwoPoints(p1, p2));
                    res.Add(Face.MakeFace(new PlaneSurface(arrowPlane), new SimpleShape(Border.MakeCircle(GeoPoint2D.Origin, arrowSize))));
                    res.Add(Face.MakeFace(new PlaneSurface(arrowPlane), new SimpleShape(Border.MakePolygon(new GeoPoint2D[] { new GeoPoint2D(headx, 0), new GeoPoint2D(headx - arrowSize, arrowSize), new GeoPoint2D(headx - arrowSize, -arrowSize) }))));
                    // paintTo3D.TriangulateText auf false setzen!
                }
                catch (PlaneException) { }
            }
            return res;
        }
        /// <summary>
        /// make an arrow showing a rotation operation. The rotation is around the <paramref name="axis"/>
        /// and goes from the vector <paramref name="fromHere"/> to the vector <paramref name="toHere"/>.
        /// </summary>
        /// <param name="axis"></param>
        /// <param name="fromHere"></param>
        /// <param name="toHere"></param>
        /// <returns></returns>
        public GeoObjectList MakeRotationArrow(Axis axis, GeoVector fromHere, GeoVector toHere)
        {
            GeoObjectList res = new GeoObjectList();
            GeoVector axisDirection = axis.Direction.Normalized; // to make further calculation easier
            fromHere.Norm();
            toHere.Norm();
            double arrowSize = (Height + Width) * 0.008 * DeviceToWorldFactor; // 1% of the display size 
            double radius = arrowSize * 10; // radius of the arc
            Plane arcPlane = new Plane(axis.Location, axisDirection); // normal to the rotation axis
            Plane fromPlane = new Plane(axis.Location, fromHere, axisDirection);
            Plane toPlane = new Plane(axis.Location, toHere, axisDirection);
            // a rectangular face to display the vector fromHere
            GeoPoint p1 = axis.Location + arrowSize * axisDirection;
            GeoPoint p2 = p1 + radius * fromHere;
            GeoPoint p3 = p2 - 2 * arrowSize * axisDirection;
            GeoPoint p4 = p3 - radius * fromHere;
            Face from = Face.MakeFace(p1, p2, p3, p4);
            p1 = axis.Location + arrowSize * axisDirection;
            p2 = p1 + radius * toHere;
            p3 = p2 - 2 * arrowSize * axisDirection;
            p4 = p3 - radius * toHere;
            Face to = Face.MakeFace(p1, p2, p3, p4);
            res.Add(from);
            res.Add(to);
            Ellipse e = Ellipse.Construct(); // the arc of the arrow
            e.SetArcPlaneCenterStartEndPoint(arcPlane, arcPlane.Project(axis.Location), arcPlane.Project(axis.Location + radius * fromHere), arcPlane.Project(axis.Location + radius * toHere), arcPlane, true);
            if (Math.Abs(e.SweepParameter) > Math.PI)
                e.SetArcPlaneCenterStartEndPoint(arcPlane, arcPlane.Project(axis.Location), arcPlane.Project(axis.Location + radius * fromHere), arcPlane.Project(axis.Location + radius * toHere), arcPlane, false);
            // e is too long, we have to shorten it by arrowSize
            double eParam = (e as ICurve).PositionAtLength(e.Length - arrowSize);
            e.Trim(0.0, eParam);
            PlaneSurface ps = new PlaneSurface(fromPlane);
            Face arrowStart = Face.MakeFace(ps, SimpleShape.MakePolygon(fromPlane.Project(axis.Location + radius * fromHere), arrowSize / 2.0, 5));
            Path arrowBase = Path.Construct();
            arrowBase.Set(new ICurve[] { e });
            res.Add(Make3D.MakePipe(arrowStart, arrowBase, null));
            GeoVector arrowDir = arrowSize * e.EndDirection.Normalized;
            res.Add(Make3D.MakeCone(axis.Location + radius * toHere - arrowDir, axisDirection, arrowDir, arrowSize, arrowSize * 0.1));
            return res;
        }


        internal GeoVector horizontalAxis
        {
            get
            {
                if (!openGlMatrix.IsValid) Initialize();
                GeoPoint p0 = inverseOpenGLMatrix * new GeoPoint(0.0, 0.0, 0.5);
                GeoPoint p1 = inverseOpenGLMatrix * new GeoPoint(1.0, 0.0, 0.5);
                GeoVector haxis = p1 - p0;
                return haxis.Normalized;
            }
        }
        internal GeoVector verticalAxis
        {
            get
            {
                if (!openGlMatrix.IsValid) Initialize();
                GeoPoint p0 = inverseOpenGLMatrix * new GeoPoint(0.0, 0.0, 0.5);
                GeoPoint p1 = inverseOpenGLMatrix * new GeoPoint(0.0, 1.0, 0.5);
                GeoVector vaxis = p1 - p0;
                return vaxis.Normalized;
            }
        }
        #endregion

        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected Projection(SerializationInfo info, StreamingContext context)
        {
            unscaledProjection = (ModOp)info.GetValue("UnscaledProjection", typeof(ModOp));
            placementFactor = (double)info.GetValue("PlacementFactor", typeof(double));
            placementX = (double)info.GetValue("PlacementX", typeof(double));
            placementY = (double)info.GetValue("PlacementY", typeof(double));
            drawingPlane = (Plane)info.GetValue("DrawingPlane", typeof(Plane));
            grid = (Grid)InfoReader.Read(info, "Grid", typeof(Grid));
            if (grid == null) grid = new Grid();
            try
            {
                LayoutFactor = (double)info.GetValue("LayoutFactor", typeof(double));
            }
            catch (SerializationException)
            {
                LayoutFactor = 1.0;
            }
            try
            {
                showFaces = (bool)info.GetValue("ShowFaces", typeof(bool));
            }
            catch (SerializationException)
            {
                showFaces = false;
            }
            try
            {
                LineWidthFactor = (double)info.GetValue("LineWidthFactor", typeof(double));
            }
            catch (SerializationException)
            {
                LineWidthFactor = 10.0;
            }
            try
            {
                UseLineWidth = (bool)info.GetValue("UseLineWidth", typeof(bool));
            }
            catch (SerializationException)
            {
                UseLineWidth = false;
            }
            try
            {
                ShowDrawingPlane = (bool)info.GetValue("ShowDrawingPlane", typeof(bool));
            }
            catch (SerializationException)
            {
                ShowDrawingPlane = false;
            }
            try
            {
                isPerspective = info.GetBoolean("IsPerspective");
                perspectiveProjection = (Matrix4)info.GetValue("PerspectiveProjection", typeof(Matrix4));
                direction = (GeoVector)info.GetValue("Direction", typeof(GeoVector));
            }
            catch (SerializationException)
            {
                isPerspective = false;
            }
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("UnscaledProjection", unscaledProjection, typeof(ModOp));
            info.AddValue("PlacementFactor", placementFactor, typeof(double));
            info.AddValue("PlacementX", placementX, typeof(double));
            info.AddValue("PlacementY", placementY, typeof(double));
            info.AddValue("DrawingPlane", drawingPlane, typeof(Plane));
            info.AddValue("Grid", grid, typeof(Grid));
            info.AddValue("LayoutFactor", LayoutFactor, typeof(double));
            info.AddValue("ShowFaces", showFaces, typeof(bool));
            info.AddValue("LineWidthFactor", LineWidthFactor, typeof(double));
            info.AddValue("UseLineWidth", UseLineWidth, typeof(bool));
            info.AddValue("ShowDrawingPlane", showDrawingPlane, typeof(bool));
            info.AddValue("IsPerspective", isPerspective);
            info.AddValue("PerspectiveProjection", perspectiveProjection);
            info.AddValue("Direction", direction);
        }

        #endregion
        #region IDeserializationCallback Members
        void IDeserializationCallback.OnDeserialization(object sender)
        {
            // clientRect ist hier noch nicht gesetzt und das wird aber bei SetCoefficients benutzt
            clientRect = new Rectangle(0, 0, 100, 100); // irgend ein Wert muss halt rein
            if (IsPerspective)
            {
                inversePerspectiveProjection = perspectiveProjection.GetInverse();
                SetCoefficients();
            }
            else
            {
                SetCoefficients();
            }
        }
        #endregion

        internal void Initialize()
        {
            if (clientRect.IsEmpty) clientRect = new Rectangle(0, 0, 100, 100); // irgend ein Wert muss halt rein
            if (IsPerspective)
            {
                inversePerspectiveProjection = perspectiveProjection.GetInverse();
                SetCoefficients();
            }
            else
            {
                SetCoefficients();
            }
        }

        internal bool IsInitialized
        {
            get
            {
                return openGlMatrix.IsValid;
            }
        }

    }
}
