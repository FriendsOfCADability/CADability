using CADability.Curve2D;

namespace CADability.Tests
{
    /// <summary>
    /// Tests for <see cref="EllipseArc2D.MinDistance(GeoPoint2D)"/> and <see cref="Ellipse2D.MinDistance(GeoPoint2D)"/> (issue #556):
    /// the foot point was taken from the triangulation based perpendicular foot search, which is imprecise on flat ellipses, so
    /// the distance to a flat elliptical arc came out too large. The expected values are computed here from the parametric form
    /// c + cos(t)·major + sin(t)·minor, independent of the code under test.
    /// </summary>
    [TestClass]
    public class EllipseMinDistanceTests
    {
        private static GeoPoint2D At(GeoPoint2D c, GeoVector2D major, GeoVector2D minor, double t)
        {
            return c + Math.Cos(t) * major + Math.Sin(t) * minor;
        }

        /// <summary>
        /// The distance of <paramref name="p"/> from the ellipse points with parameter t0 ≤ t ≤ t1 (or t1 ≤ t ≤ t0): dense sampling,
        /// every local minimum of the samples refined with a ternary search, and the end points.
        /// </summary>
        private static double Reference(GeoPoint2D c, GeoVector2D major, GeoVector2D minor, double t0, double t1, GeoPoint2D p)
        {
            Func<double, double> dist = t => At(c, major, minor, t) | p;
            const int n = 200000;
            double res = Math.Min(dist(t0), dist(t1));
            double prev = dist(t0), cur = dist(t0 + (t1 - t0) / n);
            for (int i = 1; i < n; i++)
            {
                double next = dist(t0 + (t1 - t0) * (i + 1) / n);
                if (cur <= prev && cur <= next)
                {
                    double lo = t0 + (t1 - t0) * (i - 1) / n, hi = t0 + (t1 - t0) * (i + 1) / n;
                    for (int k = 0; k < 200; k++)
                    {
                        double m1 = lo + (hi - lo) / 3, m2 = hi - (hi - lo) / 3;
                        if (dist(m1) < dist(m2)) hi = m2;
                        else lo = m1;
                    }
                    res = Math.Min(res, dist((lo + hi) / 2));
                }
                prev = cur;
                cur = next;
            }
            return res;
        }

        /// <summary>The arc from parameter t0 to t1, counterclockwise with respect to the axes when t1 &gt; t0.</summary>
        private static EllipseArc2D MakeArc(GeoPoint2D c, GeoVector2D major, GeoVector2D minor, double t0, double t1)
        {
            EllipseArc2D arc = EllipseArc2D.Create(c, major, minor, At(c, major, minor, t0), At(c, major, minor, t1), t1 > t0);
            // make sure the arc covers the parameter range the reference uses
            double size = major.Length + minor.Length + Math.Abs(c.x) + Math.Abs(c.y);
            Assert.AreEqual(0.0, arc.StartPoint | At(c, major, minor, t0), 1e-9 * size, "start point");
            Assert.AreEqual(0.0, arc.EndPoint | At(c, major, minor, t1), 1e-9 * size, "end point");
            Assert.AreEqual(0.0, arc.PointAt(0.5) | At(c, major, minor, (t0 + t1) / 2), 1e-9 * size, "mid point");
            return arc;
        }

        private static double Deg(double degrees) { return degrees * Math.PI / 180.0; }

        [TestMethod]
        public void FlatArc_ChordMidpoint_FromIssue()
        {
            GeoPoint2D c = GeoPoint2D.Origin;
            GeoVector2D major = new GeoVector2D(100000, 0);
            GeoVector2D minor = new GeoVector2D(0, 1);
            EllipseArc2D arc = MakeArc(c, major, minor, Deg(89), Deg(91));
            GeoPoint2D q = new GeoPoint2D((arc.StartPoint.x + arc.EndPoint.x) / 2, (arc.StartPoint.y + arc.EndPoint.y) / 2);
            // q lies on the minor axis, directly below the top of the ellipse (0, 1), which is inside the arc
            double exact = 1.0 - Math.Sin(Deg(89));
            Assert.AreEqual(exact, arc.MinDistance(q), exact * 1e-6, "arc");
            Assert.AreEqual(exact, new Ellipse2D(c, major, minor).MinDistance(q), exact * 1e-6, "ellipse");
            Assert.AreEqual(exact, Reference(c, major, minor, Deg(89), Deg(91), q), exact * 1e-6, "reference");
        }

