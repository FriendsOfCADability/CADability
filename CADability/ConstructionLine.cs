using System;
using System.Runtime.Serialization;

namespace CADability.GeoObject
{
    /// <summary>
    /// A construction line of conceptually infinite extent: either an infinite line in both
    /// directions (like the DXF XLINE entity) or a half-infinite ray starting at a base point
    /// (like the DXF RAY entity).
    /// CADability curves are always finite, so this object is displayed — and pickable, snappable
    /// and trimmable — as a very long finite segment. What distinguishes it from a plain
    /// <see cref="Line"/> is its extent behavior: <see cref="GetBoundingCube"/> and the projected
    /// extent only report the <see cref="BasePoint"/>, so zoom-to-extents and the model extent
    /// ignore the (huge) displayed length, exactly as AutoCAD ignores XLINE/RAY entities when
    /// zooming to the drawing extents. <see cref="GetExtent(double)"/> keeps reporting the real
    /// segment so spatial searches (picking, snapping) work along the whole displayed line.
    /// </summary>
    [Serializable()]
    public class ConstructionLine : Line, ISerializable
    {
        // Half-length of the displayed segment: long enough to cross any usual drawing,
        // harmless for the view since the extent overrides below exclude it.
        private const double displayLength = 1e6;
        private bool isRay; // true: half-infinite ray starting at BasePoint, false: infinite line

        #region polymorph construction
        /// <summary>
        /// Delegate for the construction of a ConstructionLine.
        /// </summary>
        /// <returns>A ConstructionLine or ConstructionLine derived class</returns>
        public new delegate ConstructionLine ConstructionDelegate();
        /// <summary>
        /// Provide a delegate here if you want your ConstructionLine derived class to be
        /// created each time CADability creates a ConstructionLine.
        /// </summary>
        public new static ConstructionDelegate Constructor;
        /// <summary>
        /// The only way to create a ConstructionLine. There are no public constructors to assure
        /// that this is the only way to construct a ConstructionLine.
        /// </summary>
        /// <returns></returns>
        public new static ConstructionLine Construct()
        {
            if (Constructor != null) return Constructor();
            return new ConstructionLine();
        }
        /// <summary>
        /// Empty protected constructor.
        /// </summary>
        protected ConstructionLine()
            : base()
        {
        }
        #endregion
        /// <summary>
        /// Creates a half-infinite construction ray starting at <paramref name="basePoint"/>
        /// extending in <paramref name="direction"/> (corresponds to the DXF RAY entity).
        /// </summary>
        /// <param name="basePoint">start point of the ray</param>
        /// <param name="direction">direction of the ray, must not be the null vector</param>
        /// <returns>the ray</returns>
        public static ConstructionLine MakeRay(GeoPoint basePoint, GeoVector direction)
        {
            if (direction.IsNullVector()) throw new ArgumentException("direction must not be the null vector", nameof(direction));
            ConstructionLine res = Construct();
            res.isRay = true;
            GeoVector dir = direction.Normalized;
            res.SetTwoPoints(basePoint, basePoint + displayLength * dir);
            return res;
        }
        /// <summary>
        /// Creates an infinite construction line through <paramref name="basePoint"/>
        /// extending in both directions of <paramref name="direction"/> (corresponds to the DXF XLINE entity).
        /// </summary>
        /// <param name="basePoint">a point on the line</param>
        /// <param name="direction">direction of the line, must not be the null vector</param>
        /// <returns>the construction line</returns>
        public static ConstructionLine MakeXLine(GeoPoint basePoint, GeoVector direction)
        {
            if (direction.IsNullVector()) throw new ArgumentException("direction must not be the null vector", nameof(direction));
            ConstructionLine res = Construct();
            res.isRay = false;
            GeoVector dir = direction.Normalized;
            res.SetTwoPoints(basePoint - displayLength * dir, basePoint + displayLength * dir);
            return res;
        }
        /// <summary>
        /// True if this is a half-infinite ray starting at <see cref="BasePoint"/> (DXF RAY),
        /// false if it is an infinite line (DXF XLINE).
        /// </summary>
        public bool IsRay
        {
            get { return isRay; }
        }
        /// <summary>
        /// The defining base point: the start point of the ray, or the point the infinite
        /// line was defined with (the middle of the displayed segment).
        /// </summary>
        public GeoPoint BasePoint
        {
            get { return isRay ? StartPoint : new GeoPoint(StartPoint, EndPoint); }
        }
        /// <summary>
        /// The normalized direction of the construction line.
        /// </summary>
        public GeoVector Direction
        {
            get { return (EndPoint - StartPoint).Normalized; }
        }
        /// <summary>
        /// Overrides <see cref="Line.Clone"/>, returns a clone of this construction line.
        /// </summary>
        /// <returns>the clone</returns>
        public override IGeoObject Clone()
        {
            ConstructionLine result = Construct();
            ++result.isChanging;
            result.CopyGeometry(this);
            result.CopyAttributes(this);
            --result.isChanging;
            return result;
        }
        /// <summary>
        /// Overrides <see cref="Line.CopyGeometry"/>, additionally copies the ray flag.
        /// </summary>
        /// <param name="ToCopyFrom">must be a Line (or ConstructionLine) to copy the data from</param>
        public override void CopyGeometry(IGeoObject ToCopyFrom)
        {
            base.CopyGeometry(ToCopyFrom);
            if (ToCopyFrom is ConstructionLine cl) isRay = cl.isRay;
        }
        /// <summary>
        /// Overrides <see cref="Line.GetBoundingCube"/>: only the <see cref="BasePoint"/> is
        /// reported so the displayed (huge) segment does not contribute to the model extent
        /// and zoom-to-extents ignores the construction line.
        /// </summary>
        /// <returns>the bounding cube of the base point</returns>
        public override BoundingCube GetBoundingCube()
        {
            return new BoundingCube(BasePoint);
        }
        /// <summary>
        /// Overrides <see cref="Line.GetExtent(double)"/>: in contrast to <see cref="GetBoundingCube"/>
        /// this returns the real extent of the displayed segment, so spatial data structures
        /// (octree) used for picking and snapping cover the whole displayed line.
        /// </summary>
        /// <param name="precision"></param>
        /// <returns>the extent of the displayed segment</returns>
        public override BoundingCube GetExtent(double precision)
        {
            BoundingCube res = BoundingCube.EmptyBoundingCube;
            res.MinMax(StartPoint);
            res.MinMax(EndPoint);
            return res;
        }
        /// <summary>
        /// Overrides <see cref="IGeoObjectImpl.GetExtent(Projection, ExtentPrecision)"/>: only the
        /// projected <see cref="BasePoint"/> is reported so 2D zoom-to-extents ignores the
        /// displayed length as well.
        /// </summary>
        /// <param name="projection"></param>
        /// <param name="extentPrecision"></param>
        /// <returns>the projected base point</returns>
        public override BoundingRect GetExtent(Projection projection, ExtentPrecision extentPrecision)
        {
            return new BoundingRect(projection.Project(BasePoint));
        }
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        protected ConstructionLine(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            isRay = info.GetBoolean("IsRay");
        }
        /// <summary>
        /// Implements ISerializable:GetObjectData
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("IsRay", isRay);
        }
        public override void GetObjectData(IJsonWriteData data)
        {
            base.GetObjectData(data);
            data.AddProperty("IsRay", isRay);
        }
        public override void SetObjectData(IJsonReadData data)
        {
            base.SetObjectData(data);
            isRay = data.GetPropertyOrDefault<bool>("IsRay");
        }
        #endregion
    }
}
