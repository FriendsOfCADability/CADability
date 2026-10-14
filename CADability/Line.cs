using CADability.Curve2D;
using System;
#if WEBASSEMBLY
using CADability.WebDrawing;
using Point = CADability.WebDrawing.Point;
#else
using System.Drawing;
using Point = System.Drawing.Point;
#endif
using System.Collections.Generic;
using System.Runtime.Serialization;
using MathNet.Numerics.LinearAlgebra.Double;

namespace CADability.GeoObject
{
    using CADability.Attribute;
    using CADability.UserInterface;

    /// <summary>
    /// The line is a <see cref="IGeoObject"/>. It is actually a line segment
    /// not an infinite line.
    /// </summary>
    [Serializable()]
    public class Line : IGeoObjectImpl, IColorDef, ILineWidth, ILinePattern,
        ISerializable, ICurve, IExtentedableCurve, ISimpleCurve, IExplicitPCurve3D, IJsonSerialize, IExportStep
    {
        private GeoPoint startPoint;
        private GeoPoint endPoint;
        private LineWidth lineWidth;
        private LinePattern linePattern;
        #region polymorph construction
        /// <summary>
        /// Delegate for the construction of a Line.
        /// </summary>
        /// <returns>A Line or Line derived class</returns>
        public delegate Line ConstructionDelegate();
        /// <summary>
        /// Provide a delegate here if you want you Line derived class to be
        /// created each time CADability creates a line.
        /// </summary>
        public static ConstructionDelegate Constructor;
        /// <summary>
        /// The only way to create a line. There are no public constructors for the line to assure
        /// that this is the only way to construct a line.
        /// </summary>
        /// <returns></returns>
        public static Line Construct()
        {
            if (Constructor != null) return Constructor();
            return new Line();
        }
        /// <summary>
        /// Handler for <see cref="Constructed"/>.
        /// </summary>
        /// <param name="justConstructed">the line that was just created</param>
        public delegate void ConstructedDelegate(Line justConstructed);
        /// <summary>
        /// Raised from the constructor whenever a line is created, be it by <see cref="Construct"/>, by cloning or by
        /// deserialization, before its geometry is set. An application can use it to attach data of its own to every line.
        /// </summary>
        public static event ConstructedDelegate Constructed;
        #endregion
        /// <summary>
        /// Empty protected constructor.
        /// </summary>
        protected Line()
        {
            colorDef = new ColorDef(null, Color.Black);
            if (Constructed != null) Constructed(this);
        }
        /// <summary>
        /// Creates a line from <paramref name="sp"/> to <paramref name="ep"/>. The line is created with <see cref="Construct"/>,
        /// so a registered <see cref="Constructor"/> is honored. Same as <see cref="MakeLine"/>.
        /// </summary>
        /// <param name="sp">the start point</param>
        /// <param name="ep">the end point</param>
        /// <returns>the new line</returns>
        public static Line TwoPoints(GeoPoint sp, GeoPoint ep)
        {
            Line res = Line.Construct();
            res.SetTwoPoints(sp, ep);
            return res;
        }
        /// <summary>
        /// Sets or gets the startpoint of the line. Setting the startpoint causes the line to fire the
        /// <see cref="IGeoObject.WillChangeEvent"/> and the <see cref="IGeoObject.DidChangeEvent"/>.
        /// </summary>
        public virtual GeoPoint StartPoint
        {
            get => startPoint;
            set
            {
                using (Changing.Create(this, startPoint))
                {
                    startPoint = value;
                }
            }
        }
        /// <summary>
        /// Sets or gets the endpoint of the line. Setting the endpoint causes the line to fire the
        /// <see cref="IGeoObject.WillChangeEvent"/> and the <see cref="IGeoObject.DidChangeEvent"/>.
        /// </summary>
        public virtual GeoPoint EndPoint
        {
            get => endPoint;
            set
            {
                using (Changing.Create(this, endPoint))
                {
                    endPoint = value;
                }
            }
        }
        /// <summary>
        /// Sets the start and endpoint of the line. This method causes the line to fire the
        /// <see cref="IGeoObject.WillChangeEvent"/> and the <see cref="IGeoObject.DidChangeEvent"/>.
        /// </summary>
        /// <param name="startPoint">the new startpoint</param>
        /// <param name="endPoint">the new endpoint</param>
        public void SetTwoPoints(GeoPoint startPoint, GeoPoint endPoint)
        {
            using (new Changing(this, "SetTwoPoints", this.startPoint, this.endPoint))
            {
                this.startPoint = startPoint;
                this.endPoint = endPoint;
            }
        }
        /// <summary>
        /// Gets or sets the length of the line. Setting the length modifies the endpoint and keeps the startpoint
        /// and causes the line to fire the
        /// <see cref="IGeoObject.WillChangeEvent"/> and the <see cref="IGeoObject.DidChangeEvent"/>.
        /// </summary>
        public double Length
        {
            get => Geometry.Dist(startPoint, endPoint);
            set
            {
                using (Changing.Create(this, Length))
                {
                    GeoVector v = endPoint - startPoint;
                    if (!v.IsNullVector()) v.Norm();
                    else v = new GeoVector(1.0, 0.0, 0.0); // damit wenigstens die Länge gesetzt werden kann,
                    // wenn auch keine Richtung bekannt ist
                    endPoint = startPoint + value * v;
                }
            }
        }
        /// <summary>
        /// do not use, used only internaly.
        /// </summary>
        public GeoPoint LengthFixPoint
        {	// TODO: entfernen!!!
            get
            {
                return startPoint;
            }
        }
        /// <summary>
        /// Overrides <see cref="IGeoObjectImpl.Modify"/> and implements <see cref="IGeoObject.Modify"/>.
        /// </summary>
        /// <param name="m">the operator for the modification</param>
        public override void Modify(ModOp m)
        {
            using (new Changing(this, nameof(ModifyInverse), m))
            {
                startPoint = m * startPoint;
                endPoint = m * endPoint;
            }
        }
        /// <summary>
        /// Overrides <see cref="IGeoObjectImpl.Clone"/> and implements <see cref="IGeoObject.Clone"/>.
        /// Returns a clone of this line.
        /// </summary>
        /// <returns>the clone</returns>
        public override IGeoObject Clone()
        {
            Line result = Construct();
            ++result.isChanging;
            result.CopyGeometry(this);
            result.CopyAttributes(this);
            --result.isChanging;
            return result;
        }
        /// <summary>
        /// Overrides <see cref="IGeoObjectImpl.CopyGeometry"/> and implements <see cref="IGeoObject.CopyGeometry"/>.
        /// Copies the start and endpoint of the given line. This method causes the line to fire the
        /// <see cref="IGeoObject.WillChangeEvent"/> and the <see cref="IGeoObject.DidChangeEvent"/>.
        /// </summary>
        /// <param name="ToCopyFrom">must be a line, to copy data from</param>
        public override void CopyGeometry(IGeoObject ToCopyFrom)
        {
            using (new Changing(this))
            {
                startPoint = ((Line)ToCopyFrom).startPoint;
                endPoint = ((Line)ToCopyFrom).endPoint;
            }
        }
        /// <inheritdoc/>
        public override void FindSnapPoint(SnapPointFinder spf)
        {
            if (!spf.Accept(this)) return;
            if (spf.SnapToObjectCenter)
            {
                GeoPoint Center = new GeoPoint(StartPoint, EndPoint);
                spf.Check(Center, this, SnapPointFinder.DidSnapModes.DidSnapToObjectCenter);
            }
            if (spf.SnapToObjectSnapPoint)
            {
                spf.Check(StartPoint, this, SnapPointFinder.DidSnapModes.DidSnapToObjectSnapPoint);
                spf.Check(EndPoint, this, SnapPointFinder.DidSnapModes.DidSnapToObjectSnapPoint);
            }
            if (spf.SnapToDropPoint && spf.BasePointValid)
            {
                GeoPoint toTest = Geometry.DropPL(spf.BasePoint, startPoint, endPoint);
                spf.Check(toTest, this, SnapPointFinder.DidSnapModes.DidSnapToDropPoint);
            }
            if (spf.SnapToObjectPoint)
            {
                double par = PositionOf(spf.SourcePoint3D, spf.Projection.ProjectionPlane);
                // TODO: hier ist eigentlich gefragt der nächste punkt auf der Linie im Sinne des Projektionsstrahls
                if (par >= 0.0 && par <= 1.0)
                {
                    spf.Check(PointAt(par), this, SnapPointFinder.DidSnapModes.DidSnapToObjectPoint);
                }
            }
        }
        /// <inheritdoc/>
        public override BoundingCube GetBoundingCube()
        {
            BoundingCube res = BoundingCube.EmptyBoundingCube;
            res.MinMax(startPoint);
            res.MinMax(endPoint);
            return res;
        }
        /// <inheritdoc/>
        public override bool HasValidData()
        {
            if (Precision.IsEqual(startPoint, endPoint)) return false;
            return true;
        }
        /// <summary>
        /// Handler for <see cref="OnPaintTo3D"/>.
        /// </summary>
        /// <param name="toPaint">the line being painted</param>
        /// <param name="paintTo3D">the paint interface to paint to</param>
        /// <returns>true if the handler painted the line itself and the default painting is to be skipped, false to let the
        /// line paint itself as usual</returns>
        public delegate bool PaintTo3DDelegate(Line toPaint, IPaintTo3D paintTo3D);
        /// <summary>
        /// Hook to replace the painting of lines: when set, <see cref="PaintTo3D"/> calls it first and does nothing more
        /// when it returns true. The hook is static, so it applies to every line of the process.
        /// </summary>
        public static PaintTo3DDelegate OnPaintTo3D;
        /// <inheritdoc/>
        public override void PaintTo3D(IPaintTo3D paintTo3D)
        {
            if (OnPaintTo3D != null && OnPaintTo3D(this, paintTo3D)) return;
            if (!paintTo3D.SelectMode)
            {
                if (colorDef != null) paintTo3D.SetColor(colorDef.Color);
            }
            if (lineWidth != null) paintTo3D.SetLineWidth(lineWidth);
            if (linePattern != null) paintTo3D.SetLinePattern(linePattern);
            else paintTo3D.SetLinePattern(null);
            paintTo3D.Polyline(new GeoPoint[] { startPoint, endPoint });
        }
        /// <inheritdoc/>
        public override void PrepareDisplayList(double precision)
        {
            // nichts zu tun
        }
        /// <inheritdoc/>
        public override Style.EDefaultFor PreferredStyle
        {
            get
            {
                return Style.EDefaultFor.Curves;
            }
        }
        /// <inheritdoc/>
        public override IQuadTreeInsertableZ GetQuadTreeItem(Projection projection, ExtentPrecision extentPrecision)
        {
            return new QuadTreeLine(this, startPoint, endPoint, projection);
        }
        /// <inheritdoc/>
        public override BoundingRect GetExtent(Projection projection, ExtentPrecision extentPrecision)
        {
            return new BoundingRect(projection.ProjectUnscaled(startPoint), projection.ProjectUnscaled(endPoint));
        }
        #region IOctTreeInsertable members
        /// <inheritdoc/>
        public override BoundingCube GetExtent(double precision)
        {
            return GetBoundingCube();
        }
        /// <inheritdoc/>
        public override bool HitTest(ref BoundingCube cube, double precision)
        {
            return cube.Interferes(ref startPoint, ref endPoint);
        }
        /// <inheritdoc/>
        public override bool HitTest(Projection projection, BoundingRect rect, bool onlyInside)
        {
            if (onlyInside)
            {
                return projection.ProjectUnscaled(startPoint) <= rect && projection.ProjectUnscaled(endPoint) <= rect;
            }
            else
            {
                ClipRect clr = new ClipRect(rect);
                return clr.LineHitTest(projection.ProjectUnscaled(startPoint), projection.ProjectUnscaled(endPoint));
            }
        }
        /// <inheritdoc/>
        public override bool HitTest(Projection.PickArea area, bool onlyInside)
        {
            GeoPoint sp = area.ToUnitBox * startPoint;
            GeoPoint ep = area.ToUnitBox * endPoint;
            if (onlyInside)
            {
                return BoundingCube.UnitBoundingCube.Contains(sp) && BoundingCube.UnitBoundingCube.Contains(ep);
            }
            else
            {
                // im Falle !isPerspective ginge es natürlich einfacher, s.o.
                return BoundingCube.UnitBoundingCube.Interferes(ref sp, ref ep);
            }
        }
        /// <inheritdoc/>
        public override double Position(GeoPoint fromHere, GeoVector direction, double precision)
        {   // noch nicht getestet
            double pos1, pos2;
            GeoVector ldir = endPoint - startPoint;
            if (ldir.IsNullVector())
            {
                return Geometry.LinePar(fromHere, direction, startPoint);
            }
            else
            {
                double d = Geometry.DistLL(startPoint, ldir, fromHere, direction, out pos1, out pos2);
                return pos2;
            }
        }
        #endregion
        #region ISerializable Members
        private object GetSaveValue(SerializationInfo info, string name)
        {
            try
            {
                return info.GetValue(name, typeof(object));
            }
            catch (SerializationException)
            {
                return null;
            }
        }
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected Line(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            startPoint = (GeoPoint)info.GetValue("StartPoint", typeof(GeoPoint));
            endPoint = (GeoPoint)info.GetValue("EndPoint", typeof(GeoPoint));
            colorDef = ColorDef.Read(info, context);
            lineWidth = LineWidth.Read("LineWidth", info, context);
            linePattern = LinePattern.Read("LinePattern", info, context);
            if (Constructed != null) Constructed(this);
        }

        /// <summary>
        /// Writes the data of this object for the legacy binary serialization (<see cref="System.Runtime.Serialization.ISerializable"/>).
        /// Files are written in the JSON format (<see cref="IJsonSerialize"/>) now; the binary format is still read.
        /// </summary>
        /// <param name="info">receives the data</param>
        /// <param name="context">the destination of the serialization</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("StartPoint", startPoint);
            info.AddValue("EndPoint", endPoint);
            info.AddValue("ColorDef", colorDef);
            info.AddValue("LineWidth", lineWidth);
            info.AddValue("LinePattern", linePattern);
        }
        /// <inheritdoc/>
        public override void GetObjectData(IJsonWriteData data)
        {
            base.GetObjectData(data);
            data.AddProperty("StartPoint", startPoint);
            data.AddProperty("EndPoint", endPoint);
            if (colorDef != null) data.AddProperty("ColorDef", colorDef);
            if (lineWidth != null) data.AddProperty("LineWidth", lineWidth);
            if (linePattern != null) data.AddProperty("LinePattern", linePattern);
        }