        [TestMethod]
        public void ArcsFromIssueTable()
        {
            var cases = new[]
            {
                (a: 1000.0, b: 10.0, from: 80.0, to: 100.0, p: new GeoPoint2D(100, 9.95087)),
                (a: 1000.0, b: 10.0, from: 60.0, to: 120.0, p: new GeoPoint2D(100, 9.95087)),
                (a: 100.0, b: 50.0, from: 30.0, to: 150.0, p: new GeoPoint2D(10, 49.75037)),
            };
            foreach (var cs in cases)
            {
                GeoVector2D major = new GeoVector2D(cs.a, 0), minor = new GeoVector2D(0, cs.b);
                EllipseArc2D arc = MakeArc(GeoPoint2D.Origin, major, minor, Deg(cs.from), Deg(cs.to));
                double expected = Reference(GeoPoint2D.Origin, major, minor, Deg(cs.from), Deg(cs.to), cs.p);
                string context = $"{cs.a}/{cs.b}, {cs.from}..{cs.to}";
                Assert.AreEqual(expected, arc.MinDistance(cs.p), expected * 1e-6, context);
                Assert.AreEqual(expected, new Ellipse2D(GeoPoint2D.Origin, major, minor).MinDistance(cs.p), expected * 1e-6, context);
            }
        }

        [TestMethod]
        public void NearestPointIsAnEndPoint()
        {
            GeoPoint2D c = new GeoPoint2D(5, -3);
            GeoVector2D major = new GeoVector2D(300, 400); // length 500, rotated
            GeoVector2D minor = new GeoVector2D(-0.8, 0.6); // length 1
            foreach (bool forward in new[] { true, false })
            {
                double t0 = forward ? Deg(80) : Deg(100), t1 = forward ? Deg(100) : Deg(80);
                EllipseArc2D arc = MakeArc(c, major, minor, t0, t1);
                // points beyond either end, outside and inside of the ellipse, near the curve
                foreach (double t in new[] { Deg(60), Deg(79), Deg(101), Deg(130) })
                {
                    GeoPoint2D onEllipse = At(c, major, minor, t);
                    foreach (double factor in new[] { 1.0, 1.0 + 1e-6, 1.0 - 1e-6, 1.001, 0.999 })
                    {
                        GeoPoint2D p = c + factor * (onEllipse - c);
                        double expected = Math.Min(p | arc.StartPoint, p | arc.EndPoint);
                        // the reference confirms that the closest point is an end point
                        Assert.AreEqual(expected, Reference(c, major, minor, t0, t1, p), 1e-9 * expected + 1e-12, "reference");
                        Assert.AreEqual(expected, arc.MinDistance(p), 1e-9 * expected + 1e-12, $"forward {forward}, t {t}, factor {factor}");
                    }
                }
            }
        }

        [TestMethod]
        public void SecondLocalMinimumInsideTheArc()
        {
            // the closest point of the full ellipse is the top, but the arc only covers the bottom half: the closest point of the
            // arc is the local minimum near the bottom, not an end point
            GeoVector2D major = new GeoVector2D(10, 0), minor = new GeoVector2D(0, 1);
            GeoPoint2D p = new GeoPoint2D(0.5, 0.2);
            foreach (bool forward in new[] { true, false })
            {
                double t0 = forward ? Deg(200) : Deg(340), t1 = forward ? Deg(340) : Deg(200);
                EllipseArc2D arc = MakeArc(GeoPoint2D.Origin, major, minor, t0, t1);
                double expected = Reference(GeoPoint2D.Origin, major, minor, t0, t1, p);
                Assert.IsTrue(expected < 1.21 && expected > 1.19, $"reference {expected}");
                Assert.AreEqual(expected, arc.MinDistance(p), 1e-9, $"forward {forward}");
                Assert.AreEqual(Reference(GeoPoint2D.Origin, major, minor, 0, 2 * Math.PI, p), new Ellipse2D(GeoPoint2D.Origin, major, minor).MinDistance(p), 1e-9);
            }
        }

