using CADability;
using CADability.GeoObject;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Path = System.IO.Path;

namespace CADability.Tests
{
    /// <summary>
    /// Tests for <see cref="CornerGeometry.TryComputeFillet"/>, the geometric core extracted from the interactive
    /// corner tools (CornerCurvesAction). The extraction lets us exercise the corner
    /// detection without the interactive harness. The file RoundOffTest.cdb.json contains six connected lines; only the
    /// corner near (52, 69, 0) currently rounds in the app, although several corners are geometrically valid.
    /// </summary>
    [TestClass]
    public class RoundOffTests
    {
        public TestContext TestContext { get; set; }

        private static string CdbFile([CallerFilePath] string thisFile = "")
            => Path.Combine(Path.GetDirectoryName(thisFile), "Files", "CDB", "RoundOffTest.cdb.json");

        // -------- synthetic happy-path cases (no file needed) --------

        private static Line MakeLine(GeoPoint a, GeoPoint b)
        {
            Line l = Line.Construct();
            l.StartPoint = a;
            l.EndPoint = b;
            return l;
        }

        [TestMethod]
        public void RightAngleCornerRounds()
        {
            // corner at (10,0,0): one leg back along -X, one leg up along +Y; pick inside that quadrant
            Line a = MakeLine(new GeoPoint(0, 0, 0), new GeoPoint(10, 0, 0));
            Line b = MakeLine(new GeoPoint(10, 0, 0), new GeoPoint(10, 10, 0));
            bool ok = CornerGeometry.TryComputeFillet(a, b, new GeoPoint(9, 1, 0), 2.0,
                Plane.XYPlane, out Ellipse arc, out _);
            Assert.IsTrue(ok, "right-angle corner should round");
            Assert.AreEqual(2.0, arc.Radius, 1e-6, "unexpected fillet radius");
        }

        [TestMethod]
        public void FilletIsTangentToBothLegs()
        {
            Line a = MakeLine(new GeoPoint(0, 0, 0), new GeoPoint(10, 0, 0));
            Line b = MakeLine(new GeoPoint(10, 0, 0), new GeoPoint(10, 10, 0));
            bool ok = CornerGeometry.TryComputeFillet(a, b, new GeoPoint(9, 1, 0), 2.0,
                Plane.XYPlane, out Ellipse arc, out GeoPoint corner);
            Assert.IsTrue(ok, "corner should round");
            Assert.AreEqual(2.0, arc.Radius, 1e-6, "unexpected radius");
            Assert.IsTrue(Precision.IsEqual(corner, new GeoPoint(10, 0, 0)), "corner should be the shared endpoint");
            // both arc endpoints are the tangent points; each must lie on one of the legs (within the segment)
            Assert.IsTrue(OnSegment(arc.StartPoint, a) || OnSegment(arc.StartPoint, b), "arc start not on a leg");
            Assert.IsTrue(OnSegment(arc.EndPoint, a) || OnSegment(arc.EndPoint, b), "arc end not on a leg");
            Assert.IsFalse(Precision.IsEqual(arc.StartPoint, arc.EndPoint), "the two tangent points must differ");
        }

        [TestMethod]
        public void AcuteCornerRoundsRegardlessOfPickSide()
        {
            // acute corner at the origin, both legs into the first quadrant
            Line a = MakeLine(new GeoPoint(0, 0, 0), new GeoPoint(10, 0, 0));
            Line b = MakeLine(new GeoPoint(0, 0, 0), new GeoPoint(10, 4, 0));

            // the fillet is chosen purely from the geometry (the inner angle), so the pick side does not matter:
            // a pick outside the acute wedge rounds just like one inside it, and yields the same fillet
            Assert.IsTrue(CornerGeometry.TryComputeFillet(a, b, new GeoPoint(5, -1, 0), 1.0,
                Plane.XYPlane, out Ellipse outsideArc, out _), "outside pick should still round");
            Assert.AreEqual(1.0, outsideArc.Radius, 1e-6, "unexpected radius");
            Assert.IsTrue(CornerGeometry.TryComputeFillet(a, b, new GeoPoint(5, 1, 0), 1.0,
                Plane.XYPlane, out Ellipse insideArc, out _), "inside pick should round");
            Assert.IsTrue(Precision.IsEqual(outsideArc.StartPoint, insideArc.StartPoint)
                || Precision.IsEqual(outsideArc.StartPoint, insideArc.EndPoint), "both picks should give the same fillet");
        }