        /// <inheritdoc/>
        public override void SetObjectData(IJsonReadData data)
        {
            base.SetObjectData(data);
            startPoint = data.GetProperty<GeoPoint>("StartPoint");
            endPoint = data.GetProperty<GeoPoint>("EndPoint");
            colorDef = data.GetPropertyOrDefault<ColorDef>("ColorDef");
            lineWidth = data.GetPropertyOrDefault<LineWidth>("LineWidth");
            linePattern = data.GetPropertyOrDefault<LinePattern>("LinePattern");
        }
        #endregion
        /// <summary>
        /// Returns the property entry (a <see cref="ShowPropertyLine"/>) that shows and edits this line, i.e. its start and
        /// end point, length and direction and its attributes, in the property grid.
        /// </summary>
        public override IPropertyEntry GetShowProperties(IFrame Frame)
        {
            return new ShowPropertyLine(this, Frame);
        }
        #region IColorDef Members
        private ColorDef colorDef;
        /// <summary>
        /// The color of the line, see <see cref="IColorDef.ColorDef"/>. Setting it raises the change events as a change of
        /// an attribute and is undoable. A color with <see cref="ColorDef.ColorSource.fromParent"/> is replaced by the
        /// corresponding color of the <see cref="Block"/> the line belongs to.
        /// </summary>
        public ColorDef ColorDef
        {
            get => colorDef;
            set
            {
                using (ChangingAttribute.Create(this, colorDef))
                {
                    colorDef = ColorDefForOwner(value);
                }
            }
        }
        void IColorDef.SetTopLevel(ColorDef newValue)
        {
            colorDef = newValue;
        }
        void IColorDef.SetTopLevel(ColorDef newValue, bool overwriteChildNullColor)
        {
            (this as IColorDef).SetTopLevel(newValue);
        }
        #endregion

