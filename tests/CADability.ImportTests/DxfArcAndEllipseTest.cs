using System;
using System.Globalization;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CADability;
using CADability.GeoObject;

namespace CADability.ImportTests
{
    /// <summary>
    /// Regressions for two DXF defects reported from the field:
    /// 1. A polyline vertex bulge of |bulge| &gt; 1 (an arc of more than 180°, e.g. a keyhole
    ///    contour whose partial circle spans 287°) placed the arc center on the wrong side of
    ///    the chord, so the arc collapsed to its complement on the mirrored center.
    /// 2. Exporting a clockwise elliptical arc wrote the flipped normal while keeping the
    ///    parameters, which mirrors the arc across its major axis (DXF derives the minor axis
    ///    as Normal × MajorAxis). Logos made of elliptical arcs came out shattered.
    /// </summary>
    [TestClass]
    public class DxfArcAndEllipseTest
    {
        public TestContext TestContext { get; set; }

        // --- 1. bulge import -----------------------------------------------------------------

        // Old-style POLYLINE/VERTEX (the entity the customer file contains). One segment from
        // (10,0) to (0,10) with bulge tan(270°/4) = 1+√2: a counterclockwise 270° arc with
        // radius 10 around (10,10); its midpoint is (10+10·cos45°, 10+10·sin45°).
        // The closing segment back to (10,0) is a line.
        [TestMethod]
        public void import_dxf_polyline_bulge_over_half_circle()
        {
            const string dxf = @"  0
SECTION
  2
ENTITIES
  0
POLYLINE
  8
0
 66
1
 70
1
  0
VERTEX
  8
0
 10
10.0
 20
0.0
 30
0.0
 42
2.414213562373095
  0
VERTEX
  8
0
 10
0.0
 20
10.0
 30
0.0
  0
SEQEND
  0
ENDSEC
  0
EOF
";
            var model = ImportDxf(dxf);
            Assert.AreEqual(1, model.AllObjects.Count);
            var path = model.AllObjects[0] as GeoObject.Path;
            Assert.IsNotNull(path, "POLYLINE should import as a Path");
            Ellipse arc = null;
            foreach (ICurve curve in path.Curves)
                if (curve is Ellipse e) arc = e;
            Assert.IsNotNull(arc, "the bulge segment should import as an arc");

            Assert.AreEqual(10.0, arc.Radius, 1e-8, "arc radius");
            Assert.AreEqual(0.0, arc.Center | new GeoPoint(10.0, 10.0, 0.0), 1e-8,
                "arc center: an arc of more than 180° has its center on the other side of the chord");
            Assert.AreEqual(1.5 * Math.PI, Math.Abs(arc.SweepParameter), 1e-8, "arc sweep");
            double s = 10.0 * Math.Sqrt(0.5);
            Assert.AreEqual(0.0, (arc as ICurve).PointAt(0.5) | new GeoPoint(10.0 + s, 10.0 + s, 0.0), 1e-8,
                "arc midpoint (the bulge apex)");
        }

        // The clockwise twin (negative bulge, LWPOLYLINE this time): 270° from (10,0) to
        // (0,10) around (0,0), midpoint at (-10·cos45°, -10·sin45°).
        [TestMethod]
        public void import_dxf_lwpolyline_negative_bulge_over_half_circle()
        {
            const string dxf = @"  0
SECTION
  2
HEADER
  9
$ACADVER
  1
AC1015
  0
ENDSEC
  0
SECTION
  2
ENTITIES
  0
LWPOLYLINE
  8
0
100
AcDbEntity
100
AcDbPolyline
 90
2
 70
1
 10
10.0
 20
0.0
 42
-2.414213562373095
 10
0.0
 20
10.0
  0
ENDSEC
  0
EOF
";
            var model = ImportDxf(dxf);
            Assert.AreEqual(1, model.AllObjects.Count);
            var path = model.AllObjects[0] as GeoObject.Path;
            Assert.IsNotNull(path, "LWPOLYLINE should import as a Path");
            Ellipse arc = null;
            foreach (ICurve curve in path.Curves)
                if (curve is Ellipse e) arc = e;
            Assert.IsNotNull(arc, "the bulge segment should import as an arc");

            Assert.AreEqual(10.0, arc.Radius, 1e-8, "arc radius");
            Assert.AreEqual(0.0, arc.Center | new GeoPoint(0.0, 0.0, 0.0), 1e-8, "arc center");
            Assert.AreEqual(1.5 * Math.PI, Math.Abs(arc.SweepParameter), 1e-8, "arc sweep");
            double s = 10.0 * Math.Sqrt(0.5);
            Assert.AreEqual(0.0, (arc as ICurve).PointAt(0.5) | new GeoPoint(-s, -s, 0.0), 1e-8,
                "arc midpoint (the bulge apex)");
        }

