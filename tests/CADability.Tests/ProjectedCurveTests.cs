using CADability.Curve2D;
using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// Tests of <see cref="ProjectedCurve"/> on a periodic surface, where the uv values of a point are only defined up to
    /// whole periods. The curves are arcs of a horizontal circle at z = 5 on a cylinder of radius 10 around the z-axis,
    /// where u is the angle and v is z, so every point of the 2d curve is known exactly.
    /// </summary>
    [TestClass]
    public class ProjectedCurveTests
    {
        private static CylindricalSurface Cylinder()
        {
            return new CylindricalSurface(GeoPoint.Origin, 10 * GeoVector.XAxis, 10 * GeoVector.YAxis, GeoVector.ZAxis);
        }

        /// <summary>The arc at z = 5 from the angle <paramref name="start"/> through <paramref name="sweep"/>.</summary>
        private static Ellipse Arc(double start, double sweep)
        {
            Ellipse arc = Ellipse.Construct();
            arc.SetArcPlaneCenterRadiusAngles(new Plane(new GeoPoint(0, 0, 5), GeoVector.XAxis, GeoVector.YAxis), new GeoPoint(0, 0, 5), 10, start, sweep);
            return arc;
        }

        private static void AssertContinuous(ICurve2D c2d, double startU, double endU, string what)
        {
            Assert.AreEqual(startU, c2d.StartPoint.x, 1e-9, what + ": start");
            Assert.AreEqual(endU, c2d.EndPoint.x, 1e-9, what + ": end");
            double previous = c2d.PointAt(0.0).x;
            for (int i = 1; i <= 40; i++)
            {
                GeoPoint2D uv = c2d.PointAt(i / 40.0);
                Assert.AreEqual(5.0, uv.y, 1e-9, what + ": v");
                Assert.AreEqual(startU + (endU - startU) * i / 40.0, uv.x, 1e-6, what + ": u at " + (i / 40.0) + ", after " + previous);
                previous = uv.x;
            }
        }

        /// <summary>
        /// The curve lies in [pi, 2 pi], and the domain handed to it is [0, pi]. Every point used to be moved next to the
        /// center of that domain on its own, so the half beyond 3/2 pi jumped back by a period.
        /// </summary>
        [TestMethod]
        public void a_curve_beside_its_domain_runs_on_continuously()
        {
            ProjectedCurve c2d = new ProjectedCurve(Arc(Math.PI, Math.PI), Cylinder(), true, new BoundingRect(0, 0, Math.PI, 18));
            AssertContinuous(c2d, Math.PI, 2 * Math.PI, "arc from pi to 2 pi");
        }

        /// <summary>
        /// The curve is fixed to its periods when it is made. Setting the domain of the surface afterwards changes what
        /// PositionOf returns, but not where the curve is - Shell.GetOffsetParts moves the curves of a sphere and only then
        /// makes the face, which sets the domain.
        /// </summary>
        [TestMethod]
        public void the_curve_stays_where_it_is_when_the_domain_of_the_surface_is_set_later()
        {
            CylindricalSurface cylinder = Cylinder();
            ProjectedCurve c2d = new ProjectedCurve(Arc(Math.PI / 2, Math.PI / 2), cylinder, true, BoundingRect.EmptyBoundingRect);
            c2d.Move(-2 * Math.PI, 0);
            AssertContinuous(c2d, Math.PI / 2 - 2 * Math.PI, Math.PI - 2 * Math.PI, "moved by a period");
            cylinder.usedArea = new BoundingRect(2 * Math.PI, 0, 4 * Math.PI, 18);
            c2d.Reverse();
            c2d.Reverse(); // computes the curve anew
            AssertContinuous(c2d, Math.PI / 2 - 2 * Math.PI, Math.PI - 2 * Math.PI, "after the domain was set");
        }

        /// <summary>
        /// The curve runs through u = 2 pi, and a point on it is given a period earlier, as a face whose domain is [0, 2 pi]
        /// gives it. Found in 2d it was not on the curve; BooleanOperation then lost the intersection and a union of a
        /// cone and a ball fell apart into the two operands (OffsetQuadricSurfaces).
        /// </summary>
        [TestMethod]
        public void a_point_given_in_another_period_is_found_on_the_curve()
        {
            ProjectedCurve c2d = new ProjectedCurve(Arc(1.5 * Math.PI, Math.PI), Cylinder(), true, BoundingRect.EmptyBoundingRect);
            AssertContinuous(c2d, 1.5 * Math.PI, 2.5 * Math.PI, "arc through 2 pi");
            Assert.AreEqual(0.75, c2d.PositionOf(new GeoPoint2D(0.25 * Math.PI, 5)), 1e-9, "the point at 9/4 pi, given as 1/4 pi");
        }

        [TestMethod]
        public void reversed_and_trimmed_curves_stay_in_the_same_periods()
        {
            ProjectedCurve c2d = new ProjectedCurve(Arc(1.5 * Math.PI, Math.PI), Cylinder(), true, BoundingRect.EmptyBoundingRect);
            c2d.Move(2 * Math.PI, 0);
            AssertContinuous(c2d, 3.5 * Math.PI, 4.5 * Math.PI, "moved by a period");
            ICurve2D piece = c2d.Trim(0.2, 0.6);
            AssertContinuous(piece, 3.7 * Math.PI, 4.1 * Math.PI, "a trimmed piece");
            ICurve2D[] parts = c2d.Split(0.5);
            AssertContinuous(parts[0], 3.5 * Math.PI, 4.0 * Math.PI, "the first part");
            AssertContinuous(parts[1], 4.0 * Math.PI, 4.5 * Math.PI, "the second part");
            c2d.Reverse();
            AssertContinuous(c2d, 4.5 * Math.PI, 3.5 * Math.PI, "reversed");
            AssertContinuous(c2d.Clone(), 4.5 * Math.PI, 3.5 * Math.PI, "a clone");
        }
    }
}