        #region IOcasEdge Members

        #endregion

        #region ICurve Members
        /// <summary>
        /// The direction from the start point to the end point. Its length is the length of the line, and it is the same
        /// for every point of the line.
        /// </summary>
        public GeoVector StartDirection
        {
            get
            {
                return endPoint - startPoint;
            }
        }
        /// <summary>
        /// The direction from the start point to the end point, the same as <see cref="StartDirection"/>.
        /// </summary>
        public GeoVector EndDirection
        {
            get
            {
                return endPoint - startPoint;
            }
        }
        /// <inheritdoc/>
        public GeoVector DirectionAt(double Position)
        {
            return endPoint - startPoint;
        }
        /// <inheritdoc/>
        public GeoPoint PointAt(double Position)
        {
            return startPoint + Position * (endPoint - startPoint);
        }
        /// <inheritdoc/>
        public double PositionOf(GeoPoint p)
        {
            return Geometry.LinePar(startPoint, endPoint, p);
        }
        /// <inheritdoc/>
        public double PositionOf(GeoPoint p, double prefer)
        {
            return PositionOf(p);
        }
        /// <inheritdoc/>
        public double PositionOf(GeoPoint p, Plane pl)
        {
            Line2D l2d = new Line2D(pl.Project(startPoint), pl.Project(endPoint));
            return l2d.PositionOf(pl.Project(p));
        }
        double ICurve.Length
        {
            get
            {
                return Length;
            }
        }
        void ICurve.Reverse()
        {
            using (new Changing(this, typeof(ICurve), "Reverse", new object[0])) // ob das mit "ICurve.Reverse" geht?
            {
                GeoPoint tmp = endPoint;
                endPoint = startPoint;
                startPoint = tmp;
            }
        }
        /// <inheritdoc/>
        public ICurve[] Split(double Position)
        {
            if (Position <= 0.0 || Position >= 1.0)
                return new ICurve[1] { this.Clone() as ICurve };
            Line l1 = this.Clone() as Line;
            Line l2 = this.Clone() as Line;
            l1.EndPoint = PointAt(Position);
            l2.StartPoint = l1.EndPoint;
            return new ICurve[2] { l1, l2 };
        }
        /// <inheritdoc/>
        public ICurve[] Split(double Position1, double Position2)
        {
            return null; // geht nur für geschlossene
        }
        /// <inheritdoc/>
        public void Trim(double StartPos, double EndPos)
        {
            GeoPoint newStartPoint = PointAt(StartPos);
            GeoPoint newEndPoint = PointAt(EndPos);
            using (new Changing(this))
            {
                startPoint = newStartPoint;
                endPoint = newEndPoint;
            }
        }
        ICurve ICurve.Clone() { return (ICurve)this.Clone(); }
        ICurve ICurve.CloneModified(ModOp m)
        {
            IGeoObject clone = Clone();
            clone.Modify(m);
            return (ICurve)clone;
        }
        /// <summary>
        /// Always false: a line is never closed.
        /// </summary>
        public bool IsClosed
        {
            get
            {
                return false;
            }
        }
        /// <summary>
        /// True if start point and end point coincide within <see cref="Precision"/>, i.e. the line has no length and no
        /// direction. Such a line is not valid, see <see cref="HasValidData"/>.
        /// </summary>
        public bool IsSingular
        {
            get
            {
                return Precision.IsEqual(startPoint, endPoint);
            }
        }
        /// <inheritdoc/>
        public PlanarState GetPlanarState()
        {
            return PlanarState.UnderDetermined;
        }
        /// <inheritdoc/>
        public Plane GetPlane()
        {
            throw new CurveException(CurveException.Mode.NoPlane);
        }
        /// <inheritdoc/>
        public bool IsInPlane(Plane p)
        {
            return Precision.IsPointOnPlane(startPoint, p) && Precision.IsPointOnPlane(endPoint, p);
        }
        /// <inheritdoc/>
        public ICurve2D GetProjectedCurve(Plane p)
        {
            GeoPoint sp = p.ToLocal(startPoint);
            GeoPoint ep = p.ToLocal(endPoint);
            Line2D res = new Line2D(sp.To2D(), ep.To2D());
            return res;
        }
        /// <inheritdoc/>
        public override string Description
        {
            get
            {
                return StringTable.GetString("Line.Description");
            }
        }
        bool ICurve.IsComposed
        {
            get { return false; }
        }
        ICurve[] ICurve.SubCurves
        {
            get { return new ICurve[0]; }
        }
        ICurve ICurve.Approximate(bool linesOnly, double maxError)
        {
            return Clone() as ICurve;
        }
        double[] ICurve.TangentPosition(GeoVector direction)
        {
            if (Precision.SameDirection(direction, endPoint - startPoint, false))
            {
                return new double[] { 0.5 };
            }
            return null;
        }
        double[] ICurve.GetSelfIntersections()
        {
            return new double[0];
        }
        bool ICurve.SameGeometry(ICurve other, double precision)
        {
            if (other is Line)
            {
                if ((other.StartPoint | startPoint) < precision && (other.EndPoint | this.endPoint) < precision) return true;
                if ((other.StartPoint | endPoint) < precision && (other.EndPoint | this.startPoint) < precision) return true;
            }
            return false;
        }
        double ICurve.PositionAtLength(double position)
        {
            return position / Length;
        }
        double ICurve.ParameterToPosition(double parameter)
        {
            return parameter;
        }
        double ICurve.PositionToParameter(double position)
        {
            return position;
        }
        BoundingCube ICurve.GetExtent()
        {
            return GetExtent(0.0);
        }
        bool ICurve.Extend(double atStart, double atEnd)
        {
            using (new Changing(this, "SetTwoPoints", this.startPoint, this.endPoint))
            {
                startPoint = startPoint - atStart * StartDirection.Normalized;
                endPoint = endPoint + atEnd * StartDirection.Normalized;
            }
            return true;
        }
        bool ICurve.HitTest(BoundingCube cube)
        {
            return HitTest(ref cube, 0.0);
        }
        double[] ICurve.GetSavePositions()
        {
            return new double[] { 0.0, 1.0 };
        }
        double[] ICurve.GetExtrema(GeoVector direction)
        {
            return new double[0];
        }
        double[] ICurve.GetPlaneIntersection(Plane plane)
        {
            return (this as ISimpleCurve).GetPlaneIntersection(plane);
        }
        double ICurve.DistanceTo(GeoPoint p)
        {
            double se = Math.Min(p | startPoint, p | endPoint);
            double lp = Geometry.DistPL(p, startPoint, endPoint);
            if (lp < se)
            {
                double pos = Geometry.LinePar(startPoint, endPoint, p);
                if (pos >= 0.0 && pos <= 1.0) return lp;
            }
            return se;
        }
        bool ICurve.TryPointDeriv2At(double position, out GeoPoint point, out GeoVector deriv1, out GeoVector deriv2)
        {
            point = startPoint + position * (endPoint - startPoint);
            deriv1 = endPoint - startPoint;
            deriv2 = GeoVector.NullVector;
            return true;
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.PointAndDerivativesAt(double, int)"/>: all derivatives
        /// beyond the first one vanish.
        /// </summary>
        public IReadOnlyList<GeoVector> PointAndDerivativesAt(double position, int grad)
        {
            List<GeoVector> res = new List<GeoVector>();
            if (grad >= 0) res.Add((startPoint + position * (endPoint - startPoint)).ToVector());
            if (grad >= 1) res.Add(endPoint - startPoint);
            for (int i = 0; i < grad - 1; i++)
            {
                res.Add(GeoVector.NullVector);
            }
            return res;
        }
        #endregion

        #region ILineWidth Members
        /// <summary>
        /// The line width attribute of the line, see <see cref="ILineWidth.LineWidth"/>; null means the default width.
        /// Setting it raises the change events as a change of an attribute and is undoable.
        /// </summary>
        public LineWidth LineWidth
        {
            get => lineWidth;
            set
            {
                using (ChangingAttribute.Create(this, lineWidth))
                {
                    lineWidth = value;
                }
            }
        }
        #endregion

        #region ILinePattern Members

        /// <summary>
        /// The line pattern attribute of the line, see <see cref="ILinePattern.LinePattern"/>; null means a solid line.
        /// Setting it raises the change events as a change of an attribute and is undoable.
        /// </summary>
        public LinePattern LinePattern
        {
            get => linePattern;
            set
            {
                using (ChangingAttribute.Create(this, linePattern))
                {
                    linePattern = value;
                }
            }
        }

        #endregion

        #region ICndHlp3DEdge Members
        #endregion

        #region IExtentedableCurve Members

        IOctTreeInsertable IExtentedableCurve.GetExtendedCurve(ExtentedableCurveDirection direction)
        {
            switch (direction)
            {
                default:
                case ExtentedableCurveDirection.both:
                    return new InfiniteLine(startPoint, endPoint - startPoint);
                case ExtentedableCurveDirection.forward:
                    return new InfiniteRay(startPoint, endPoint - startPoint);
                case ExtentedableCurveDirection.backward:
                    return new InfiniteRay(endPoint, startPoint - endPoint);
            }
        }

        #endregion

        /// <summary>
        /// Creates a line from <paramref name="p1"/> to <paramref name="p2"/>. Identical to <see cref="TwoPoints"/>.
        /// </summary>
        /// <param name="p1">the start point</param>
        /// <param name="p2">the end point</param>
        /// <returns>the new line</returns>
        public static Line MakeLine(GeoPoint p1, GeoPoint p2)
        {
            Line res = Line.Construct();
            res.SetTwoPoints(p1, p2);
            return res;
        }

        #region ISimpleCurve Members

        double[] ISimpleCurve.GetPlaneIntersection(Plane pln)
        {
            try
            {
                GeoPoint pnt;
                if (pln.Intersect(startPoint, endPoint - startPoint, out pnt))
                {
                    return new double[] { PositionOf(pnt) };
                }
                return new double[0];
            }
            catch (ArithmeticException)
            {
                return new double[0];
            }
        }

        ExplicitPCurve3D IExplicitPCurve3D.GetExplicitPCurve3D()
        {
            return ExplicitPCurve3D.MakeLine(startPoint, endPoint - startPoint);
        }

        int IExportStep.Export(ExportStep export, bool topLevel)
        {
            // #63=LINE('',#60,#62) ;
            int p1 = (startPoint as IExportStep).Export(export, false); // unfortunately we don't know about vertices here, so we cannot use them
            int dir = ((EndPoint - StartPoint) as IExportStep).Export(export, false);
            int vec = export.WriteDefinition("VECTOR('',#" + dir.ToString() + ",1.)");
            int ln = export.WriteDefinition("LINE('',#" + p1.ToString() + ",#" + vec.ToString() + ")");
            if (topLevel)
            {   // export as a geometric curve set of a single trimmed curve
                // #37 = GEOMETRIC_CURVE_SET('',(#38));
                // #38 = TRIMMED_CURVE('',#39,(#43,PARAMETER_VALUE(0.E+000)),(#44,PARAMETER_VALUE(62.52263230373)),.T.,.PARAMETER.);
                int sp = (startPoint as IExportStep).Export(export, false);
                int ep = (endPoint as IExportStep).Export(export, false);
                int tc = export.WriteDefinition("TRIMMED_CURVE('',#" + ln.ToString() + ",(#" + sp.ToString() + ",PARAMETER_VALUE(0.0)),(#" + ep.ToString() + ",PARAMETER_VALUE("+export.ToString(Length)+")),.T.,.CARTESIAN.)");
                int gcs = export.WriteDefinition("GEOMETRIC_CURVE_SET('',(#" + tc.ToString() + "))"); // is a Representation_Item
                ColorDef cd = ColorDef;
                if (cd == null) cd = new ColorDef("Black", Color.Black);
                cd.MakeStepStyle(gcs, export);
                int product = export.WriteDefinition("PRODUCT( '','','',(#2))");
                int pdf = export.WriteDefinition("PRODUCT_DEFINITION_FORMATION_WITH_SPECIFIED_SOURCE( ' ', 'NONE', #" + product.ToString() + ", .NOT_KNOWN. )");
                int pd = export.WriteDefinition("PRODUCT_DEFINITION( 'NONE', 'NONE', #" + pdf.ToString() + ", #3 )");
                int pds = export.WriteDefinition("PRODUCT_DEFINITION_SHAPE( 'NONE', 'NONE', #" + pd.ToString() + " )");
                int sr = export.WriteDefinition("SHAPE_REPRESENTATION('', ( #" + gcs.ToString() + "), #4 )");
                export.WriteDefinition("SHAPE_DEFINITION_REPRESENTATION( #" + pds.ToString() + ", #" + sr.ToString() + ")");
                return sr;
            }
            else
            {
                return ln;
            }
        }

        #endregion
    }