        // Arcs up to 180° were correct before the fix and have to stay so: 90° fillet,
        // counterclockwise from (10,0) to (0,10) around (0,0), midpoint on the diagonal.
        [TestMethod]
        public void import_dxf_lwpolyline_small_bulge_still_correct()
        {
            const string dxf = @"  0
SECTION
  2
HEADER
  9
$ACADVER
  1
AC1015
  0
ENDSEC
  0
SECTION
  2
ENTITIES
  0
LWPOLYLINE
  8
0
100
AcDbEntity
100
AcDbPolyline
 90
2
 70
1
 10
10.0
 20
0.0
 42
0.4142135623730951
 10
0.0
 20
10.0
  0
ENDSEC
  0
EOF
";
            var model = ImportDxf(dxf);
            Assert.AreEqual(1, model.AllObjects.Count);
            var path = model.AllObjects[0] as GeoObject.Path;
            Assert.IsNotNull(path);
            Ellipse arc = null;
            foreach (ICurve curve in path.Curves)
                if (curve is Ellipse e) arc = e;
            Assert.IsNotNull(arc);

            Assert.AreEqual(10.0, arc.Radius, 1e-8, "arc radius");
            Assert.AreEqual(0.0, arc.Center | new GeoPoint(0.0, 0.0, 0.0), 1e-8, "arc center");
            Assert.AreEqual(0.5 * Math.PI, Math.Abs(arc.SweepParameter), 1e-8, "arc sweep");
            double s = 10.0 * Math.Sqrt(0.5);
            Assert.AreEqual(0.0, (arc as ICurve).PointAt(0.5) | new GeoPoint(s, s, 0.0), 1e-8,
                "arc midpoint");
        }

        // --- 2. elliptical arc export --------------------------------------------------------

        // A clockwise elliptical arc (the orientation LogiCal produces when it reverses contour
        // curves) must come out of the export covering the same points, evaluated with plain
        // DXF ELLIPSE semantics: P(t) = C + cos(t)·Major + sin(t)·(N × Major)·ratio.
        [TestMethod]
        public void export_dxf_clockwise_ellipse_arc_is_not_mirrored()
        {
            Ellipse elli = MakeEllipseArc(majorX: 8, majorY: 3, ratio: 0.4, startParameter: 0.7, sweepParameter: 1.1);
            (elli as ICurve).Reverse(); // clockwise now

            DxfEllipse written = ExportSingleEllipse(elli);

            Assert.IsTrue(written.Nz > 0.0,
                "the ellipse normal must stay +Z; the flipped normal mirrors the arc across the major axis");
            AssertSameArc(elli, written);
        }

        // The counterclockwise case was correct before and has to stay so.
        [TestMethod]
        public void export_dxf_counterclockwise_ellipse_arc_roundtrips()
        {
            Ellipse elli = MakeEllipseArc(majorX: 8, majorY: 3, ratio: 0.4, startParameter: 0.7, sweepParameter: 1.1);

            DxfEllipse written = ExportSingleEllipse(elli);

            Assert.IsTrue(written.Nz > 0.0, "normal stays +Z");
            AssertSameArc(elli, written);
        }

        // A CADability ellipse may carry the longer radius on DirectionY. DXF requires the true
        // major axis in group 11 and RadiusRatio ≤ 1, otherwise readers reject or distort the
        // entity; the export has to swap the axes (shifting the parameters by -π/2).
        [TestMethod]
        public void export_dxf_ellipse_with_larger_minor_radius_writes_valid_ratio()
        {
            Ellipse elli = Ellipse.Construct();
            elli.SetEllipseCenterAxis(new GeoPoint(-4, 2, 0), new GeoVector(3, 0, 0), new GeoVector(0, 7, 0));
            elli.StartParameter = 0.4;
            elli.SweepParameter = 1.3;
            Assert.IsTrue(elli.MinorRadius > elli.MajorRadius, "test setup: longer radius on DirectionY");

            DxfEllipse written = ExportSingleEllipse(elli);

            Assert.IsTrue(written.Ratio <= 1.0 + 1e-12, "RadiusRatio must not exceed 1, was " + written.Ratio);
            AssertSameArc(elli, written);
        }

