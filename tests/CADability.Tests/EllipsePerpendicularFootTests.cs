using CADability.Curve2D;

namespace CADability.Tests
{
    /// <summary>
    /// Tests for <see cref="Ellipse2D.PerpendicularFoot(GeoPoint2D)"/> and <see cref="EllipseArc2D.PerpendicularFoot(GeoPoint2D)"/>
    /// (issue #557): the triangulation based search returned points that are not foot points on flat ellipses and missed some.
    /// Every returned point is checked against the parametric form c + cos(t)·major + sin(t)·minor, and the number of foot points
    /// against the sign changes of (P(t) − p)·P'(t), independent of the code under test.
    /// </summary>
    [TestClass]
    public class EllipsePerpendicularFootTests
    {
        /// <summary>The parameter t of a point q of the ellipse, and how far q is from the ellipse (|α² + β² − 1|).</summary>
        private static double ParameterOf(GeoPoint2D c, GeoVector2D major, GeoVector2D minor, GeoPoint2D q, out double offEllipse)
        {
            double alpha = ((q - c) * major) / (major * major);
            double beta = ((q - c) * minor) / (minor * minor);
            offEllipse = Math.Abs(alpha * alpha + beta * beta - 1.0);
            return Math.Atan2(beta, alpha);
        }

        /// <summary>The sine of the angle between q − p and the normal of the ellipse at q.</summary>
        private static double PerpendicularityError(GeoVector2D major, GeoVector2D minor, double t, GeoPoint2D q, GeoPoint2D p)
        {
            GeoVector2D tangent = -Math.Sin(t) * major + Math.Cos(t) * minor;
            GeoVector2D d = q - p;
            if (d.Length == 0.0) return 0.0;
            return Math.Abs(d * tangent) / (d.Length * tangent.Length);
        }

        /// <summary>The number of foot points with parameter t0 ≤ t &lt; t1 (sign changes of (P(t) − p)·P'(t)).</summary>
        private static int ReferenceCount(GeoPoint2D c, GeoVector2D major, GeoVector2D minor, double t0, double t1, GeoPoint2D p)
        {
            Func<double, double> f = t => ((c + Math.Cos(t) * major + Math.Sin(t) * minor) - p) * (-Math.Sin(t) * major + Math.Cos(t) * minor);
            const int n = 100000;
            int res = 0;
            double prev = f(t0);
            for (int i = 1; i <= n; i++)
            {
                double cur = f(t0 + (t1 - t0) * i / n);
                if (Math.Sign(cur) != Math.Sign(prev)) res++;
                prev = cur;
            }
            return res;
        }

        private static void CheckFeet(GeoPoint2D[] feet, GeoPoint2D c, GeoVector2D major, GeoVector2D minor, GeoPoint2D p, string context)
        {
            foreach (GeoPoint2D q in feet)
            {
                double t = ParameterOf(c, major, minor, q, out double offEllipse);
                Assert.IsTrue(offEllipse < 1e-9, $"{context}: {q} is not on the ellipse ({offEllipse})");
                double err = PerpendicularityError(major, minor, t, q, p);
                Assert.IsTrue(err < 1e-7, $"{context}: {q} is not a foot point, sine of the angle error {err}");
            }
        }

        [TestMethod]
        public void FlatEllipseFromIssue()
        {
            GeoVector2D major = new GeoVector2D(14.114367070202455, 0), minor = new GeoVector2D(0, 0.004619964910175692);
            GeoPoint2D p = new GeoPoint2D(6.8146403912799025, -0.006205605628115422);
            GeoPoint2D[] feet = new Ellipse2D(GeoPoint2D.Origin, major, minor).PerpendicularFoot(p);
            CheckFeet(feet, GeoPoint2D.Origin, major, minor, p, "issue");
            Assert.AreEqual(ReferenceCount(GeoPoint2D.Origin, major, minor, 0, 2 * Math.PI, p), feet.Length);
        }

        [TestMethod]
        public void PointsOnTheAxes()
        {
            GeoVector2D major = new GeoVector2D(10, 0), minor = new GeoVector2D(0, 2);
            Ellipse2D ellipse = new Ellipse2D(GeoPoint2D.Origin, major, minor);
            // on the major axis inside the evolute (|x| < 9.6) there are four foot points, outside two; the same on the minor
            // axis with |y| < 48; the center has the four vertices
            var cases = new[]
            {
                (p: new GeoPoint2D(3, 0), count: 4), (p: new GeoPoint2D(-9.9, 0), count: 2), (p: new GeoPoint2D(12, 0), count: 2),
                (p: new GeoPoint2D(0, 1), count: 4), (p: new GeoPoint2D(0, -30), count: 4), (p: new GeoPoint2D(0, 50), count: 2),
                (p: GeoPoint2D.Origin, count: 4),
            };
            foreach (var cs in cases)
            {
                GeoPoint2D[] feet = ellipse.PerpendicularFoot(cs.p);
                CheckFeet(feet, GeoPoint2D.Origin, major, minor, cs.p, cs.p.ToString());
                Assert.AreEqual(cs.count, feet.Length, cs.p.ToString());
            }
        }

        [TestMethod]
        public void RandomEllipsesAndArcs()
        {
            Random rnd = new Random(557);
            for (int i = 0; i < 100; i++)
            {
                double a = Math.Pow(10, rnd.NextDouble() * 3);
                double ratio = i % 4 == 0 ? 1.0 - rnd.NextDouble() * 0.01 : Math.Pow(10, -rnd.NextDouble() * 4);
                double angle = rnd.NextDouble() * 2 * Math.PI;
                GeoVector2D major = a * new GeoVector2D(Math.Cos(angle), Math.Sin(angle));
                GeoVector2D minor = a * ratio * new GeoVector2D(-Math.Sin(angle), Math.Cos(angle));
                if (rnd.Next(2) == 0) minor = -minor;
                if (i % 5 == 0) (major, minor) = (minor, major); // the first axis is the shorter one
                GeoPoint2D c = new GeoPoint2D(rnd.NextDouble() * 200 - 100, rnd.NextDouble() * 200 - 100);
                GeoPoint2D p = c + 3 * (rnd.NextDouble() - 0.5) * major + 3 * (rnd.NextDouble() - 0.5) * minor;
                string context = $"case {i}: a {a}, ratio {ratio}, point {p}";

                GeoPoint2D[] feet = new Ellipse2D(c, major, minor).PerpendicularFoot(p);
                CheckFeet(feet, c, major, minor, p, "ellipse, " + context);
                Assert.AreEqual(ReferenceCount(c, major, minor, 0, 2 * Math.PI, p), feet.Length, "ellipse, " + context);

                // an arc in either orientation keeps the foot points inside of it
                double t0 = rnd.NextDouble() * 2 * Math.PI;
                double sweep = (rnd.NextDouble() * 1.9 + 0.05) * Math.PI;
                double t1 = rnd.Next(2) == 0 ? t0 + sweep : t0 - sweep;
                EllipseArc2D arc = EllipseArc2D.Create(c, major, minor, c + Math.Cos(t0) * major + Math.Sin(t0) * minor, c + Math.Cos(t1) * major + Math.Sin(t1) * minor, t1 > t0);
                GeoPoint2D[] arcFeet = arc.PerpendicularFoot(p);
                CheckFeet(arcFeet, c, major, minor, p, "arc, " + context);
                Assert.AreEqual(ReferenceCount(c, major, minor, Math.Min(t0, t1), Math.Max(t0, t1), p), arcFeet.Length, $"arc {t0}..{t1}, " + context);
            }
        }
    }
}