    internal class QuadTreeLine : IQuadTreeInsertableZ
    {
        GeoPoint2D sp, ep;
        IGeoObject go;
        Plane plane;
        double zmax;
        public QuadTreeLine(IGeoObject go, GeoPoint sp, GeoPoint ep, Projection projection)
        {
            this.go = go;
            GeoPoint spp = projection.UnscaledProjection * sp;
            GeoPoint epp = projection.UnscaledProjection * ep;
            GeoPoint p3 = new GeoPoint(spp.x + (epp.y - spp.y), spp.y + (spp.x - epp.x));
            this.sp = spp.To2D();
            this.ep = epp.To2D();
            GeoVector normal = GeoVector.ZAxis ^ (epp - spp);
            if (Precision.IsNullVector(normal))
            {
                zmax = Math.Max(spp.z, epp.z);
            }
            else
            {
                try
                {
                    plane = new Plane(spp, epp - spp, normal);
                    zmax = double.MinValue;
                }
                catch (PlaneException)
                {
                    zmax = Math.Max(spp.z, epp.z);
                }
            }
            // folgende 3 Gleichungen bestimmen den Z Wert (wenn die Linie nicht genau in Projektionsrichtung geht
            // fx*sp.x + fy*sp.y + c = spp.z;
            // fx*ep.x + fy*ep.y + c = epp.z;
            // fx*p3.x + fy*p3.y + c = spp.z; // nur damit die Lösung eindeutig bestimmt ist
            //double[,] m = new double[3, 3];
            //m[0, 0] = this.sp.x;
            //m[0, 1] = this.sp.y;
            //m[0, 2] = 1.0;
            //m[1, 0] = this.ep.x;
            //m[1, 1] = this.ep.y;
            //m[1, 2] = 1.0;
            //m[2, 0] = p3.x;
            //m[2, 1] = p3.y;
            //m[2, 2] = 1.0;
            //double[,] b = new double[,] { { spp.z }, { epp.z }, { spp.z } };
            //LinearAlgebra.Matrix mx = new CADability.LinearAlgebra.Matrix(m);
            //try
            //{
            //    LinearAlgebra.Matrix s = mx.Solve(new LinearAlgebra.Matrix(b));
            //    fx = s[0, 0];
            //    fy = s[0, 1];
            //    c = s[0, 2];
            //}
            //catch (System.ApplicationException)
            //{   // Linie genau in Projektionsrichtung
            //    // Z-Wert ist maximum
            //    fx = 0.0;
            //    fy = 0.0;
            //    c = Math.Max(spp.z, epp.z);
            //}

        }
        #region IQuadTreeInsertableZ Members
        double IQuadTreeInsertableZ.GetZPosition(GeoPoint2D p)
        {
            if (zmax > double.MinValue) return zmax;
            double[,] m = new double[2, 2];
            m[0, 0] = plane.DirectionX.x;
            m[0, 1] = plane.DirectionY.x;
            m[1, 0] = plane.DirectionX.y;
            m[1, 1] = plane.DirectionY.y;
            double[] b = new double[] {  p.x - plane.Location.x ,  p.y - plane.Location.y  };
            Matrix mx = DenseMatrix.OfArray(m);
            Vector s = (Vector)mx.Solve(new DenseVector(b));
            if (s != null)
            {
                double l1 = s[0];
                double l2 = s[1];
                return plane.Location.z + l1 * plane.DirectionX.z + plane.DirectionY.z;
            }
            else
            {
                return zmax;
            }
        }
        #endregion

