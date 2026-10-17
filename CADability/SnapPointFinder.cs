using CADability.Attribute;
using System;
using System.Drawing;
using Point = System.Drawing.Point;

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
        /// <summary>
        /// Maximum distance (measured on the projection plane, in model units) a snap point may have from the mouse
        /// position to be accepted. Computed by <see cref="Init(System.Drawing.Point, Projection, SnapModes, int)"/> from the pixel
        /// distance passed there.
        /// </summary>
        public double MaxDist;
        /// <summary>
        /// Distance (on the projection plane) of the best snap point found so far from the mouse position. Starts at
        /// <see cref="double.MaxValue"/>; <see cref="Check"/> replaces the current snap point by one of the same priority
        /// only when it is closer.
        /// </summary>
        public double BestDist;
        /// <summary>
        /// For <see cref="SnapModes.SnapToFaceSurface"/>: the position along the <see cref="SourceBeam"/> of the closest
        /// surface found so far. Faces (and bitmaps) use it to keep only the frontmost surface under the cursor.
        /// </summary>
        public double faceDist;
        /// <summary>
        /// The object that provided the current <see cref="SnapPoint"/>, or null if no object was involved (grid, origin,
        /// orthogonal adjustment or no snapping at all). Views keep it as <see cref="IView.LastSnapObject"/>.
        /// </summary>
        public IGeoObject BestObject;
        /// <summary>
        /// Reserved for the index of the snap point within <see cref="BestObject"/>. It is reset to 0 by
        /// <see cref="Init(System.Drawing.Point, Projection, SnapModes, int)"/> and not set by the current snap implementations.
        /// </summary>
        public int SnapPointIndex;
        /// <summary>
        /// The result: the snapped point in model coordinates. Before any snapping it holds the mouse position on the
        /// drawing plane, so it is valid even when <see cref="DidSnap"/> is <see cref="DidSnapModes.DidNotSnap"/>.
        /// </summary>
        public GeoPoint SnapPoint;
        /// <summary>
        /// The base point (in model coordinates) for the snap modes that refer to a previously defined point:
        /// <see cref="SnapModes.SnapToDropPoint"/>, <see cref="SnapModes.SnapToTangentPoint"/> and
        /// <see cref="SnapModes.AdjustOrtho"/>. Only meaningful when <see cref="BasePointValid"/> is true.
        /// </summary>
        public GeoPoint BasePoint;
        /// <summary>
        /// True when <see cref="BasePoint"/> has been set, i.e. when the
        /// <see cref="Init(System.Drawing.Point, Projection, SnapModes, int, GeoPoint)"/> overload was used.
        /// </summary>
        public bool BasePointValid;
        /// <summary>
        /// The combination of <see cref="SnapModes"/> to apply; the <c>SnapToXxx</c> properties test its individual flags.
        /// </summary>
        public SnapModes SnapMode;
        /// <summary>
        /// If true, circles, arcs and ellipses additionally offer the points at multiples of 30 degrees as object snap
        /// points (<see cref="DidSnapModes.DidSnapToObjectSnapPoint"/>).
        /// </summary>
        public bool Snap30;
        /// <summary>
        /// If true, circles, arcs and ellipses additionally offer the points at multiples of 45 degrees as object snap
        /// points (<see cref="DidSnapModes.DidSnapToObjectSnapPoint"/>).
        /// </summary>
        public bool Snap45;
        /// <summary>
        /// If true, the origin of the drawing plane is offered as a snap point (<see cref="DidSnapModes.DidSnapToLocalZero"/>,
        /// setting "Snap.SnapLocalOrigin").
        /// </summary>
        public bool SnapLocalOrigin;
        /// <summary>
        /// If true, the origin of the model coordinate system is offered as a snap point
        /// (<see cref="DidSnapModes.DidSnapToAbsoluteZero"/>, setting "Snap.SnapGlobalOrigin").
        /// </summary>
        public bool SnapGlobalOrigin;
        /// <summary>
        /// Objects excluded from the snap search, typically the object that is currently being modified with the mouse.
        /// May be null.
        /// </summary>
        public GeoObjectList IgnoreList;
        /// <summary>
        /// Filter deciding which objects take part in the snap search (usually <see cref="Project.FilterList"/>), see
        /// <see cref="Accept"/>. May be null, in which case all objects are accepted.
        /// </summary>
        public FilterList FilterList;
        /// <summary>
        /// The result: the snap mode that produced the current <see cref="SnapPoint"/>, or
        /// <see cref="DidSnapModes.DidNotSnap"/>.
        /// </summary>
        public DidSnapModes DidSnap;
        /// <summary>
        /// Postition of the source point on the projection plane
        /// </summary>
        public GeoPoint2D SourcePoint;
        /// <summary>
        /// The line perpendicular to the screen through the mouse position, in model coordinates. Snap points are searched
        /// close to this line, and surfaces are intersected with it for <see cref="SnapModes.SnapToFaceSurface"/>.
        /// </summary>
        public Axis SourceBeam;
        /// <summary>
        /// The pick area (a small square around the mouse position, extruded along the view direction) used to find the
        /// objects close to the mouse. Its size is given by the pixel distance passed to
        /// <see cref="Init(System.Drawing.Point, Projection, SnapModes, int)"/>.
        /// </summary>
        public Projection.PickArea pickArea;
        /// <summary>
        /// The projection of the view in which the snapping takes place. It provides the drawing plane and the conversion
        /// between model coordinates and the projection plane.
        /// </summary>
        public Projection Projection;
        /// <summary>
        /// Creates an uninitialized finder. Call <see cref="Init(System.Drawing.Point, Projection, SnapModes, int)"/> (or the overload
        /// with a base point) before use.
        /// </summary>
        public SnapPointFinder()
        {
            // alles leer initialisiert, kein new nötig
        }
        /// <summary>
        /// Prepares the finder for a snap search at the mouse position <paramref name="SourcePoint"/>: resets the result
        /// (<see cref="SnapPoint"/> is the mouse position on the drawing plane, <see cref="DidSnap"/> is
        /// <see cref="DidSnapModes.DidNotSnap"/>), computes <see cref="SourceBeam"/>, <see cref="pickArea"/> and
        /// <see cref="MaxDist"/> and sets the snap modes. No base point is defined, so the modes that need one have no effect.
        /// </summary>
        /// <param name="SourcePoint">The mouse position in pixels of the view</param>
        /// <param name="projection">The projection of the view</param>
        /// <param name="SnapMode">The snap modes to apply</param>
        /// <param name="MaxDist">Maximum distance in pixels between the mouse position and an accepted snap point</param>
        public void Init(System.Drawing.Point SourcePoint, Projection projection, SnapModes SnapMode, int MaxDist)
        {
            this.BestDist = double.MaxValue;
            this.BestObject = null;
            this.SnapPointIndex = 0;
            SourceBeam = projection.PointBeam(SourcePoint);
            this.SourcePoint = projection.ProjectionPlane.Intersect(SourceBeam);
            this.Projection = projection;
            this.SnapPoint = projection.DrawingPlanePoint(SourcePoint);
            // MaxDist die die maximale Entfernung des Fangpunktes vom aktuellen Punkt bezogen auf die ProjectionPlane
            GeoPoint2D p1 = projection.ProjectionPlane.Intersect(projection.PointBeam(new System.Drawing.Point(SourcePoint.X, SourcePoint.Y + MaxDist)));
            GeoPoint2D p2 = projection.ProjectionPlane.Intersect(projection.PointBeam(new System.Drawing.Point(SourcePoint.X + MaxDist, SourcePoint.Y)));
            GeoPoint2D p3 = projection.ProjectionPlane.Intersect(projection.PointBeam(new System.Drawing.Point(SourcePoint.X, SourcePoint.Y - MaxDist)));
            GeoPoint2D p4 = projection.ProjectionPlane.Intersect(projection.PointBeam(new System.Drawing.Point(SourcePoint.X - MaxDist, SourcePoint.Y)));
            this.MaxDist = Math.Max(Math.Max(p1 | this.SourcePoint, p2 | this.SourcePoint), Math.Max(p3 | this.SourcePoint, p4 | this.SourcePoint));
            pickArea = projection.GetPickSpace(new Rectangle(SourcePoint.X - MaxDist, SourcePoint.Y - MaxDist, 2 * MaxDist, 2 * MaxDist));
            this.BasePointValid = false;
            this.SnapMode = SnapMode;
            this.DidSnap = DidSnapModes.DidNotSnap;
            faceDist = double.MaxValue;
        }
        /// <summary>
        /// Same as <see cref="Init(System.Drawing.Point, Projection, SnapModes, int)"/>, but additionally defines the <see cref="BasePoint"/>
        /// needed for <see cref="SnapModes.SnapToDropPoint"/>, <see cref="SnapModes.SnapToTangentPoint"/> and
        /// <see cref="SnapModes.AdjustOrtho"/>.
        /// </summary>
        /// <param name="SourcePoint">The mouse position in pixels of the view</param>
        /// <param name="projection">The projection of the view</param>
        /// <param name="SnapMode">The snap modes to apply</param>
        /// <param name="MaxDist">Maximum distance in pixels between the mouse position and an accepted snap point</param>
        /// <param name="BasePoint">The base point in model coordinates</param>
        public void Init(System.Drawing.Point SourcePoint, Projection projection, SnapModes SnapMode, int MaxDist, GeoPoint BasePoint)
        {
            Init(SourcePoint, projection, SnapMode, MaxDist);
            this.BasePoint = BasePoint;
            this.BasePointValid = true;
        }
        /// <summary>
        /// Tests whether <paramref name="toTest"/> may take part in the snap search according to <see cref="FilterList"/>.
        /// </summary>
        /// <param name="toTest">The object to test</param>
        /// <returns>true if there is no filter list or the filter list accepts the object</returns>
        public bool Accept(IGeoObject toTest)
        {
            if (FilterList == null) return true;
            return FilterList.Accept(toTest);
        }
        /// <summary>
        /// The source point (the mouse position on the projection plane, see <see cref="SourcePoint"/>) as a 3d point
        /// in model coordinates. Together with the direction of the projection it defines the line perpendicular to
        /// the screen through the mouse position, along which snap points are searched.
        /// </summary>
        /// <value>
        /// The mouse position as a 3d point on the projection plane.
        /// </value>
        public GeoPoint SourcePoint3D
        {
            get { return Projection.ProjectionPlane.ToGlobal(SourcePoint); }
        }
        /// <summary>
        /// True if <see cref="SnapModes.SnapToIntersectionPoint"/> is set in <see cref="SnapMode"/>.
        /// </summary>
        public bool SnapToIntersectionPoint
        {
            get { return (SnapMode & SnapModes.SnapToIntersectionPoint) != 0; }
        }
        /// <summary>
        /// True if <see cref="SnapModes.SnapToDropPoint"/> is set in <see cref="SnapMode"/>. Requires a valid <see cref="BasePoint"/>.
        /// </summary>
        public bool SnapToDropPoint
        {
            get { return (SnapMode & SnapModes.SnapToDropPoint) != 0; }
        }
        /// <summary>
        /// True if <see cref="SnapModes.SnapToObjectPoint"/> is set in <see cref="SnapMode"/>.
        /// </summary>
        public bool SnapToObjectPoint
        {
            get { return (SnapMode & SnapModes.SnapToObjectPoint) != 0; }
        }
        /// <summary>
        /// True if <see cref="SnapModes.SnapToObjectSnapPoint"/> is set in <see cref="SnapMode"/>.
        /// </summary>
        public bool SnapToObjectSnapPoint
        {
            get { return (SnapMode & SnapModes.SnapToObjectSnapPoint) != 0; }
        }
        /// <summary>
        /// True if <see cref="SnapModes.SnapToObjectCenter"/> is set in <see cref="SnapMode"/>.
        /// </summary>
        public bool SnapToObjectCenter
        {
            get { return (SnapMode & SnapModes.SnapToObjectCenter) != 0; }
        }
        /// <summary>
        /// True if <see cref="SnapModes.SnapToTangentPoint"/> is set in <see cref="SnapMode"/>. Requires a valid <see cref="BasePoint"/>.
        /// </summary>
        public bool SnapToTangentPoint
        {
            get { return (SnapMode & SnapModes.SnapToTangentPoint) != 0; }
        }
        /// <summary>
        /// True if <see cref="SnapModes.SnapToGridPoint"/> is set in <see cref="SnapMode"/>.
        /// </summary>
        public bool SnapToGridPoint
        {
            get { return (SnapMode & SnapModes.SnapToGridPoint) != 0; }
        }
        /// <summary>
        /// True if <see cref="SnapModes.AdjustOrtho"/> is set in <see cref="SnapMode"/>. Requires a valid <see cref="BasePoint"/>.
        /// </summary>
        public bool AdjustOrtho
        {
            get { return (SnapMode & SnapModes.AdjustOrtho) != 0; }
        }
        /// <summary>
        /// True if <see cref="SnapModes.SnapToZero"/> is set in <see cref="SnapMode"/>.
        /// </summary>
        public bool SnapToZero
        {
            get { return (SnapMode & SnapModes.SnapToZero) != 0; }
        }
        /// <summary>
        /// True if <see cref="SnapModes.SnapToFaceSurface"/> is set in <see cref="SnapMode"/>.
        /// </summary>
        public bool SnapToFaceSurface
        {
            get { return (SnapMode & SnapModes.SnapToFaceSurface) != 0; }
        }
        /// <summary>
        /// Returns the priority of a snap mode: 1 for intersection points and object snap points, 2 for tangent points
        /// and perpendicular foot points, 3 for object centers, 4 for arbitrary object points, 5 for grid points, 6 for
        /// the orthogonal adjustment and 7 for everything else (including <see cref="DidSnapModes.DidNotSnap"/>).
        /// <see cref="Check"/> replaces the current snap point only by one with a lower or equal value.
        /// </summary>
        /// <param name="mode">The snap mode to rate</param>
        /// <returns>The priority; lower values win</returns>
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
