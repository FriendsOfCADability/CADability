using CADability.Attribute;
using CADability.Curve2D;
using CADability.UserInterface;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CADability.GeoObject
{

    public class PolylineException : ApplicationException
    {
        public enum PolylineExceptionType { General, NoPoints, NoRectangle, NoParallelogram, RectangleSameDirections };
        public PolylineExceptionType ExceptionType;
        public PolylineException(string message, PolylineExceptionType tp)
            : base(message)
        {

            ExceptionType = tp;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    [Serializable()]
    public class Polyline : IGeoObjectImpl, IColorDef, ILineWidth, ILinePattern, ICurve,
            ISerializable, IExtentedableCurve, IJsonSerialize, IExportStep
    {
        private ColorDef colorDef; // die Farbe. 
        private GeoPoint[] vertex; // die Eckpunkte
        private bool closed; // geschlossen oder nicht
        private PlanarState planarState;
        private Plane plane; // nur gültig wenn planarState == Planar
        #region polymorph construction
        public delegate Polyline ConstructionDelegate();
        public static ConstructionDelegate Constructor;
        public static Polyline Construct()
        {
            if (Constructor != null) return Constructor();
            return new Polyline();
        }
        public delegate void ConstructedDelegate(Polyline justConstructed);
        public static event ConstructedDelegate Constructed;
        #endregion
        public static Polyline FromPoints(GeoPoint[] pnts, bool closed = false)
        {
            Polyline res = Polyline.Construct();
            res.SetPoints(pnts, closed);
            return res;
        }
        protected Polyline()
            : base()
        {
            planarState = PlanarState.Unknown;
            if (Constructed != null) Constructed(this);
        }
        #region allgemeine Manipulation
        public void AddPoint(GeoPoint p)
        {
            int pos = 0;
            if (vertex != null) pos = vertex.Length;
            using (new Changing(this, "RemovePoint", pos))
            {
                // planarState = PlanarState.Unknown; Ebene bleibt erhalten
                planarState = PlanarState.Unknown;
                if (vertex == null)
                {
                    vertex = new GeoPoint[1];
                    vertex[0] = p;
                }
                else
                {
                    GeoPoint[] newvertex = new GeoPoint[vertex.Length + 1];
                    vertex.CopyTo(newvertex, 0);
                    newvertex[vertex.Length] = p;
                    vertex = newvertex;
                }
            }
        }
        public void SetPoint(int Index, GeoPoint p)
        {
            using (new Changing(this, "SetPoint", Index, vertex[Index]))
            {
                planarState = PlanarState.Unknown;
                vertex[Index] = p;
            }
        }
        public void RemovePoint(int Index)
        {
            using (new Changing(this, "InsertPoint", Index, vertex[Index]))
            {
                planarState = PlanarState.Unknown;
                GeoPoint[] newvertex = new GeoPoint[vertex.Length - 1];
                for (int i = 0; i < Index; ++i) newvertex[i] = vertex[i];
                for (int i = Index + 1; i < vertex.Length; ++i) newvertex[i - 1] = vertex[i];
                vertex = newvertex;
            }
        }
        public void InsertPoint(int Index, GeoPoint p)
        {
            using (new Changing(this, "RemovePoint", Index))
            {
                planarState = PlanarState.Unknown;
                GeoPoint[] newvertex = new GeoPoint[vertex.Length + 1];
                for (int i = 0; i < Index; ++i) newvertex[i] = vertex[i];
                for (int i = Index + 1; i <= vertex.Length; ++i) newvertex[i] = vertex[i - 1];
                newvertex[Index] = p;
                vertex = newvertex;
            }
        }
        public void InsertPoint(double position)
        {
            GeoPoint p = PointAt(position);
            int c = vertex.Length;
            if (closed) ++c;
            position *= (c - 1); // auf 0..n normiert
            int i = (int)Math.Floor(position) + 1;
            using (new Changing(this, "RemovePoint", i))
            {
                planarState = PlanarState.Unknown;
                List<GeoPoint> tmp = new List<GeoPoint>(vertex);
                tmp.Insert(i, p);
                vertex = tmp.ToArray();
            }
        }
        internal void CyclicalPermutation(GeoPoint pointOnSegment, bool setStartSegment)
        {
            if (!IsClosed) return;
            int n = (int)Math.Floor(PositionOf(pointOnSegment) * vertex.Length);
            if (!setStartSegment)
            {   // wenn dieses Segment das Endsegment sein soll, dann ist das nächste das Startsegment
                ++n;
                if (n >= vertex.Length) n = 0;
            }
            GeoPoint[] newVertex = new GeoPoint[vertex.Length];
            Array.Copy(vertex, n, newVertex, 0, vertex.Length - n);
            Array.Copy(vertex, 0, newVertex, vertex.Length - n, n);
            using (new Changing(this))
            {
                planarState = PlanarState.Unknown;
                vertex = newVertex;
            }
        }
        /// <summary>
        /// Let the closed polyline start with the vertex with the specified index. The vertex array will be rotated
        /// </summary>
        /// <param name="startIndex">Index of vertex with which to start</param>
        /// <returns>true on success</returns>
        public bool SetClosedPolylineStartIndex(int startIndex)
        {
            if (!IsClosed) return false;
            if (startIndex < 0 || startIndex >= vertex.Length) return false;
            int n = startIndex;
            GeoPoint[] newVertex = new GeoPoint[vertex.Length];
            Array.Copy(vertex, n, newVertex, 0, vertex.Length - n);
            Array.Copy(vertex, 0, newVertex, vertex.Length - n, n);
            using (new Changing(this))
            {
                planarState = PlanarState.Unknown;
                vertex = newVertex;
            }
            return true;
        }
        public GeoPoint GetPoint(int Index)
        {
            return vertex[Index];
        }
        public int PointCount
        {
            get
            {
                if (vertex == null) return 0;
                else return vertex.Length;
            }
        }
        public void SetPoints(GeoPoint[] points, bool closed)
        {
            using (new Changing(this, "SetPoints", vertex, this.closed))
            {
                planarState = PlanarState.Unknown;
                List<GeoPoint> al = new List<GeoPoint>(points.Length);
                for (int i = 0; i < points.Length; ++i)
                {
                    if (i > 0 && Precision.IsEqual(points[i - 1], points[i])) continue;
                    al.Add(points[i]);
                }
                if (al.Count < 2) throw new PolylineException("Setting a polyline with less than two different points", PolylineException.PolylineExceptionType.NoPoints);
                if (closed && al[al.Count - 1] == al[0]) al.RemoveAt(al.Count - 1);
                vertex = al.ToArray();
                this.closed = closed;
            }
        }
        public bool TrySetPoints(GeoPoint[] points, bool closed)
        {
            using (new Changing(this, "SetPoints", vertex, this.closed))
            {
                planarState = PlanarState.Unknown;
                ArrayList al = new ArrayList(points.Length);
                for (int i = 0; i < points.Length; ++i)
                {
                    if (i > 0 && Precision.IsEqual(points[i - 1], points[i])) continue;
                    al.Add(points[i]);
                }
                if (al.Count < 2) return false;
                vertex = (GeoPoint[])al.ToArray(typeof(GeoPoint));
                this.closed = closed;
                return true;
            }
        }
        public bool IsParallelogram
        {
            get
            {
                if (vertex == null) return false;
                if (vertex.Length != 4) return false;
                if (!closed) return false;
                if (!Precision.SameDirection(vertex[1] - vertex[0], vertex[2] - vertex[3], false)) return false;
                if (!Precision.SameDirection(vertex[3] - vertex[0], vertex[2] - vertex[1], false)) return false;
                // dürfen auch nicht colinear sein!
                if (Precision.SameDirection(vertex[3] - vertex[0], vertex[1] - vertex[0], false)) return false;
                return true;
            }
        }
        public bool IsRectangle
        {
            get
            {
                if (!IsParallelogram) return false;
                if (!Precision.IsPerpendicular(vertex[3] - vertex[0], vertex[1] - vertex[0], false)) return false;
                return true;
            }
        }
        public void SetRectangle(GeoPoint location, GeoVector directionX, GeoVector directionY)
        {
            // kein Changing nötig, da "SetPoints" aufgerufen wird
            if (Precision.SameDirection(directionX, directionY, false)) throw new PolylineException("Setting a rectangle with two identical directions", PolylineException.PolylineExceptionType.RectangleSameDirections);
            GeoPoint[] points = new GeoPoint[4];
            if (!Precision.IsPerpendicular(directionX, directionY, false))
            {	// geradebiegen falls nicht rechtwinklig!
                GeoVector norm = directionX ^ directionY;
                GeoVector dy = norm ^ directionX;
                dy.Length = directionY.Length;
                directionY = dy;
            }
            points[0] = location;
            points[1] = location + directionX;
            points[2] = location + directionX + directionY;
            points[3] = location + directionY;
            SetPoints(points, true);
        }
        public double RectangleWidth
        {
            get
            {
                // Vorzeichen macht keinen Sinn, da das Rechteck seine eigene Ebene
                // definiert, in der es immer richtig orientiert ist
                if (!IsRectangle) throw new PolylineException("Attempt to get rectangle width on polyline that is no rectangle", PolylineException.PolylineExceptionType.NoRectangle);
                return Geometry.Dist(vertex[1], vertex[0]);
            }
            set
            {
                if (!IsRectangle) throw new PolylineException("Attempt to set rectangle width on polyline that is no rectangle", PolylineException.PolylineExceptionType.NoRectangle);
                if (Math.Abs(value) < Precision.eps) throw new PolylineException("Attempt to set rectangle width to 0.0", PolylineException.PolylineExceptionType.General);
                GeoVector dirx = vertex[1] - vertex[0];
                GeoVector diry = vertex[3] - vertex[0];
                dirx.Length = value;
                SetRectangle(vertex[0], dirx, diry);
            }
        }
        public double RectangleHeight
        {
            get
            {
                // Vorzeichen macht keinen Sinn, da das Rechteck seine eigene Ebene
                // definiert, in der es immer richtig orientiert ist
                if (!IsRectangle) throw new PolylineException("Attempt to get rectangle heigth on polyline that is no rectangle", PolylineException.PolylineExceptionType.NoRectangle);
                return Geometry.Dist(vertex[3], vertex[0]);
            }
            set
            {
                if (!IsRectangle) throw new PolylineException("Attempt to set rectangle heigth on polyline that is no rectangle", PolylineException.PolylineExceptionType.NoRectangle);
                GeoVector dirx = vertex[1] - vertex[0];
                GeoVector diry = vertex[3] - vertex[0];
                diry.Length = value;
                SetRectangle(vertex[0], dirx, diry);
            }
        }
        public GeoPoint RectangleLocation
        {
            get
            {
                if (!IsRectangle) throw new PolylineException("Attempt to get rectangle location on polyline that is no rectangle", PolylineException.PolylineExceptionType.NoRectangle);
                return vertex[0];
            }
            set
            {
                if (!IsRectangle) throw new PolylineException("Attempt to set rectangle location on polyline that is no rectangle", PolylineException.PolylineExceptionType.NoRectangle);
                ModOp m = ModOp.Translate(value - vertex[0]);
                this.Modify(m);
            }
        }
        public void SetParallelogram(GeoPoint location, GeoVector directionX, GeoVector directionY)
        {
            GeoPoint[] points = new GeoPoint[4];
            points[0] = location;
            points[1] = location + directionX;
            points[2] = location + directionX + directionY;
            points[3] = location + directionY;
            SetPoints(points, true);
        }
        public double ParallelogramWidth
        {
            get
            {
                // Vorzeichen macht keinen Sinn, da das Rechteck seine eigene Ebene
                // definiert, in der es immer richtig orientiert ist
                if (!IsParallelogram) throw new PolylineException("Attempt to gset parallelogram width on polyline that is no Parallelogram", PolylineException.PolylineExceptionType.NoParallelogram);
                return Geometry.Dist(vertex[1], vertex[0]);
            }
            set
            {
                if (!IsParallelogram) throw new PolylineException("Attempt to set parallelogram width on polyline that is no Parallelogram", PolylineException.PolylineExceptionType.NoParallelogram);
                GeoVector dirx = vertex[1] - vertex[0];
                GeoVector diry = vertex[3] - vertex[0];
                dirx.Length = value;
                SetParallelogram(vertex[0], dirx, diry);
            }
        }
        public double ParallelogramHeight
        {
            get
            {
                // Vorzeichen macht keinen Sinn, da das Rechteck seine eigene Ebene
                // definiert, in der es immer richtig orientiert ist
                if (!IsParallelogram) throw new PolylineException("Attempt to get Parallelogram heigth on polyline that is no Parallelogram", PolylineException.PolylineExceptionType.NoParallelogram);
                return Geometry.Dist(vertex[3], vertex[0]);
            }
            set
            {
                if (!IsParallelogram) throw new PolylineException("Attempt to set Parallelogram heigth on polyline that is no Parallelogram", PolylineException.PolylineExceptionType.NoParallelogram);
                GeoVector dirx = vertex[1] - vertex[0];
                GeoVector diry = vertex[3] - vertex[0];
                diry.Length = value;
                SetParallelogram(vertex[0], dirx, diry);
            }
        }
        public GeoPoint ParallelogramLocation
        {
            get
            {
                if (!IsParallelogram) throw new PolylineException("Attempt to get Parallelogram location on polyline that is no Parallelogram", PolylineException.PolylineExceptionType.NoParallelogram);
                return vertex[0];
            }
            set
            {
                if (!IsParallelogram) throw new PolylineException("Attempt to set Parallelogram location on polyline that is no Parallelogram", PolylineException.PolylineExceptionType.NoParallelogram);
                ModOp m = ModOp.Translate(value - vertex[0]);
                this.Modify(m);
            }
        }
        public GeoVector ParallelogramMainDirection
        {
            get
            {
                if (!IsParallelogram) throw new PolylineException("Attempt to get Parallelogram location on polyline that is no Parallelogram", PolylineException.PolylineExceptionType.NoParallelogram);
                return vertex[1] - vertex[0];
            }
            set
            {
                if (!IsParallelogram) throw new PolylineException("Attempt to set Parallelogram location on polyline that is no Parallelogram", PolylineException.PolylineExceptionType.NoParallelogram);
                Plane pln = new Plane(vertex[0], vertex[1] - vertex[0], vertex[3] - vertex[0]);
                // Basislinie ist x-Richtung
                GeoVector2D v = pln.Project(value);
                SweepAngle sw = new SweepAngle(GeoVector2D.XAxis, v);
                ModOp m = ModOp.Rotate(vertex[0], pln.Normal, sw);
                using (new Changing(this))
                {   // ohne das umgebende Changing wird bei Mehrfachänderung nur das erste modify gemerkt und somit auch nur das erste Modify rückgängig gemacht
                    this.Modify(m);
                }
            }
        }
        public GeoVector ParallelogramSecondaryDirection
        {
            get
            {
                if (!IsParallelogram) throw new PolylineException("Attempt to get Parallelogram location on polyline that is no Parallelogram", PolylineException.PolylineExceptionType.NoParallelogram);
                return vertex[3] - vertex[0];
            }
            set
            {
                if (!IsParallelogram) throw new PolylineException("Attempt to set Parallelogram location on polyline that is no Parallelogram", PolylineException.PolylineExceptionType.NoParallelogram);
                this.SetParallelogram(vertex[0], vertex[1] - vertex[0], value);
            }
        }
        public SweepAngle ParallelogramAngle
        {
            get
            {
                if (!IsParallelogram) throw new PolylineException("Attempt to get Parallelogram angle on polyline that is no Parallelogram", PolylineException.PolylineExceptionType.NoParallelogram);
                return new SweepAngle(vertex[1] - vertex[0], vertex[3] - vertex[0]);
            }
            set
            {
                if (!IsParallelogram) throw new PolylineException("Attempt to set Parallelogram angle on polyline that is no Parallelogram", PolylineException.PolylineExceptionType.NoParallelogram);
                if (Precision.IsNull(value)) throw new PolylineException("Sweep angle 0.0 not allowed for parallelograms", PolylineException.PolylineExceptionType.General);
                GeoVector dirx = vertex[1] - vertex[0];
                GeoVector diry = vertex[3] - vertex[0];
                GeoVector norm = dirx ^ diry;
                ModOp m = ModOp.Rotate(vertex[0], norm, value);
                GeoVector y = m * dirx;
                y.Length = diry.Length;
                SetParallelogram(vertex[0], dirx, y);
            }
        }
        public void RemoveDoublePoints()
        {
            bool modified = false;
            List<GeoPoint> v = new List<GeoPoint>(vertex);
            for (int i = 1; i < v.Count; ++i)
            {
                if (Precision.IsEqual(v[i], v[i - 1]))
                {
                    v.RemoveAt(i);
                    --i; // zum Ausgleich
                    modified = true;
                }
            }
            if (modified)
            {
                using (new Changing(this, false))
                {
                    planarState = PlanarState.Unknown;
                    vertex = v.ToArray();
                }
            }
        }
        public GeoPoint[] Vertices
        {
            get
            {
                return (GeoPoint[])vertex.Clone();
            }
        }
        public BSpline ApproximateByBSpline(double precision)
        {
            BSpline res = BSpline.Construct();
            res.Approximate(vertex, precision);
            return res;
        }
        #endregion
        #region IGeoObject
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.AttributeChanged (INamedAttribute)"/>
        /// </summary>
        /// <param name="attribute"></param>
        /// <returns></returns>
        public override bool AttributeChanged(INamedAttribute attribute)
        {
            if (attribute == colorDef || attribute == Layer)
            {
                using (new Changing(this, true, true, "AttributeChanged", attribute))
                {
                    planarState = PlanarState.Unknown;
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.CopyGeometry (IGeoObject)"/>
        /// </summary>
        /// <param name="ToCopyFrom"></param>
        public override void CopyGeometry(IGeoObject ToCopyFrom)
        {
            using (new Changing(this))
            {
                planarState = PlanarState.Unknown;
                Polyline cpy = ToCopyFrom as Polyline;
                this.vertex = (GeoPoint[])cpy.vertex.Clone();
                this.closed = cpy.closed;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Modify (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        public override void Modify(ModOp m)
        {
            using (new Changing(this, "ModifyInverse", m))
            {
                planarState = PlanarState.Unknown;
                for (int i = 0; i < vertex.Length; ++i) vertex[i] = m * vertex[i];
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Clone ()"/>
        /// </summary>
        /// <returns></returns>
        public override IGeoObject Clone()
        {
            Polyline res = Construct();
            res.vertex = (GeoPoint[])vertex.Clone();
            res.closed = closed;
            res.ColorDef = colorDef;
            res.CopyAttributes(this);
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.FindSnapPoint (SnapPointFinder)"/>
        /// </summary>
        /// <param name="spf"></param>
        public override void FindSnapPoint(SnapPointFinder spf)
        {
            if (!spf.Accept(this)) return;
            GeoPoint startPoint = new GeoPoint(0.0, 0.0), endPoint;
            for (int i = 0; i < vertex.Length; ++i)
            {	// inhaltlich aus der Linie abgeschrieben
                endPoint = vertex[i];
                if (i == 0) startPoint = vertex[vertex.Length - 1];
                bool isLine = i > 0 || closed;
                // man müsste hier abchecken, auf welcher Linie der Cursor gerade steht, und 
                // nur diese Linie als Fußpunkt, Mittelpunkt u.s.w. zulassen
                if (spf.SnapToObjectCenter && isLine)
                {
                    GeoPoint Center = new GeoPoint(startPoint, endPoint);
                    spf.Check(Center, this, SnapPointFinder.DidSnapModes.DidSnapToObjectCenter);
                }
                if (spf.SnapToObjectSnapPoint)
                {
                    spf.Check(startPoint, this, SnapPointFinder.DidSnapModes.DidSnapToObjectSnapPoint);
                    spf.Check(endPoint, this, SnapPointFinder.DidSnapModes.DidSnapToObjectSnapPoint);
                }
                if (spf.SnapToDropPoint && spf.BasePointValid && isLine)
                {
                    GeoPoint toTest = Geometry.DropPL(spf.BasePoint, startPoint, endPoint);
                    spf.Check(toTest, this, SnapPointFinder.DidSnapModes.DidSnapToDropPoint);
                }
                if (spf.SnapToObjectPoint && isLine)
                {
                    double par = PositionOf(spf.SourcePoint3D);
                    if (par >= 0.0 && par <= 1.0)
                    {
                        spf.Check(PointAt(par), this, SnapPointFinder.DidSnapModes.DidSnapToObjectPoint);
                    }
                }
                startPoint = endPoint;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetShowProperties (IFrame)"/>
        /// </summary>
        /// <param name="Frame"></param>
        /// <returns></returns>
        public override IPropertyEntry GetShowProperties(IFrame Frame)
        {
            return new ShowPropertyPolyline(this, Frame);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetBoundingCube ()"/>
        /// </summary>
        /// <returns></returns>
        public override BoundingCube GetBoundingCube()
        {
            BoundingCube res = BoundingCube.EmptyBoundingCube;
            if (vertex != null)
            {
                for (int i = 0; i < vertex.Length; ++i)
                {
                    res.MinMax(vertex[i]);
                }
            }
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.HasValidData ()"/>
        /// </summary>
        /// <returns></returns>
        public override bool HasValidData()
        {
            if (vertex != null && vertex.Length > 1)
            {
                for (int i = 1; i < vertex.Length; ++i)
                {
                    // wenigstens ein Punkt muss vom Anfangspunkt verschieden sein
                    if (!Precision.IsEqual(vertex[0], vertex[i])) return true;
                }
            }
            return false;
        }
        public delegate bool PaintTo3DDelegate(Polyline toPaint, IPaintTo3D paintTo3D);
        public static PaintTo3DDelegate OnPaintTo3D;
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PaintTo3D (IPaintTo3D)"/>
        /// </summary>
        /// <param name="paintTo3D"></param>
        public override void PaintTo3D(IPaintTo3D paintTo3D)
        {
            if (vertex == null) return;
            if (vertex.Length < 2) return;
            if (OnPaintTo3D != null && OnPaintTo3D(this, paintTo3D)) return;
            if (paintTo3D.SelectMode)
            {
                // paintTo3D.SetColor(paintTo3D.SelectColor);
            }
            else
            {
                if (colorDef != null) paintTo3D.SetColor(colorDef.Color);
            }
            if (lineWidth != null) paintTo3D.SetLineWidth(lineWidth);
            if (linePattern != null) paintTo3D.SetLinePattern(linePattern);
            else paintTo3D.SetLinePattern(null);
            if (IsClosed)
            {
                GeoPoint[] v = new GeoPoint[vertex.Length + 1];
                Array.Copy(vertex, v, vertex.Length);
                v[vertex.Length] = vertex[0];
                paintTo3D.Polyline(v);
            }
            else
            {
                paintTo3D.Polyline(vertex);
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PrepareDisplayList (double)"/>
        /// </summary>
        /// <param name="precision"></param>
        public override void PrepareDisplayList(double precision)
        {
            // nichts zu tun
        }
        public override Style.EDefaultFor PreferredStyle
        {
            get
            {
                return Style.EDefaultFor.Curves;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetQuadTreeItem (Projection, ExtentPrecision)"/>
        /// </summary>
        /// <param name="projection"></param>
        /// <param name="extentPrecision"></param>
        /// <returns></returns>
        public override IQuadTreeInsertableZ GetQuadTreeItem(Projection projection, ExtentPrecision extentPrecision)
        {
            // im planar Fall ginge es besser, aber dazu müsste die Ebene gecacht sein
            QuadTreeCollection res = new QuadTreeCollection(this, projection);
            for (int i = 1; i < vertex.Length; ++i)
            {
                res.Add(new QuadTreeLine(this, vertex[i - 1], vertex[i], projection));
            }
            if (IsClosed)
            {
                res.Add(new QuadTreeLine(this, vertex[vertex.Length - 1], vertex[0], projection));
            }
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Decompose ()"/>
        /// </summary>
        /// <returns></returns>
        public override GeoObjectList Decompose()
        {
            GeoObjectList decomposedList = new GeoObjectList();
            for (int i = 1; i < this.PointCount; ++i)
            {
                Line line = Line.Construct();
                line.StartPoint = this.GetPoint(i - 1);
                line.EndPoint = this.GetPoint(i);
                line.CopyAttributes(this);
                decomposedList.Add(line);
            }
            if (this.IsClosed && this.PointCount > 2)
            {
                Line line = Line.Construct();
                line.StartPoint = this.GetPoint(this.PointCount - 1);
                line.EndPoint = this.GetPoint(0);
                line.CopyAttributes(this);
                decomposedList.Add(line);
            }
            return decomposedList;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetExtent (Projection, ExtentPrecision)"/>
        /// </summary>
        /// <param name="projection"></param>
        /// <param name="extentPrecision"></param>
        /// <returns></returns>
        public override BoundingRect GetExtent(Projection projection, ExtentPrecision extentPrecision)
        {
            BoundingRect res = BoundingRect.EmptyBoundingRect;
            for (int i = 0; i < vertex.Length; ++i)
            {
                res.MinMax(projection.ProjectUnscaled(vertex[i]));
            }
            return res;
        }
        #endregion
        #region IOctTreeInsertable members
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetExtent (double)"/>
        /// </summary>
        /// <param name="precision"></param>
        /// <returns></returns>
        public override BoundingCube GetExtent(double precision)
        {
            return GetBoundingCube();
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.HitTest (ref BoundingCube, double)"/>
        /// </summary>
        /// <param name="cube"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public override bool HitTest(ref BoundingCube cube, double precision)
        {
            for (int i = 0; i < vertex.Length - 1; ++i)
            {
                if (cube.Interferes(ref vertex[i], ref vertex[i + 1])) return true;
            }
            if (closed)
            {
                if (cube.Interferes(ref vertex[0], ref vertex[vertex.Length - 1])) return true;
            }
            return false;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.HitTest (Projection, BoundingRect, bool)"/>
        /// </summary>
        /// <param name="projection"></param>
        /// <param name="rect"></param>
        /// <param name="onlyInside"></param>
        /// <returns></returns>
        public override bool HitTest(Projection projection, BoundingRect rect, bool onlyInside)
        {
            ICurve2D c2d = (this as ICurve).GetProjectedCurve(projection.ProjectionPlane);
            if (c2d == null) return false;
            if (onlyInside) return c2d.GetExtent() <= rect;
            else return c2d.HitTest(ref rect, false);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.HitTest (Projection.PickArea, bool)"/>
        /// </summary>
        /// <param name="area"></param>
        /// <param name="onlyInside"></param>
        /// <returns></returns>
        public override bool HitTest(Projection.PickArea area, bool onlyInside)
        {
            if (onlyInside)
            {
                for (int i = 0; i < vertex.Length; ++i)
                {
                    if (!BoundingCube.UnitBoundingCube.Contains(area.ToUnitBox * vertex[i])) return false;
                }
                return true;
            }
            else
            {
                GeoPoint lastPoint = area.ToUnitBox * vertex[0];
                GeoPoint firstPoint = lastPoint;
                for (int i = 1; i < vertex.Length; ++i)
                {
                    GeoPoint thisPoint = area.ToUnitBox * vertex[i];
                    if (BoundingCube.UnitBoundingCube.Interferes(ref lastPoint, ref thisPoint)) return true;
                    lastPoint = thisPoint;
                }
                if (closed && BoundingCube.UnitBoundingCube.Interferes(ref lastPoint, ref firstPoint)) return true;
                return false;
            }
        }
		/// <summary>
		/// Computes the parameter value along the given ray (fromHere + t * direction)
		/// where it intersects the polyline (if any). Returns <c>double.MaxValue</c>
		/// if no intersection is found or if the polyline is planar and the ray is parallel to the plane.
		/// </summary>
		/// <param name="fromHere">Start point of the ray.</param>
		/// <param name="direction">Direction of the ray.</param>
		/// <param name="precision">Tolerance for intersection tests (not used here).</param>
		/// <returns>The parameter t where the intersection occurs, or <c>double.MaxValue</c> if no hit.</returns>
		public override double Position(GeoPoint fromHere, GeoVector direction, double precision)
		{
			// Check if the polyline lies in a single plane
			if ((this as ICurve).GetPlanarState() == PlanarState.Planar)
			{
				try
				{
					// Attempt to intersect with the plane
					GeoPoint p = GetPlane().Intersect(fromHere, direction);
					// Return parameter t on the ray for the intersection point
					return Geometry.LinePar(fromHere, direction, p);
				}
				catch (PlaneException)
				{
					// Ray is parallel to the plane → no intersection
					return double.MaxValue;
				}
			}

			// Not planar: manually test each segment for intersection
			double res = double.MaxValue;
			for (int i = 0; i < vertex.Length - 1; ++i)
			{
				Geometry.DistLL(vertex[i], vertex[i + 1] - vertex[i], fromHere, direction, out double pos1, out double pos2);
				if (pos1 >= 0.0 && pos1 <= 1.0 && pos2 < res) 
					res = pos2;
			}

			// If closed, check the last-to-first segment
			if (closed)
			{
				Geometry.DistLL(vertex[vertex.Length - 1], vertex[0] - vertex[vertex.Length - 1], fromHere, direction, out double pos1, out double pos2);
				if (pos1 >= 0.0 && pos1 <= 1.0 && pos2 < res) 
					res = pos2;
			}

			return res;
		}

		#endregion
		#region IColorDef Members
		public ColorDef ColorDef
        {
            get
            {
                return colorDef;
            }
            set
            {
                SetColorDef(ref colorDef, value);
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
        #region ICurve Members
        public GeoPoint StartPoint
        {
            get
            {
                return vertex[0];
            }
            set
            {
                SetPoint(0, value);
            }
        }
        public GeoPoint EndPoint
        {
            get
            {
                if (IsClosed) return vertex[0];
                else return vertex[vertex.Length - 1];
            }
            set
            {
                if (IsClosed) SetPoint(0, value);
                else SetPoint(vertex.Length - 1, value);
            }
        }
        public GeoVector StartDirection
        {
            get
            {
                if (vertex == null || vertex.Length < 2) throw new PolylineException("StartDirection: NoPoints", PolylineException.PolylineExceptionType.NoPoints);
                return new GeoVector(vertex[0], vertex[1]);
            }
        }
        public GeoVector EndDirection
        {
            get
            {
                if (vertex == null || vertex.Length < 2) throw new PolylineException("EndDirection: NoPoints", PolylineException.PolylineExceptionType.NoPoints);
                if (IsClosed) return new GeoVector(vertex[vertex.Length - 1], vertex[0]);
                else return new GeoVector(vertex[vertex.Length - 2], vertex[vertex.Length - 1]);
            }
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.DirectionAt (double)"/>
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public GeoVector DirectionAt(double Position)
        {	// TODO: noch skalierung des Parameters beachten
            int c = vertex.Length;
            if (closed) ++c;
            int i, iend;
            Position *= (c - 1); // auf 0..n normiert
            if (Position < 0.0)
            {	// Punkt vor dem Anfang
                i = 0; iend = 1;
            }
            else if (Position > c - 1)
            {	// Punkt nach dem Ende
                i = c - 2;
                iend = c - 1;
            }
            else
            {
                i = (int)Math.Floor(Position);
                iend = i + 1;
            }
            if (iend >= vertex.Length) iend = 0;
            if (i >= vertex.Length) i = vertex.Length - 1;
            GeoVector dir = new GeoVector(vertex[i], vertex[iend]);

            //double LastPositiv = Position;
            //int i = 0;
            //for (i=1; i<vertex.Length; ++i)
            //{
            //    LastPositiv = Position;
            //    Position -= Geometry.Dist(vertex[i],vertex[i-1]);
            //    if (Position<0.0) break;
            //}
            //GeoVector dir = new GeoVector(vertex[i-1],vertex[i]);
            dir.Norm();
            return dir;
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.PointAt (double)"/>
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public GeoPoint PointAt(double Position)
        {
            int c = vertex.Length;
            if (closed) ++c;
            int i, iend;
            Position *= (c - 1); // auf 0..n normiert
            if (Position < 0.0)
            {	// Punkt vor dem Anfang
                i = 0; iend = 1;
            }
            else if (Position > c - 1)
            {	// Punkt nach dem Ende
                i = c - 2;
                iend = c - 1;
            }
            else
            {
                i = (int)Math.Floor(Position);
                iend = i + 1;
            }
            if (iend >= vertex.Length) iend = 0;
            if (i >= vertex.Length) i = vertex.Length - 1;
            GeoVector dir = new GeoVector(vertex[i], vertex[iend]);
            return vertex[i] + (Position - i) * dir;
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.PositionOf (GeoPoint)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public double PositionOf(GeoPoint p)
        {
            if (vertex.Length < 2) return -1.0;
            double res = -1.0;
            int c = vertex.Length;
            if (closed) ++c;
            double maxdist = double.MaxValue;
            for (int i = 0; i < c - 1; ++i)
            {
                int iend;
                if (i == vertex.Length - 1) iend = 0;
                else iend = i + 1;
                GeoPoint dp = Geometry.DropPL(p, vertex[i], vertex[iend]);
                double pos = Geometry.LinePar(vertex[i], vertex[iend] - vertex[i], dp);
                bool valid = (pos >= 0.0 && pos <= 1.0);
                bool inside = valid;
                if (!closed && !valid)
                {	// es sollen auch Positionen über die Verlängerung hinaus gelten
                    valid = (i == 0 && pos < 0.5) || (i == c - 2 && pos > 0.5);
                }
                if (valid)
                {
                    double d = Geometry.DistPL(p, vertex[i], vertex[iend]);
                    // Problem: ein Punkt wurde bereits in der rückwärtigen Verlängerung der Anfangslinie gefunden
                    // und dieser ist gleichgut oder sogar besser als ein innerer, so wurde der innere Punkt ignoriert.
                    // mit der komplexeren Abfrage werden solche inneren Punkte bevorzugt
                    if (!inside) d += Precision.eps; // äußere werden schlechter behandelt
                    if ((res < 0 && valid && Math.Abs(d - maxdist) < Precision.eps) || (d <= maxdist))
                    {
                        maxdist = d;
                        res = i + pos;
                    }
                }
            }
            // wenn ein Eckpunkt näher ist als die gefundenen Fußpunkte, oder
            // wenn kein Fußpunkt gefunden wurde:
            for (int i = 0; i < vertex.Length; ++i)
            {
                double d = Geometry.Dist(p, vertex[i]);
                if (d < maxdist)
                {
                    maxdist = d;
                    res = i;
                }
            }
            return res / (c - 1);
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.PositionOf (GeoPoint, double)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <param name="prefer"></param>
        /// <returns></returns>
        public double PositionOf(GeoPoint p, double prefer)
        {
            return PositionOf(p);
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.PositionOf (GeoPoint, Plane)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <param name="pl"></param>
        /// <returns></returns>
        public double PositionOf(GeoPoint p, Plane pl)
        {
            ICurve2D c2d = GetProjectedCurve(pl);
            return c2d.PositionOf(pl.Project(p));
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.Reverse ()"/>
        /// </summary>
        public void Reverse()
        {
            using (new Changing(this, "Reverse", new object[] { }))
            {
                // planarState = PlanarState.Unknown; Ebene bleibt erhalten
                Array.Reverse(vertex);
                if (closed)
                {
                    GeoPoint swap = vertex[vertex.Length - 1];
                    Array.Copy(vertex, 0, vertex, 1, vertex.Length - 1);
                    vertex[0] = swap;
                }
            }
        }
        public double Length
        {
            get
            {
                double l = 0.0;
                for (int i = 1; i < vertex.Length; ++i) l += Geometry.Dist(vertex[i - 1], vertex[i]);
                if (closed) l += Geometry.Dist(vertex[vertex.Length - 1], vertex[0]);
                return l;
            }
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.Split (double)"/>
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public ICurve[] Split(double Position)
        {
            if (closed)
            {
                GeoPoint[] v1 = new GeoPoint[vertex.Length + 1];
                Polyline tmp = Polyline.Construct();
                for (int i = 0; i < vertex.Length; ++i)
                {
                    v1[i] = vertex[i];
                }
                v1[vertex.Length] = vertex[0];
                tmp.SetPoints(v1, false); // identische offene Polyline
                ICurve[] tmpcurves = tmp.Split(Position);
                if (tmpcurves.Length == 2)
                {
                    // warum hier wieder zusammennähen. Macht jedenfalls einen Fehler beim Rechteck, wenn es mit rechter Maustaste
                    return tmpcurves;
                    //Polyline pl1 = tmpcurves[0] as Polyline;
                    //Polyline pl2 = tmpcurves[1] as Polyline;
                    //// GeoPoint [] v2 = new GeoPoint[vertex.Length+2];
                    //GeoPoint[] v2 = new GeoPoint[pl2.vertex.Length + pl1.vertex.Length - 1];
                    //Array.Copy(pl2.vertex, 0, v2, 0, pl2.vertex.Length);
                    //Array.Copy(pl1.vertex, 1, v2, pl2.vertex.Length, pl1.vertex.Length - 1);
                    //Polyline p1 = Polyline.Construct();
                    //p1.SetPoints(v2, false); // eine offene Polyline zurückliefern (muss eigentlich immer klappen)
                    //return new ICurve[] { p1 };
                }
                else if (tmpcurves.Length == 1)
                {
                    return new ICurve[] { tmpcurves[0] };
                }
                else
                {
                    return new ICurve[] { tmp as ICurve }; // das ist schlecht, denn hier fehlt ein Stück
                    // aber es sollte nie drankommen
                }
            }
            else
            {
                double p = Position * (vertex.Length - 1); // auf 0..n normiert
                GeoPoint splitPoint = PointAt(Position);
                int splitindex = (int)Math.Floor(p);
                Polyline p1 = Polyline.Construct();
                GeoPoint[] v1 = new GeoPoint[splitindex + 2];
                for (int i = 0; i <= splitindex; ++i)
                {
                    v1[i] = vertex[i];
                }
                v1[splitindex + 1] = splitPoint;
                if (!p1.TrySetPoints(v1, false)) p1 = null;
                Polyline p2 = Polyline.Construct();
                GeoPoint[] v2 = new GeoPoint[vertex.Length - splitindex];
                for (int i = splitindex + 1; i < vertex.Length; ++i)
                {
                    v2[i - splitindex] = vertex[i];
                }
                v2[0] = splitPoint;
                if (!p2.TrySetPoints(v2, false)) p2 = null;
                if (p1 != null && p2 != null) return new ICurve[] { p1, p2 };
                else if (p1 != null) return new ICurve[] { p1 };
                else if (p2 != null) return new ICurve[] { p2 };
                else return new ICurve[0]; // sollte eigentlich nicht vorkommen
            }
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.Split (double, double)"/>
        /// </summary>
        /// <param name="Position1"></param>
        /// <param name="Position2"></param>
        /// <returns></returns>
        public ICurve[] Split(double Position1, double Position2)
        {
            if (!closed) return new ICurve[] { };
            GeoPoint p2 = PointAt(Position2);
            if (Position1 > Position2)
            {
                double t = Position2;
                Position2 = Position1;
                Position1 = t;
            }
            ICurve[] tmp = Split(Position1);
            // es gibt eine oder zwei offene Polylinen
            if (tmp.Length == 2)
            {
                Polyline pp = tmp[1] as Polyline;
                if (pp != null)
                {
                    ICurve[] tmp1 = pp.Split(pp.PositionOf(p2));
                    if (tmp1.Length == 2)
                    {
                        Polyline pp1 = tmp1[0] as Polyline; // des innere Stück
                        Polyline pp2 = tmp1[1] as Polyline; // hinteres Stück, dazu noch tmp[0]
                        Polyline pp3 = tmp[0] as Polyline;
                        for (int i = 1; i < pp3.Vertices.Length; i++)
                        {
                            pp2.AddPoint(pp3.Vertices[i]);
                        }
                        return new ICurve[] { pp1, pp2 };
                    }
                    else
                    {
                        return new ICurve[] { tmp[1], tmp[0] };
                    }
                }
            }
            else if (tmp.Length == 1)
            {
                ICurve[] tmp1 = tmp[0].Split(tmp[0].PositionOf(p2));
                return tmp1;
            }
            return new ICurve[] { }; // hat nicht geklappt
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.Trim (double, double)"/>
        /// </summary>
        /// <param name="StartPos"></param>
        /// <param name="EndPos"></param>
        public void Trim(double StartPos, double EndPos)
        {
            // wenn StartPos==EndPos, dann wird SetPoints eine Exception
            // werfen. Ist das überall berücksichtigt?
            if (closed)
            {
                if (StartPos > EndPos)
                {
                    ICurve[] crvs = Split(EndPos, StartPos);
                    SetPoints((crvs[1] as Polyline).vertex, false); // richtiger Abschnitt??
                }
                else
                {
                    ICurve[] crvs = Split(StartPos, EndPos);
                    SetPoints((crvs[0] as Polyline).vertex, false);
                }
            }
            else
            {
                if (StartPos == 0.0)
                {
                    ICurve[] crvs = Split(EndPos);
                    SetPoints((crvs[0] as Polyline).vertex, false);
                }
                else if (EndPos == 1.0)
                {
                    ICurve[] crvs = Split(StartPos);
                    if (crvs.Length > 1) SetPoints((crvs[1] as Polyline).vertex, false);
                    else SetPoints((crvs[0] as Polyline).vertex, false);
                }
                else
                {
                    if (StartPos > EndPos)
                    {
                        double t = EndPos;
                        EndPos = StartPos;
                        StartPos = t;
                    }
                    // double sp = StartPos/EndPos;
                    double sp = StartPos * (vertex.Length - 1);
                    Trim(0.0, EndPos);
                    sp = sp / (vertex.Length - 1);
                    Trim(sp, 1.0);
                }
            }
        }
        ICurve ICurve.Clone() { return (ICurve)this.Clone(); }
        ICurve ICurve.CloneModified(ModOp m)
        {
            IGeoObject clone = Clone();
            clone.Modify(m);
            return (ICurve)clone;
        }
        public bool IsClosed
        {
            get
            {
                return closed;
            }
            set
            {
                using (new Changing(this, "IsClosed", closed))
                {
                    // planarState = PlanarState.Unknown; Ebene bleibt erhalten
                    closed = value;
                }
            }
        }
        public bool IsSingular
        {
            get
            {
                return vertex.Length == 1;
            }
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.GetPlanarState ()"/>
        /// </summary>
        /// <returns></returns>
        public PlanarState GetPlanarState()
        {   // planar state und die Ebene sollte gecachet werden
            if (planarState == PlanarState.Unknown)
            {
                if (vertex.Length == 2) planarState = PlanarState.UnderDetermined;
                else
                {
                    double maxdist;
                    bool isLinear;
                    plane = Plane.FromPoints(vertex, out maxdist, out isLinear);
                    if (isLinear) return PlanarState.UnderDetermined;
                    if (maxdist < Precision.eps) planarState = PlanarState.Planar;
                    else planarState = PlanarState.NonPlanar;
                }
            }
            return planarState;
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.GetPlane ()"/>
        /// </summary>
        /// <returns></returns>
        public Plane GetPlane()
        {
            if (planarState == PlanarState.Unknown) GetPlanarState();
            // es liegt in der Verantwortung des Aufrufers planarState zu überprüfen.
            return plane;
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.IsInPlane (Plane)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public bool IsInPlane(Plane p)
        {
            for (int i = 0; i < vertex.Length; ++i)
            {
                if (!Precision.IsPointOnPlane(vertex[i], p))
                    return false;
            }
            return true;
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.GetProjectedCurve (Plane)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public ICurve2D GetProjectedCurve(Plane p)
        {
            int len = vertex.Length;
            if (closed) ++len;
            GeoPoint2D[] v = new GeoPoint2D[len];
            for (int i = 0; i < vertex.Length; ++i)
            {
                v[i] = p.Project(vertex[i]);
            }
            if (closed) v[vertex.Length] = v[0];
            try
            {
                return new Polyline2D(v);
            }
            catch (Polyline2DException)
            {
                return null; // alle Punkte identisch
            }
        }
        public override string Description
        {
            get
            {
                return StringTable.GetString("Polyline.Description");
            }
        }
        bool ICurve.IsComposed
        {
            get { return true; }
        }
        ICurve[] ICurve.SubCurves
        {
            get
            {
                ICurve[] res;
                if (closed) res = new ICurve[vertex.Length];
                else res = new ICurve[vertex.Length - 1];
                for (int i = 0; i < vertex.Length - 1; ++i)
                {
                    Line l = Line.Construct();
                    l.StartPoint = vertex[i];
                    l.EndPoint = vertex[i + 1];
                    l.CopyAttributes(this);
                    res[i] = l;
                }
                if (closed)
                {
                    Line l = Line.Construct();
                    l.StartPoint = vertex[vertex.Length - 1];
                    l.EndPoint = vertex[0];
                    l.CopyAttributes(this);
                    res[vertex.Length - 1] = l;
                }
                return res;
            }
        }
        ICurve ICurve.Approximate(bool linesOnly, double maxError)
        {
            return Clone() as ICurve;
        }
        double[] ICurve.TangentPosition(GeoVector direction)
        {
            List<double> res = new List<double>();
            for (int i = 0; i < vertex.Length - 1; i++)
            {
                if (Precision.SameDirection(vertex[i + 1] - vertex[i], direction, false))
                {
                    res.Add((i + 0.5) / vertex.Length); // muss noch überprüft werden
                }
            }
            if (closed)
            {
                // naja, das mit dem Parameter stimmt so nicht...
            }
            return res.ToArray();
        }
        double[] ICurve.GetSelfIntersections()
        {
            List<double> res = new List<double>();
            Line l1 = Line.Construct();
            Line l2 = Line.Construct();
            int n = vertex.Length - 1;
            if (IsClosed) ++n;
            for (int i = 0; i < n - 1; ++i)
            {
                for (int j = i + 1; j < n; ++j)
                {
                    l1.SetTwoPoints(vertex[i], vertex[i + 1]);
                    if (j == vertex.Length - 1) l2.SetTwoPoints(vertex[j], vertex[0]);
                    else l2.SetTwoPoints(vertex[j], vertex[j + 1]);
                    double[] intp = CADability.GeoObject.Curves.Intersect(l1, l2, true);
                    for (int k = 0; k < intp.Length; ++k)
                    {
                        double p = l2.PositionOf(l1.PointAt(intp[k]));
                        if (j == i + 1)
                        {   // Ecken selbst nicht als Schnittpunkte interpretieren
                            if (Math.Abs(1.0 - intp[k]) < 1e-8 && Math.Abs(p) < 1e-8) continue;
                        }
                        if (i == 0 && j == vertex.Length - 1 && IsClosed)
                        {   // bei geschlossenen nicht den Schließpunkt beachten
                            if (Math.Abs(intp[k]) < 1e-8 && Math.Abs(1.0 - p) < 1e-8) continue;
                        }
                        if (p >= -1e-8 && p <= 1.0 + 1e-8)
                        {
                            res.Add((i + intp[k]) / n);
                            res.Add((j + p) / n);
                        }
                    }
                }
            }
            return res.ToArray();
        }
        bool ICurve.SameGeometry(ICurve other, double precision)
        {
            if (other is Polyline)
            {
                Polyline pother = (other as Polyline);
                if (vertex.Length == pother.vertex.Length)
                {
                    for (int i = 0; i < vertex.Length; i++)
                    {
                        if ((pother.vertex[i] | vertex[i]) > precision) return false;
                    }
                }
                else
                {
                    for (int i = 0; i < vertex.Length; i++)
                    {
                        if (other.DistanceTo(vertex[i]) > precision) return false;
                    }
                    for (int i = 0; i < pother.vertex.Length; i++)
                    {
                        if ((this as ICurve).DistanceTo(pother.vertex[i]) > precision) return false;
                    }
                }
                return true;
            }
            else if (other is Line)
            {
                for (int i = 0; i < vertex.Length; i++)
                {
                    if (other.DistanceTo(vertex[i]) > precision) return false;
                }
                return true;
            }
            return false;
        }
        double ICurve.PositionAtLength(double position)
        {
            double length = Length;
            int maxind = vertex.Length;
            if (IsClosed) ++maxind;
            for (int i = 1; i < maxind; ++i)
            {
                int n = i;
                if (n >= vertex.Length) n = 0;
                double l = vertex[i - 1] | vertex[n];
                if (l < position)
                {
                    position -= l;
                }
                else
                {
                    return (i - 1 + position / l) / (maxind - 1);
                }
            }
            return 1.0;
        }
        double ICurve.ParameterToPosition(double parameter)
        {
            return parameter / Length;
            // return (this as ICurve).PositionAtLength(parameter);
        }
        double ICurve.PositionToParameter(double position)
        {
            return position * Length;
        }
        bool ICurve.Extend(double atStart, double atEnd)
        {
            return false;
        }
        BoundingCube ICurve.GetExtent()
        {
            return GetExtent(0.0);
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
            List<double> res = new List<double>();
            int c = vertex.Length;
            if (closed) ++c;
            GeoPoint sp = StartPoint;
            for (int i = 0; i < vertex.Length; i++)
            {
                if (!closed && i == 0 || i == vertex.Length - 1) continue;
                double before, after;
                if (i == 0) before = Geometry.LinePar(sp, direction, vertex[vertex.Length - 1]);
                else before = Geometry.LinePar(sp, direction, vertex[i - 1]);
                if (i == vertex.Length - 1) after = 0.0;
                else after = Geometry.LinePar(sp, direction, vertex[i + 1]);
                double lp = Geometry.LinePar(sp, direction, vertex[i]);
                if ((lp <= before && lp <= after) || (lp >= before && lp >= after)) res.Add(i / (double)c);
            }
            return res.ToArray();
        }
        double[] ICurve.GetPlaneIntersection(Plane plane)
        {
            List<double> res = new List<double>();
            for (int i = 0; i < vertex.Length; ++i)
            {
                int nexti = i + 1;
                if (nexti == vertex.Length)
                {
                    if (closed)
                    {
                        if (Precision.IsEqual(vertex[i], vertex[0])) break;
                    }
                    else break;
                    nexti = 0;
                }
                GeoPoint sp = vertex[i];
                GeoPoint ep = vertex[nexti];
                if (plane.Intersect(sp, ep - sp, out GeoPoint pnt))
                {
                    double par = Geometry.LinePar(sp, ep, pnt);
                    //bool ok; // we only want the inner intersection points, except with an open Polyline we would allow outside points for the first and last segment
                    //if (closed) ok = (par >= 0.0 && par <= 1.0);
                    //else
                    //{
                    //    ok = (i == 0 || par >= 0.0) && (i == vertex.Length - 1 || par <= 1.0);
                    //}
                    // it looks like we only want inner intersection points not in the extension of the curve. at least SphericalSurfaceNP assumes this in its constructor
                    if ((par >= 0.0 && par <= 1.0))
                    {
                        if (!closed)
                            par = (i + par) / (vertex.Length - 1);
                        else
                            par = (i + par) / (vertex.Length);
                        res.Add(par);
                    }
                }
            }
            return res.ToArray();
        }
        double ICurve.DistanceTo(GeoPoint p)
        {
            double pos = (this as ICurve).PositionOf(p);
            if (pos >= 0.0 && pos <= 1.0)
            {
                GeoPoint pCurve = (this as ICurve).PointAt(pos);
                return pCurve | p;
            }
            else
            {
                return Math.Min(p | StartPoint, p | EndPoint);
            }
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.TryPointDeriv2At (double, out GeoPoint, out GeoVector, out GeoVector)"/>
        /// </summary>
        /// <param name="position"></param>
        /// <param name="point"></param>
        /// <param name="deriv"></param>
        /// <param name="deriv2"></param>
        /// <returns></returns>
        public virtual bool TryPointDeriv2At(double position, out GeoPoint point, out GeoVector deriv, out GeoVector deriv2)
        {
            point = GeoPoint.Origin;
            deriv = deriv2 = GeoVector.NullVector;
            return false;
        }
        #endregion
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected Polyline(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            vertex = info.GetValue("Vertex", typeof(GeoPoint[])) as GeoPoint[];
            closed = (bool)info.GetValue("Closed", typeof(bool));
            colorDef = info.GetValue("ColorDef", typeof(ColorDef)) as ColorDef;
            lineWidth = LineWidth.Read("LineWidth", info, context);
            linePattern = LinePattern.Read("LinePattern", info, context);
            planarState = PlanarState.Unknown;
            if (Constructed != null) Constructed(this);
        }

        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("Vertex", vertex);
            info.AddValue("Closed", closed);
            info.AddValue("ColorDef", colorDef);
            info.AddValue("LineWidth", lineWidth);
            info.AddValue("LinePattern", linePattern);
        }

        public override void GetObjectData(IJsonWriteData data)
        {
            base.GetObjectData(data);
            data.AddProperty("Vertex", vertex);
            data.AddProperty("Closed", closed);
            if (colorDef != null) data.AddProperty("ColorDef", colorDef);
            if (lineWidth != null) data.AddProperty("LineWidth", lineWidth);
            if (linePattern != null) data.AddProperty("LinePattern", linePattern);
        }

        public override void SetObjectData(IJsonReadData data)
        {
            base.SetObjectData(data);
            vertex = data.GetProperty<GeoPoint[]>("Vertex");
            closed = data.GetProperty<bool>("Closed");
            colorDef = data.GetPropertyOrDefault<ColorDef>("ColorDef");
            lineWidth = data.GetPropertyOrDefault<LineWidth>("LineWidth");
            linePattern = data.GetPropertyOrDefault<LinePattern>("LinePattern");
        }

        #endregion
        #region ILineWidth Members
        private LineWidth lineWidth;
        public LineWidth LineWidth
        {
            get
            {
                return lineWidth;
            }
            set
            {
                using (new ChangingAttribute(this, "LineWidth", lineWidth))
                {
                    // planarState = PlanarState.Unknown; Ebene bleibt erhalten
                    lineWidth = value;
                }
            }
        }
        #endregion
        #region ILinePattern Members
        private LinePattern linePattern;
        public LinePattern LinePattern
        {
            get
            {
                return linePattern;
            }
            set
            {
                using (new ChangingAttribute(this, "LinePattern", linePattern))
                {
                    // planarState = PlanarState.Unknown; Ebene bleibt erhalten
                    linePattern = value;
                }
            }
        }
        #endregion
        #region IExtentedableCurve Members
        IOctTreeInsertable IExtentedableCurve.GetExtendedCurve(ExtentedableCurveDirection direction)
        {
            return new InfinitePolyLine(vertex);
        }
        #endregion
        int IExportStep.Export(ExportStep export, bool topLevel)
        {
            if (vertex.Length == 2)
            {
                Line ln = Line.TwoPoints(vertex[0], vertex[1]);
                return (ln as IExportStep).Export(export, topLevel);
            }
            else
            {
                BSpline bspl = BSpline.Construct();
                bspl.ThroughPoints(vertex, 1, this.IsClosed);
                return (bspl as IExportStep).Export(export, topLevel);
            }
        }
        internal void CyclicalPermutation(int index)
        {
            GeoPoint[] newvertex = new GeoPoint[vertex.Length];
            for (int i = index; i < vertex.Length; i++)
            {
                newvertex[i - index] = vertex[i];
            }
            for (int i = 0; i < index; i++)
            {
                newvertex[vertex.Length - index + i] = vertex[i];
            }
            using (new Changing(this, "SetPoints", vertex, this.closed))
            {
                vertex = newvertex;
            }
        }
    }

    internal class InfinitePolyLine : IOctTreeInsertable
    {
        GeoPoint[] vertex;

        public InfinitePolyLine(GeoPoint[] vertex)
        {
            this.vertex = vertex; // wird ja nie verändert
        }

        #region IOctTreeInsertable Members

        BoundingCube IOctTreeInsertable.GetExtent(double precision)
        {
            return BoundingCube.InfiniteBoundingCube; // stimmt nicht unbedingt
        }

        bool IOctTreeInsertable.HitTest(ref BoundingCube cube, double precision)
        {
            for (int i = 1; i < vertex.Length - 3; ++i)
            {
                if (cube.Interferes(ref vertex[i], ref vertex[i + 1])) return true;
            }
            if (cube.Interferes(vertex[1], vertex[0] - vertex[1], precision, true)) return true;
            if (cube.Interferes(vertex[vertex.Length - 2], vertex[vertex.Length - 1] - vertex[vertex.Length - 2], precision, true)) return true;
            return false;
        }

        bool IOctTreeInsertable.HitTest(Projection projection, BoundingRect rect, bool onlyInside)
        {
            ClipRect clr = new ClipRect(rect);
            for (int i = 1; i < vertex.Length - 3; ++i)
            {
                if (clr.LineHitTest(projection.ProjectUnscaled(vertex[i]), projection.ProjectUnscaled(vertex[i + 1]))) return true;
            }
            InfiniteRay r1 = new InfiniteRay(vertex[1], vertex[0] - vertex[1]);
            if ((r1 as IOctTreeInsertable).HitTest(projection, rect, onlyInside)) return true;
            InfiniteRay r2 = new InfiniteRay(vertex[vertex.Length - 2], vertex[vertex.Length - 1] - vertex[vertex.Length - 2]);
            if ((r2 as IOctTreeInsertable).HitTest(projection, rect, onlyInside)) return true;
            return false;
        }

        double IOctTreeInsertable.Position(GeoPoint fromHere, GeoVector direction, double precision)
        {   // sollte nie drankommen
            throw new Exception("The method or operation is not implemented.");
        }

        bool IOctTreeInsertable.HitTest(Projection.PickArea area, bool onlyInside)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        #endregion
    }
}