        #region IQuadTreeInsertable Members

        BoundingRect IQuadTreeInsertable.GetExtent()
        {
            return new BoundingRect(sp, ep);
        }

        bool IQuadTreeInsertable.HitTest(ref BoundingRect rect, bool includeControlPoints)
        {
            ClipRect clr = new ClipRect(rect);
            return clr.LineHitTest(sp, ep);
        }

        object IQuadTreeInsertable.ReferencedObject
        {
            get { return go; }
        }

        #endregion
    }

    internal class InfiniteLine : IOctTreeInsertable
    {
        private GeoPoint start;
        private GeoVector dir;
        public InfiniteLine(GeoPoint start, GeoVector dir)
        {
            this.start = start;
            this.dir = dir;
        }

        #region IOctTreeInsertable Members

        BoundingCube IOctTreeInsertable.GetExtent(double precision)
        {
            return BoundingCube.InfiniteBoundingCube;
        }

        bool IOctTreeInsertable.HitTest(ref BoundingCube cube, double precision)
        {
            return cube.Interferes(start, dir, precision, false);
        }

        bool IOctTreeInsertable.HitTest(Projection projection, BoundingRect rect, bool onlyInside)
        {
            if (onlyInside)
            {
                return false;
            }
            else
            {
                ClipRect clr = new ClipRect(rect);
                // eine Linie machen, die bestimmt über das rechteck hinaus geht
                GeoPoint2D sp2d = projection.ProjectUnscaled(start);
                GeoVector2D dir2d = projection.ProjectUnscaled(dir);
                double max = 0.0;
                max = Math.Max(max, sp2d | rect.GetLowerLeft());
                max = Math.Max(max, sp2d | rect.GetLowerRight());
                max = Math.Max(max, sp2d | rect.GetUpperLeft());
                max = Math.Max(max, sp2d | rect.GetUpperRight());
                GeoPoint2D ep2d = sp2d + max * dir2d;
                sp2d = sp2d - max * dir2d; // in beide Richtungen
                return clr.LineHitTest(sp2d, ep2d);
            }
        }