        // --- helpers -------------------------------------------------------------------------

        private Model ImportDxf(string dxf)
        {
            string file = this.TestContext.TestName + ".dxf";
            File.WriteAllText(file, dxf);
            var project = Project.ReadFromFile(file, "dxf");
            Assert.IsNotNull(project);
            var model = project.GetActiveModel();
            Assert.IsNotNull(model);
            return model;
        }

        private static Ellipse MakeEllipseArc(double majorX, double majorY, double ratio, double startParameter, double sweepParameter)
        {
            GeoVector major = new GeoVector(majorX, majorY, 0);
            GeoVector minor = GeoVector.ZAxis ^ major;
            minor.Norm();
            minor = (major.Length * ratio) * minor;
            Ellipse elli = Ellipse.Construct();
            elli.SetEllipseCenterAxis(new GeoPoint(10, 5, 0), major, minor);
            elli.StartParameter = startParameter;
            elli.SweepParameter = sweepParameter;
            return elli;
        }

        private DxfEllipse ExportSingleEllipse(Ellipse elli)
        {
            Project project = Project.CreateSimpleProject();
            project.GetModel(0).Add(elli);
            string file = this.TestContext.TestName + ".dxf";
            Assert.IsTrue(project.Export(file, "dxf"), "export must succeed");
            DxfEllipse written = ReadFirstEllipse(file);
            Assert.IsNotNull(written, "exported file must contain an ELLIPSE entity");
            return written;
        }

        // The written arc covers the same point set as the original curve (start, end and the
        // arc midpoint, which flips to the other side of the major axis when mirrored).
        private static void AssertSameArc(Ellipse original, DxfEllipse written)
        {
            ICurve curve = original;
            GeoPoint start = curve.StartPoint, end = curve.EndPoint, mid = curve.PointAt(0.5);
            GeoPoint2D ws = written.PointAt(written.P1);
            GeoPoint2D we = written.PointAt(written.P2);
            GeoPoint2D wm = written.PointAt(0.5 * (written.P1 + written.P2));
            double ends = Math.Min(Dist(start, ws) + Dist(end, we), Dist(start, we) + Dist(end, ws));
            Assert.AreEqual(0.0, ends, 1e-8, "start/end points of the written ellipse arc");
            Assert.AreEqual(0.0, Dist(mid, wm), 1e-8, "arc midpoint of the written ellipse arc (mirror check)");
        }

        private static double Dist(GeoPoint a, GeoPoint2D b)
        {
            return Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y) + a.z * a.z);
        }

        /// <summary>The ELLIPSE entity as written to the file, evaluated with DXF semantics.</summary>
        private sealed class DxfEllipse
        {
            public double Cx, Cy, Mx, My, Nz = 1.0, Ratio, P1, P2;

            public GeoPoint2D PointAt(double t)
            {
                // minor axis direction = Normal × MajorAxis (normalized · minor length);
                // for a normal of (0,0,nz) that is (-nz·My, nz·Mx) · ratio
                double ux = -Nz * My * Ratio;
                double uy = Nz * Mx * Ratio;
                return new GeoPoint2D(Cx + Math.Cos(t) * Mx + Math.Sin(t) * ux,
                                      Cy + Math.Cos(t) * My + Math.Sin(t) * uy);
            }
        }

        private static DxfEllipse ReadFirstEllipse(string file)
        {
            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                if (lines[i].Trim() != "0" || lines[i + 1].Trim() != "ELLIPSE") continue;
                var e = new DxfEllipse();
                for (int j = i + 2; j + 1 < lines.Length; j += 2)
                {
                    string code = lines[j].Trim();
                    if (code == "0") break;
                    if (!double.TryParse(lines[j + 1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)) continue;
                    switch (code)
                    {
                        case "10": e.Cx = value; break;
                        case "20": e.Cy = value; break;
                        case "11": e.Mx = value; break;
                        case "21": e.My = value; break;
                        case "230": e.Nz = value; break;
                        case "40": e.Ratio = value; break;
                        case "41": e.P1 = value; break;
                        case "42": e.P2 = value; break;
                    }
                }
                return e;
            }
            return null;
        }
    }
}