        [TestMethod]
        public void PointsOnTheAxes()
        {
            // the degenerate cases of the foot point computation: points on the major or minor axis, inside and outside of the evolute
            GeoVector2D major = new GeoVector2D(10, 0), minor = new GeoVector2D(0, 2);
            Ellipse2D ellipse = new Ellipse2D(GeoPoint2D.Origin, major, minor);
            EllipseArc2D lower = MakeArc(GeoPoint2D.Origin, major, minor, Deg(190), Deg(350));
            GeoPoint2D[] points =
            {
                GeoPoint2D.Origin, new GeoPoint2D(3, 0), new GeoPoint2D(-9.9, 0), new GeoPoint2D(12, 0), new GeoPoint2D(0, 1),
                new GeoPoint2D(0, -1.5), new GeoPoint2D(0, 30), new GeoPoint2D(0, -30),
            };
            foreach (GeoPoint2D p in points)
            {
                Assert.AreEqual(Reference(GeoPoint2D.Origin, major, minor, 0, 2 * Math.PI, p), ellipse.MinDistance(p), 1e-9, $"ellipse, {p}");
                Assert.AreEqual(Reference(GeoPoint2D.Origin, major, minor, Deg(190), Deg(350), p), lower.MinDistance(p), 1e-9, $"lower arc, {p}");
            }
        }

        [TestMethod]
        public void RandomEllipsesAndArcs()
        {
            // flat and round ellipses, rotated, with left- and right-handed axes, arcs in both orientations, points inside, outside
            // and very close to the curve
            Random rnd = new Random(556);
            for (int i = 0; i < 150; i++)
            {
                double a = Math.Pow(10, rnd.NextDouble() * 4 - 1);
                double ratio = i % 3 == 0 ? 1.0 - rnd.NextDouble() * 0.01 : Math.Pow(10, -rnd.NextDouble() * 5);
                double angle = rnd.NextDouble() * 2 * Math.PI;
                GeoVector2D major = a * new GeoVector2D(Math.Cos(angle), Math.Sin(angle));
                GeoVector2D minor = a * ratio * new GeoVector2D(-Math.Sin(angle), Math.Cos(angle));
                if (rnd.Next(2) == 0) minor = -minor;
                if (i % 5 == 0) (major, minor) = (minor, major); // the first axis is the shorter one
                GeoPoint2D c = new GeoPoint2D(rnd.NextDouble() * 200 - 100, rnd.NextDouble() * 200 - 100);
                double t0 = rnd.NextDouble() * 2 * Math.PI;
                double sweep = (rnd.NextDouble() * 1.9 + 0.05) * Math.PI;
                double t1 = rnd.Next(2) == 0 ? t0 + sweep : t0 - sweep;
                EllipseArc2D arc = MakeArc(c, major, minor, t0, t1);
                Ellipse2D ellipse = new Ellipse2D(c, major, minor);

                GeoPoint2D p;
                switch (i % 4)
                {
                    case 0: // inside
                        p = c + (rnd.NextDouble() - 0.5) * major + (rnd.NextDouble() - 0.5) * minor;
                        break;
                    case 1: // outside, far away
                        p = c + 3 * (rnd.NextDouble() - 0.5) * major + 3 * (rnd.NextDouble() - 0.5) * minor + a * new GeoVector2D(rnd.NextDouble(), rnd.NextDouble());
                        break;
                    default: // close to the curve
                        p = At(c, major, minor, rnd.NextDouble() * 2 * Math.PI) + a * Math.Pow(10, -rnd.NextDouble() * 7) * new GeoVector2D(rnd.NextDouble() - 0.5, rnd.NextDouble() - 0.5);
                        break;
                }
                double expectedArc = Reference(c, major, minor, t0, t1, p);
                double expectedEllipse = Reference(c, major, minor, 0, 2 * Math.PI, p);
                double tolerance = 1e-10 * a;
                string context = $"case {i}: a {a}, ratio {ratio}, arc {t0}..{t1}, point {p}";
                Assert.AreEqual(expectedArc, arc.MinDistance(p), tolerance + 1e-6 * expectedArc, "arc, " + context);
                Assert.AreEqual(expectedEllipse, ellipse.MinDistance(p), tolerance + 1e-6 * expectedEllipse, "ellipse, " + context);
            }
        }
    }
}