        double IOctTreeInsertable.Position(GeoPoint fromHere, GeoVector direction, double precision)
        {   // wird (hoffentlich) in diesem Zusammenhang nicht verwendet
            return double.MinValue;
        }

        bool IOctTreeInsertable.HitTest(Projection.PickArea area, bool onlyInside)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        #endregion
    }

    internal class InfiniteRay : IOctTreeInsertable, ISimpleCurve
    {
        private GeoPoint start;
        private GeoVector dir;
        public InfiniteRay(GeoPoint start, GeoVector dir)
        {
            this.start = start;
            this.dir = dir;
        }

        #region IOctTreeInsertable Members

        BoundingCube IOctTreeInsertable.GetExtent(double precision)
        {
            BoundingCube bc = new BoundingCube(start);
            GeoPoint p = new GeoPoint();
            if (dir.x > 0) p.x = double.MaxValue;
            else p.x = double.MinValue;
            if (dir.y > 0) p.y = double.MaxValue;
            else p.y = double.MinValue;
            if (dir.z > 0) p.z = double.MaxValue;
            else p.z = double.MinValue;
            bc.MinMax(p);
            return bc;
        }

        bool IOctTreeInsertable.HitTest(ref BoundingCube cube, double precision)
        {
            return cube.Interferes(start, dir, precision, true);
        }