        [TestMethod]
        public void RoundAllCornersOfClosedSquare()
        {
            // a closed square of four lines (10 x 10)
            List<ICurve> square = new List<ICurve>
            {
                MakeLine(new GeoPoint(0, 0, 0), new GeoPoint(10, 0, 0)),
                MakeLine(new GeoPoint(10, 0, 0), new GeoPoint(10, 10, 0)),
                MakeLine(new GeoPoint(10, 10, 0), new GeoPoint(0, 10, 0)),
                MakeLine(new GeoPoint(0, 10, 0), new GeoPoint(0, 0, 0)),
            };
            List<ICurve> parts = CornerGeometry.AllCorners(square, true, 2.0, CornerGeometry.Operation.Fillet, Plane.XYPlane);
            Assert.IsNotNull(parts, "the square should round");
            int arcs = parts.OfType<Ellipse>().Count();
            int lines = parts.OfType<Line>().Count();
            Assert.AreEqual(4, arcs, "a square has four corners, so four fillet arcs are expected");
            Assert.AreEqual(4, lines, "the four sides remain, each shortened at both ends");
            foreach (Ellipse arc in parts.OfType<Ellipse>())
                Assert.AreEqual(2.0, arc.Radius, 1e-6, "unexpected fillet radius");
            // every side is shortened to 10 - 2*2 = 6
            foreach (Line line in parts.OfType<Line>())
                Assert.AreEqual(6.0, line.Length, 1e-6, "each side should be shortened by the radius at both ends");
        }

        [TestMethod]
        public void RoundAllCornersOfOpenChainKeepsEnds()
        {
            // an open chain of three lines has two inner corners; the two outer ends stay unrounded
            List<ICurve> chain = new List<ICurve>
            {
                MakeLine(new GeoPoint(0, 0, 0), new GeoPoint(10, 0, 0)),
                MakeLine(new GeoPoint(10, 0, 0), new GeoPoint(10, 10, 0)),
                MakeLine(new GeoPoint(10, 10, 0), new GeoPoint(20, 10, 0)),
            };
            List<ICurve> parts = CornerGeometry.AllCorners(chain, false, 2.0, CornerGeometry.Operation.Fillet, Plane.XYPlane);
            Assert.IsNotNull(parts);
            Assert.AreEqual(2, parts.OfType<Ellipse>().Count(), "an open three-line chain has two inner corners");
            Assert.AreEqual(3, parts.OfType<Line>().Count(), "all three segments remain");
        }

        [TestMethod]
        public void ChamferRightAngleHasRequestedEdgeLength()
        {
            // right angle at (10,0,0); a symmetric chamfer of edge length 3 cuts 3/sqrt(2) on each leg
            Line a = MakeLine(new GeoPoint(0, 0, 0), new GeoPoint(10, 0, 0));
            Line b = MakeLine(new GeoPoint(10, 0, 0), new GeoPoint(10, 10, 0));
            bool ok = CornerGeometry.TryComputeChamfer(a, b, new GeoPoint(9, 1, 0), 3.0,
                Plane.XYPlane, out Line chamfer, out GeoPoint corner);
            Assert.IsTrue(ok, "right angle should chamfer");
            Assert.AreEqual(3.0, chamfer.Length, 1e-6, "the chamfer edge should have the requested length");
            Assert.IsTrue(Precision.IsEqual(corner, new GeoPoint(10, 0, 0)), "unexpected corner");
            // symmetric: the two cut points are equidistant from the corner
            double dA = chamfer.StartPoint | corner;
            double dB = chamfer.EndPoint | corner;
            Assert.AreEqual(dA, dB, 1e-6, "a symmetric chamfer cuts both legs equally");
            // both cut points lie on the legs, within the segments
            Assert.IsTrue(OnSegment(chamfer.StartPoint, a) || OnSegment(chamfer.StartPoint, b), "cut point not on a leg");
            Assert.IsTrue(OnSegment(chamfer.EndPoint, a) || OnSegment(chamfer.EndPoint, b), "cut point not on a leg");
        }

        [TestMethod]
        public void ChamferAllCornersOfClosedSquare()
        {
            List<ICurve> square = new List<ICurve>
            {
                MakeLine(new GeoPoint(0, 0, 0), new GeoPoint(10, 0, 0)),
                MakeLine(new GeoPoint(10, 0, 0), new GeoPoint(10, 10, 0)),
                MakeLine(new GeoPoint(10, 10, 0), new GeoPoint(0, 10, 0)),
                MakeLine(new GeoPoint(0, 10, 0), new GeoPoint(0, 0, 0)),
            };
            List<ICurve> parts = CornerGeometry.AllCorners(square, true, 2.0, CornerGeometry.Operation.Chamfer, Plane.XYPlane);
            Assert.IsNotNull(parts, "the square should chamfer");
            Assert.AreEqual(4, parts.OfType<Line>().Count(l => Math.Abs(l.Length - 2.0) < 1e-6), "four chamfer edges of length 2 expected");
            Assert.AreEqual(8, parts.OfType<Line>().Count(), "four sides plus four chamfer edges");
        }

