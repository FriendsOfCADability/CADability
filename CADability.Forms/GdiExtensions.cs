using CADability.Curve2D;
using CADability.Shapes;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace CADability.Forms
{
    /// <summary>
    /// Converts CADability geometry to GDI+ (<see cref="GraphicsPath"/>, <see cref="Matrix"/>). These conversions used to be
    /// members of the geometry types in the kernel (<see cref="ICurve2D.AddToGraphicsPath"/>, <see cref="Border.AddToGraphicsPath"/>,
    /// <see cref="CompoundShape.CreateGraphicsPath"/>, <see cref="ModOp2D.Matrix2D"/>). GDI+ is only available on Windows, so they
    /// live here, next to the other WinForms code, and the kernel stays platform independent (issue #412).
    /// </summary>
    public static class GdiExtensions
    {
        /// <summary>
        /// Appends <paramref name="curve"/> to <paramref name="path"/>. Lines, arcs and circles are added exactly, ellipses and
        /// B-splines as an approximation through points on the curve.
        /// </summary>
        /// <param name="path">The graphics path to append to</param>
        /// <param name="curve">The curve to add</param>
        /// <param name="forward">true: add in the direction of the curve, false: add reversed</param>
        public static void AddCurve2D(this GraphicsPath path, ICurve2D curve, bool forward)
        {
            // Exact type checks: a derived class may have overridden the obsolete ICurve2D.AddToGraphicsPath.
            Type type = curve.GetType();
            if (type == typeof(Line2D)) AddLine(path, (Line2D)curve, forward);
            else if (type == typeof(Arc2D)) AddArc(path, (Arc2D)curve, forward);
            else if (type == typeof(Circle2D)) AddCircle(path, (Circle2D)curve, forward);
            else if (type == typeof(EllipseArc2D)) AddEllipseArc(path, (EllipseArc2D)curve, forward);
            else if (type == typeof(Ellipse2D)) AddEllipse(path, (Ellipse2D)curve, forward);
            else if (type == typeof(Polyline2D)) AddPolyline(path, (Polyline2D)curve, forward);
            else if (type == typeof(BSpline2D)) AddBSpline(path, (BSpline2D)curve, forward);
            else if (type == typeof(Path2D)) AddPath(path, (Path2D)curve, forward);
            else
            {
                // Other curves keep their own implementation as long as the kernel has it (1.x).
#pragma warning disable CS0618 // Type or member is obsolete
                curve.AddToGraphicsPath(path, forward);
#pragma warning restore CS0618
            }
        }
        /// <summary>
        /// Appends the segments of <paramref name="border"/> to <paramref name="path"/>, e.g. for filling or clipping with
        /// System.Drawing. With <paramref name="counterClockWise"/> false the segments are added in reverse order and direction,
        /// which is how <see cref="AddShape(GraphicsPath, SimpleShape)"/> adds the holes so that they stay unfilled.
        /// </summary>
        /// <param name="path">The graphics path to append to</param>
        /// <param name="border">The border to add</param>
        /// <param name="counterClockWise">true: add in the direction of the border, false: add reversed</param>
        public static void AddBorder(this GraphicsPath path, Border border, bool counterClockWise)
        {
            if (counterClockWise)
            {
                for (int i = 0; i < border.Count; ++i) path.AddCurve2D(border[i], true);
            }
            else
            {
                for (int i = border.Count - 1; i >= 0; --i) path.AddCurve2D(border[i], false);
            }
        }
        /// <summary>
        /// Appends <paramref name="shape"/> to <paramref name="path"/>: the outline counterclockwise and the holes clockwise, so
        /// filling the path leaves the holes empty.
        /// </summary>
        /// <param name="path">The graphics path to append to</param>
        /// <param name="shape">The shape to add</param>
        public static void AddShape(this GraphicsPath path, SimpleShape shape)
        {
            path.AddBorder(shape.Outline, true);
            Border[] holes = shape.Holes;
            for (int i = 0; i < holes.Length; ++i) path.AddBorder(holes[i], false);
        }
        /// <summary>
        /// Appends all parts of <paramref name="shape"/> to <paramref name="path"/>, see <see cref="AddShape(GraphicsPath, SimpleShape)"/>.
        /// </summary>
        /// <param name="path">The graphics path to append to</param>
        /// <param name="shape">The shape to add</param>
        public static void AddShape(this GraphicsPath path, CompoundShape shape)
        {
            SimpleShape[] simpleShapes = shape.SimpleShapes;
            for (int i = 0; i < simpleShapes.Length; ++i) path.AddShape(simpleShapes[i]);
        }
        /// <summary>
        /// Converts <paramref name="shape"/> to a new <see cref="GraphicsPath"/> for drawing or clipping with System.Drawing,
        /// see <see cref="AddShape(GraphicsPath, CompoundShape)"/>.
        /// </summary>
        /// <param name="shape">The shape to convert</param>
        /// <returns>The graphics path</returns>
        public static GraphicsPath ToGraphicsPath(this CompoundShape shape)
        {
            GraphicsPath res = new GraphicsPath();
            res.AddShape(shape);
            return res;
        }
        /// <summary>
        /// Converts <paramref name="m"/> to a GDI+ <see cref="Matrix"/> with the same coefficients (in single precision), e.g. to
        /// apply it when drawing with GDI+.
        /// </summary>
        /// <param name="m">The modification to convert</param>
        /// <returns>The equivalent matrix</returns>
        public static Matrix ToGdiMatrix(this ModOp2D m)
        {
            return new Matrix((float)m[0, 0], (float)m[1, 0], (float)m[0, 1], (float)m[1, 1], (float)m[0, 2], (float)m[1, 2]);
        }

        private static void AddLine(GraphicsPath path, Line2D line, bool forward)
        {
            GeoPoint2D sp = forward ? line.StartPoint : line.EndPoint;
            GeoPoint2D ep = forward ? line.EndPoint : line.StartPoint;
            path.AddLine((float)sp.x, (float)sp.y, (float)ep.x, (float)ep.y);
        }
        private static void AddArc(GraphicsPath path, Arc2D arc, bool forward)
        {
            Angle start = arc.StartParameter;
            SweepAngle sweep = arc.SweepAngle;
            float startDegree = (float)(forward ? start.Degree : (start + sweep).Degree);
            float sweepDegree = (float)(forward ? sweep.Degree : -sweep.Degree);
            path.AddArc((float)(arc.Center.x - arc.Radius), (float)(arc.Center.y - arc.Radius), (float)(2 * arc.Radius), (float)(2 * arc.Radius),
                startDegree, sweepDegree);
        }
        private static void AddCircle(GraphicsPath path, Circle2D circle, bool forward)
        {
            // AddArc and not AddEllipse, so that the direction is kept, which matters for FillMode.Winding
            path.AddArc((float)(circle.Center.x - circle.Radius), (float)(circle.Center.y - circle.Radius), (float)(2 * circle.Radius), (float)(2 * circle.Radius),
                0.0f, forward ? 360.0f : -360.0f);
        }
        private static void AddEllipse(GraphicsPath path, Ellipse2D ellipse, bool forward)
        {
            AddCurveThroughPoints(path, ellipse, 36, forward);
        }
        private static void AddEllipseArc(GraphicsPath path, EllipseArc2D ellipseArc, bool forward)
        {
            // about one point every 10°
            int n = (int)Math.Round(Math.Abs(ellipseArc.axisSweep) / Math.PI * 18);
            AddCurveThroughPoints(path, ellipseArc, Math.Max(n, 2), forward);
        }
        private static void AddCurveThroughPoints(GraphicsPath path, ICurve2D curve, int n, bool forward)
        {
            PointF[] points = new PointF[n + 1];
            double step = 1.0 / n;
            for (int i = 0; i < n; ++i) points[i] = curve.PointAt(i * step).PointF;
            points[n] = curve.PointAt(1.0).PointF;
            if (!forward) Array.Reverse(points);
            path.AddCurve(points);
        }
        private static void AddPolyline(GraphicsPath path, Polyline2D polyline, bool forward)
        {
            GeoPoint2D[] vertex = polyline.Vertex;
            if (forward)
            {
                for (int i = 1; i < vertex.Length; ++i)
                    path.AddLine((float)vertex[i - 1].x, (float)vertex[i - 1].y, (float)vertex[i].x, (float)vertex[i].y);
            }
            else
            {
                for (int i = vertex.Length - 1; i > 0; --i)
                    path.AddLine((float)vertex[i].x, (float)vertex[i].y, (float)vertex[i - 1].x, (float)vertex[i - 1].y);
            }
        }
        private static void AddBSpline(GraphicsPath path, BSpline2D bspline, bool forward)
        {
            // a polyline through points on the curve, four per pole
            int n = bspline.Poles.Length * 4 + 2;
            PointF[] points = new PointF[n];
            for (int i = 0; i < n; ++i) points[i] = bspline.PointAt((double)i / (n - 1)).PointF;
            if (!forward) Array.Reverse(points);
            path.AddLines(points);
        }
        private static void AddPath(GraphicsPath path, Path2D path2D, bool forward)
        {
            ICurve2D[] subCurves = path2D.SubCurves;
            if (forward)
            {
                for (int i = 0; i < subCurves.Length; ++i) path.AddCurve2D(subCurves[i], true);
            }
            else
            {
                for (int i = subCurves.Length - 1; i >= 0; --i) path.AddCurve2D(subCurves[i], false);
            }
        }
    }
}