        bool IOctTreeInsertable.HitTest(Projection projection, BoundingRect rect, bool onlyInside)
        {   // kommt dran bei GetObjectsCloseTo
            if (onlyInside)
            {
                return false;
            }
            else
            {
                ClipRect clr = new ClipRect(rect);
                // eine Linie machen, die bestimmt über das rechteck hinaus geht
                GeoPoint2D sp2d = projection.ProjectUnscaled(start);
                GeoVector2D dir2d = projection.ProjectUnscaled(dir);
                double max = 0.0;
                max = Math.Max(max, sp2d | rect.GetLowerLeft());
                max = Math.Max(max, sp2d | rect.GetLowerRight());
                max = Math.Max(max, sp2d | rect.GetUpperLeft());
                max = Math.Max(max, sp2d | rect.GetUpperRight());
                GeoPoint2D ep2d = sp2d + max * dir2d;
                return clr.LineHitTest(sp2d, ep2d);
            }
        }

        double IOctTreeInsertable.Position(GeoPoint fromHere, GeoVector direction, double precision)
        {   // wird (hoffentlich) in diesem Zusammenhang nicht verwendet
            return double.MinValue;
        }

        bool IOctTreeInsertable.HitTest(Projection.PickArea area, bool onlyInside)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        #endregion