        private static bool OnSegment(GeoPoint p, ICurve curve)
        {
            double t = curve.PositionOf(p);
            if (t < -1e-6 || t > 1.0 + 1e-6) return false;
            return (p | curve.PointAt(t)) < 1e-6;
        }

        [TestMethod]
        public void AcuteAndObtuseCornersRound()
        {
            // obtuse corner
            Line a = MakeLine(new GeoPoint(0, 0, 0), new GeoPoint(10, 0, 0));
            Line b = MakeLine(new GeoPoint(10, 0, 0), new GeoPoint(18, 6, 0));
            Assert.IsTrue(CornerGeometry.TryComputeFillet(a, b, new GeoPoint(9.5, 0.5, 0), 1.5,
                Plane.XYPlane, out _, out _), "obtuse corner should round");

            // acute corner
            Line c = MakeLine(new GeoPoint(0, 0, 0), new GeoPoint(10, 0, 0));
            Line d = MakeLine(new GeoPoint(10, 0, 0), new GeoPoint(2, 4, 0));
            Assert.IsTrue(CornerGeometry.TryComputeFillet(c, d, new GeoPoint(8, 0.5, 0), 1.0,
                Plane.XYPlane, out _, out _), "acute corner should round");
        }

        // -------- data-driven exploration over the supplied test file --------

        [TestMethod]
        public void RoundOffCornersFromFile()
        {
            string file = CdbFile();
            Assert.IsTrue(System.IO.File.Exists(file), $"test file not found: {file}");
            Project project = Project.ReadFromFile(file, "cdb");
            Assert.IsNotNull(project, "could not read project");
            Model model = project.GetActiveModel();
            Assert.IsNotNull(model);

            List<Line> lines = model.AllObjects.OfType<Line>().ToList();
            Assert.AreEqual(6, lines.Count, "expected six lines in the test file");

            const double radius = 3.0;
            StringBuilder report = new StringBuilder();
            report.AppendLine();
            int roundable = 0;
            bool knownGoodCornerRounds = false;

            for (int i = 0; i < lines.Count; i++)
            {
                for (int j = i + 1; j < lines.Count; j++)
                {
                    if (!SharedVertex(lines[i], lines[j], out GeoPoint corner)) continue;

                    // pick 1: exactly at the corner vertex
                    bool okVertex = CornerGeometry.TryComputeFillet(lines[i], lines[j], corner, radius,
                        Plane.XYPlane, out _, out _);

                    // pick 2: offset into the corner interior along the angle bisector (simulates a real mouse pick)
                    GeoPoint pickInside = corner + 0.5 * InsideBisector(lines[i], lines[j], corner);
                    bool okInside = CornerGeometry.TryComputeFillet(lines[i], lines[j], pickInside, radius,
                        Plane.XYPlane, out _, out _);

                    if (okVertex) roundable++;
                    if (okVertex && Precision.IsEqual(corner, new GeoPoint(51.93661971830986, 68.90140845070425, 0.0)))
                        knownGoodCornerRounds = true;
                    report.AppendLine($"corner ({corner.x,7:F2},{corner.y,7:F2},{corner.z,7:F2})  lines {i},{j}  -> "
                        + $"vertexPick={(okVertex ? "ROUND" : "no")}, insidePick={(okInside ? "ROUND" : "no")}");
                }
            }
            report.AppendLine($"total roundable corners: {roundable}");
            TestContext.WriteLine(report.ToString());
            System.Diagnostics.Trace.WriteLine(report.ToString());

            // The geometry core rounds every geometrically valid corner when fed the two adjacent lines directly;
            // this guards it against regressions.
            Assert.IsTrue(knownGoodCornerRounds, "the known-good corner near (52,69,0) should round\n" + report);
            Assert.AreEqual(6, roundable, "all six corners are geometrically valid and should round\n" + report);
        }

        // Direction from the shared corner into the interior of the corner (normalized bisector of the two legs).
        private static GeoVector InsideBisector(Line a, Line b, GeoPoint corner)
        {
            GeoPoint farA = Precision.IsEqual(a.StartPoint, corner) ? a.EndPoint : a.StartPoint;
            GeoPoint farB = Precision.IsEqual(b.StartPoint, corner) ? b.EndPoint : b.StartPoint;
            GeoVector da = (farA - corner).Normalized;
            GeoVector db = (farB - corner).Normalized;
            GeoVector bisector = da + db;
            return bisector.Length > 1e-9 ? bisector.Normalized : da;
        }

        private static bool SharedVertex(Line a, Line b, out GeoPoint shared)
        {
            GeoPoint[] pa = { a.StartPoint, a.EndPoint };
            GeoPoint[] pb = { b.StartPoint, b.EndPoint };
            foreach (GeoPoint x in pa)
                foreach (GeoPoint y in pb)
                    if (Precision.IsEqual(x, y)) { shared = x; return true; }
            shared = GeoPoint.Origin;
            return false;
        }
    }
}
