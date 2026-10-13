using CADability.Attribute;
using System;
#if WEBASSEMBLY
using CADability.WebDrawing;
using Point = CADability.WebDrawing.Point;
#else
using System.Drawing;
using Point = System.Drawing.Point;
#endif

namespace CADability.GeoObject
{
    /// <summary>
    /// Ths class serves as a parameter to <see cref="IGeoObject.FindSnapPoint"/>.
    /// It keeps the best snappoint, the object, which caused it and the distance
    /// to the mouse point.
    /// </summary>

    public class SnapPointFinder
    {
        /// <summary>
        /// Snap modes to specify in which points the SnapPointFinder should respect
        /// </summary>
        [Flags]
        public enum SnapModes
        {
            // SnapToPoint = 0x01, Unterschied zu SnapToObjectSnapPoint?
            /// <summary>
            /// Snap to intersection points of curves.
            /// </summary>
            SnapToIntersectionPoint = 0x02,
            /// <summary>
            /// Snap to the perpendicular foot point from the basepoint. A basepoint is required.
            /// </summary>
            SnapToDropPoint = 0x04,
            /// <summary>
            /// Snap to any point on the object, may be some intermediate point on the object.
            /// </summary>
            SnapToObjectPoint = 0x08,
            /// <summary>
            /// Snap to a specifically qualified point on the object, usually start or endpoint, centerpoint depending on the kind of object.
            /// </summary>
            SnapToObjectSnapPoint = 0x10,
            /// <summary>
            /// Snap to the center of the object under the cursor.
            /// </summary>
            SnapToObjectCenter = 0x20,
            /// <summary>
            /// Snap to a tangent point of the object under the cursor. A basepoint must be defined from which the tangent line starts.
            /// </summary>
            SnapToTangentPoint = 0x40,
            /// <summary>
            /// Snap to a grid point. This has lower priority than the other snap operations.
            /// </summary>
            SnapToGridPoint = 0x80,
            /// <summary>
            /// Snap to a point horizontally or vertically adjusted to the basepoint. A basepoint is required in this maode.
            /// </summary>
            AdjustOrtho = 0x100,
            /// <summary>
            /// Snap to the absolute origin of the coordinatesystem or drawing plane.
            /// </summary>
            SnapToZero = 0x200,
            /// <summary>
            /// Snap to any point on the surface of the Face under the cursor.
            /// </summary>
            SnapToFaceSurface = 0x400
        }
        /// <summary>
        /// Resulting snap modes of the snap opertaion.
        /// </summary>
        public enum DidSnapModes
        {
            /// <summary>
            /// There was no snap action.
            /// </summary>
            DidNotSnap,
            /// <summary>
            /// <see cref="SnapModes.SnapToIntersectionPoint"/> caused the snapping.
            /// </summary>
            DidSnapToIntersectionPoint,
            /// <summary>
            /// <see cref="SnapModes.SnapToDropPoint"/> caused the snapping.
            /// </summary>
            DidSnapToDropPoint,
            /// <summary>
            /// <see cref="SnapModes.SnapToObjectPoint"/> caused the snapping.
            /// </summary>
            DidSnapToObjectPoint,
            /// <summary>
            /// <see cref="SnapModes.SnapToObjectSnapPoint"/> caused the snapping.
            /// </summary>
            DidSnapToObjectSnapPoint,
            /// <summary>
            /// <see cref="SnapModes.SnapToObjectCenter"/> caused the snapping.
            /// </summary>
            DidSnapToObjectCenter,
            /// <summary>
            /// <see cref="SnapModes.SnapToTangentPoint"/> caused the snapping.
            /// </summary>
            DidSnapToTangentPoint,
            /// <summary>
            /// <see cref="SnapModes.SnapToGridPoint"/> caused the snapping.
            /// </summary>
            DidSnapToGridPoint,
            /// <summary>
            /// <see cref="SnapModes.AdjustOrtho"/> caused the snapping.
            /// </summary>
            DidAdjustOrtho,
            /// <summary>
            /// <see cref="SnapModes.SnapToZero"/> caused the snapping to the drawing plane origin.
            /// </summary>
            DidSnapToLocalZero,
            /// <summary>
            /// <see cref="SnapModes.SnapToZero"/> caused the snapping to absolute zero.
            /// </summary>
            DidSnapToAbsoluteZero,
            /// <summary>
            /// <see cref="SnapModes.SnapToFaceSurface"/> caused the snapping.
            /// </summary>
            DidSnapToFaceSurface,
            /// <summary>
            /// There was a keyboard input for the point, no snapping.
            /// </summary>
            KeyboardInput
        }
        public double MaxDist;
        public double BestDist;
        public double faceDist;
        public IGeoObject BestObject;
        public int SnapPointIndex;
        public GeoPoint SnapPoint;
        public GeoPoint BasePoint;
        public bool BasePointValid;
        public SnapModes SnapMode;
        public bool Snap30;
        public bool Snap45;
        public bool SnapLocalOrigin;
        public bool SnapGlobalOrigin;
        public GeoObjectList IgnoreList;
        public FilterList FilterList;
        public DidSnapModes DidSnap;
        /// <summary>
        /// Postition of the source point on the projection plane
        /// </summary>
        public GeoPoint2D SourcePoint;
        public Axis SourceBeam;
        public Projection.PickArea pickArea;
        public Projection Projection;
        public SnapPointFinder()
        {
            // alles leer initialisiert, kein new nötig
        }
#if WEBASSEMBLY
       public void Init(CADability.WebDrawing.Point SourcePoint, Projection projection, SnapModes SnapMode, int MaxDist)
#else
        public void Init(System.Drawing.Point SourcePoint, Projection projection, SnapModes SnapMode, int MaxDist)
#endif
        {
            this.BestDist = double.MaxValue;
            this.BestObject = null;
            this.SnapPointIndex = 0;
            SourceBeam = projection.PointBeam(SourcePoint);
            this.SourcePoint = projection.ProjectionPlane.Intersect(SourceBeam);
            this.Projection = projection;
            this.SnapPoint = projection.DrawingPlanePoint(SourcePoint);
            // MaxDist die die maximale Entfernung des Fangpunktes vom aktuellen Punkt bezogen auf die ProjectionPlane
#if WEBASSEMBLY
            GeoPoint2D p1 = projection.ProjectionPlane.Intersect(projection.PointBeam(new CADability.WebDrawing.Point(SourcePoint.X, SourcePoint.Y + MaxDist)));
            GeoPoint2D p2 = projection.ProjectionPlane.Intersect(projection.PointBeam(new CADability.WebDrawing.Point(SourcePoint.X + MaxDist, SourcePoint.Y)));
            GeoPoint2D p3 = projection.ProjectionPlane.Intersect(projection.PointBeam(new CADability.WebDrawing.Point(SourcePoint.X, SourcePoint.Y - MaxDist)));
            GeoPoint2D p4 = projection.ProjectionPlane.Intersect(projection.PointBeam(new CADability.WebDrawing.Point(SourcePoint.X - MaxDist, SourcePoint.Y)));
#else
            GeoPoint2D p1 = projection.ProjectionPlane.Intersect(projection.PointBeam(new System.Drawing.Point(SourcePoint.X, SourcePoint.Y + MaxDist)));
            GeoPoint2D p2 = projection.ProjectionPlane.Intersect(projection.PointBeam(new System.Drawing.Point(SourcePoint.X + MaxDist, SourcePoint.Y)));
            GeoPoint2D p3 = projection.ProjectionPlane.Intersect(projection.PointBeam(new System.Drawing.Point(SourcePoint.X, SourcePoint.Y - MaxDist)));
            GeoPoint2D p4 = projection.ProjectionPlane.Intersect(projection.PointBeam(new System.Drawing.Point(SourcePoint.X - MaxDist, SourcePoint.Y)));
#endif
            this.MaxDist = Math.Max(Math.Max(p1 | this.SourcePoint, p2 | this.SourcePoint), Math.Max(p3 | this.SourcePoint, p4 | this.SourcePoint));
            pickArea = projection.GetPickSpace(new Rectangle(SourcePoint.X - MaxDist, SourcePoint.Y - MaxDist, 2 * MaxDist, 2 * MaxDist));
            this.BasePointValid = false;
            this.SnapMode = SnapMode;
            this.DidSnap = DidSnapModes.DidNotSnap;
            faceDist = double.MaxValue;
        }
#if WEBASSEMBLY
        public void Init(CADability.WebDrawing.Point SourcePoint, Projection projection, SnapModes SnapMode, int MaxDist, GeoPoint BasePoint)
#else
        public void Init(System.Drawing.Point SourcePoint, Projection projection, SnapModes SnapMode, int MaxDist, GeoPoint BasePoint)
#endif
        {
            Init(SourcePoint, projection, SnapMode, MaxDist);
            this.BasePoint = BasePoint;
            this.BasePointValid = true;
        }
        public bool Accept(IGeoObject toTest)
        {
            if (FilterList == null) return true;
            return FilterList.Accept(toTest);
        }
        /// <value>
        /// Gets the source point (which is the mouse position on the projection plane)
        /// as a 3D point. Together with the direction of the projection this makes a
        /// line that is perpendicular to the screen and goes through the mouse point.
        /// </value>
        public GeoPoint SourcePoint3D
        {
            get { return Projection.ProjectionPlane.ToGlobal(SourcePoint); }
        }
        public bool SnapToIntersectionPoint
        {
            get { return (SnapMode & SnapModes.SnapToIntersectionPoint) != 0; }
        }
        public bool SnapToDropPoint
        {
            get { return (SnapMode & SnapModes.SnapToDropPoint) != 0; }
        }
        public bool SnapToObjectPoint
        {
            get { return (SnapMode & SnapModes.SnapToObjectPoint) != 0; }
        }
        public bool SnapToObjectSnapPoint
        {
            get { return (SnapMode & SnapModes.SnapToObjectSnapPoint) != 0; }
        }
        public bool SnapToObjectCenter
        {
            get { return (SnapMode & SnapModes.SnapToObjectCenter) != 0; }
        }
        public bool SnapToTangentPoint
        {
            get { return (SnapMode & SnapModes.SnapToTangentPoint) != 0; }
        }
        public bool SnapToGridPoint
        {
            get { return (SnapMode & SnapModes.SnapToGridPoint) != 0; }
        }
        public bool AdjustOrtho
        {
            get { return (SnapMode & SnapModes.AdjustOrtho) != 0; }
        }
        public bool SnapToZero
        {
            get { return (SnapMode & SnapModes.SnapToZero) != 0; }
        }
        public bool SnapToFaceSurface
        {
            get { return (SnapMode & SnapModes.SnapToFaceSurface) != 0; }
        }
        public int SnapHierarchy(DidSnapModes mode)
        {
            switch (mode)
            {
                case DidSnapModes.DidSnapToIntersectionPoint:
                    return (1);
                case DidSnapModes.DidSnapToObjectSnapPoint:
                    return (1);
                case DidSnapModes.DidSnapToTangentPoint:
                    return (2);
                case DidSnapModes.DidSnapToDropPoint:
                    return (2);
                case DidSnapModes.DidSnapToObjectCenter:
                    return (3);
                case DidSnapModes.DidSnapToObjectPoint:
                    return (4);
                case DidSnapModes.DidSnapToGridPoint:
                    return (5);
                case DidSnapModes.DidAdjustOrtho:
                    return (6);
                default:
                    return (7);
            }


        }
        /// <summary>
        /// The <see cref="IGeoObject"/> obj offers the point p as a snappoint in the given mode.
        /// This method checks whether to use this point as the closest snap point.
        /// This method is typically called by GeoObjects from the <see cref="IGeoObject.FindSnapPoint"/> method.
        /// </summary>
        /// <param name="p"></param>
        /// <param name="obj"></param>
        /// <param name="mode"></param>
        public void Check(GeoPoint p, IGeoObject obj, DidSnapModes mode)
        {
            GeoPoint p2 = Geometry.DropPL(p, SourceBeam.Location, SourceBeam.Direction);
            double d = Projection.WorldToProjectionPlane(p) | Projection.WorldToProjectionPlane(p2);
            // d ist der Abstand bezogen auf die projectionplane
            bool DoIt = false;
            if (SnapHierarchy(mode) <= SnapHierarchy(DidSnap))
            {
                if (mode == DidSnapModes.DidSnapToFaceSurface)
                {   // hier zählt die sichtbare Fläche, die das
                    // kleinste faceDist hat. Das wird in Face gecheckt
                    DoIt = true;
                }
                else if (SnapHierarchy(mode) == SnapHierarchy(DidSnap))
                    DoIt = (d < BestDist && d < MaxDist);
                else DoIt = d < MaxDist;
                if (mode == DidSnapModes.DidSnapToDropPoint ||
                    mode == DidSnapModes.DidSnapToObjectCenter ||
                    mode == DidSnapModes.DidSnapToTangentPoint)
                    DoIt = d < BestDist;
                if (mode == DidSnapModes.DidAdjustOrtho || mode == DidSnapModes.DidSnapToGridPoint) DoIt = true; // Orthogonal unabhängig von der Entfernung
            }
            if (DoIt)
            {
                SnapPoint = p;
                BestDist = d;
                DidSnap = mode;
                BestObject = obj;
            }
        }
    }
}