        #region ISimpleCurve Members

        double[] ISimpleCurve.GetPlaneIntersection(Plane pln)
        {
            try
            {
                GeoPoint p = pln.Intersect(start, dir);
                return new double[] { Geometry.LinePar(start, dir, p) };
            }
            catch (DivideByZeroException)
            {
                return new double[0];
            }
        }

        #endregion
    }

    internal class OctTreeLine : IOctTreeInsertable
    {
        private GeoPoint startPoint, endPoint;
        public OctTreeLine(GeoPoint startPoint, GeoPoint endPoint)
        {
            this.startPoint = startPoint;
            this.endPoint = endPoint;
        }

        #region IOctTreeInsertable Members

        BoundingCube IOctTreeInsertable.GetExtent(double precision)
        {
            return new BoundingCube(startPoint, endPoint);
        }

        bool IOctTreeInsertable.HitTest(ref BoundingCube cube, double precision)
        {
            return cube.Interferes(ref startPoint, ref endPoint);
        }

        bool IOctTreeInsertable.HitTest(Projection projection, BoundingRect rect, bool onlyInside)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        bool IOctTreeInsertable.HitTest(Projection.PickArea area, bool onlyInside)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        double IOctTreeInsertable.Position(GeoPoint fromHere, GeoVector direction, double precision)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        #endregion
    }

    internal class OctTreeTriangle : IOctTreeInsertable
    {
        GeoPoint p1, p2, p3;
        public OctTreeTriangle(GeoPoint p1, GeoPoint p2, GeoPoint p3)
        {
            this.p1 = p1;
            this.p2 = p2;
            this.p3 = p3;
        }

        #region IOctTreeInsertable Members

        BoundingCube IOctTreeInsertable.GetExtent(double precision)
        {
            return new BoundingCube(p1, p2, p3);
        }

        bool IOctTreeInsertable.HitTest(ref BoundingCube cube, double precision)
        {
            return cube.Interferes(ref p1, ref p2, ref p3);
        }

        bool IOctTreeInsertable.HitTest(Projection projection, BoundingRect rect, bool onlyInside)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        bool IOctTreeInsertable.HitTest(Projection.PickArea area, bool onlyInside)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        double IOctTreeInsertable.Position(GeoPoint fromHere, GeoVector direction, double precision)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        #endregion
    }

    internal class OctTreeTetraeder : IOctTreeInsertable
    {
        GeoPoint p1, p2, p3, p4;
        public OctTreeTetraeder(GeoPoint p1, GeoPoint p2, GeoPoint p3, GeoPoint p4)
        {
            this.p1 = p1;
            this.p2 = p2;
            this.p3 = p3;
            this.p4 = p4;
        }

        #region IOctTreeInsertable Members

        BoundingCube IOctTreeInsertable.GetExtent(double precision)
        {
            return new BoundingCube(p1, p2, p3, p4);
        }

        bool IOctTreeInsertable.HitTest(ref BoundingCube cube, double precision)
        {
            return cube.Interferes(p1, p2, p3, p4);
        }

        bool IOctTreeInsertable.HitTest(Projection projection, BoundingRect rect, bool onlyInside)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        bool IOctTreeInsertable.HitTest(Projection.PickArea area, bool onlyInside)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        double IOctTreeInsertable.Position(GeoPoint fromHere, GeoVector direction, double precision)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        #endregion
    }
}
