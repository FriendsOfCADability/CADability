using System.Diagnostics;
using CADability.Forms;
using System.IO.Compression;
using CADability.Attribute;
using CADability.Shapes;

namespace CADability.Tests
{
    [TestClass]
    public class ProjectTest
    {

        public TestContext TestContext { get; set; }

        [TestMethod]
        [DeploymentItem(@"Files/Dxf/square_100x100.dxf", nameof(import_dxf_square_succeds))]
        [DeploymentItem(@"Files/Dxf/square_100x100.png", nameof(import_dxf_square_succeds))]
        public void import_dxf_square_succeds()
        {
            var file = Path.Combine(this.TestContext.DeploymentDirectory, this.TestContext.TestName, "square_100x100.dxf");
            Assert.IsTrue(File.Exists(file));
            var bmpFile = Path.Combine(this.TestContext.DeploymentDirectory, this.TestContext.TestName, "square_100x100.png");
            Assert.IsTrue(File.Exists(bmpFile));

            var project = Project.ReadFromFile(file, "dxf");
            Assert.IsNotNull(project);
            var model = project.GetActiveModel();
            Assert.IsNotNull(model);
            var obj = Assert.That.Single(model.AllObjects);
            var polyline = Assert.That.IsInstanceOfType<GeoObject.Polyline>(obj);
            Assert.AreEqual(400, polyline.Length);


            using (var expected = (Bitmap)Image.FromFile(bmpFile))
            using (var actual = PaintToOpenGL.PaintToBitmap(model.AllObjects, GeoVector.ZAxis, 100, 100))
            {
                Assert.That.BitmapsAreEqual(expected, actual);
            }
        }
        [TestMethod]
        [DeploymentItem(@"Files/Step/issue101.stp", nameof(import_step_issue101_succeds))]
        [DeploymentItem(@"Files/Step/issue101.png", nameof(import_step_issue101_succeds))]
        public void import_step_issue101_succeds()
        {
            // cylinder.OutwardOriented throws an NotImplementedException
            // because there is a concret implementation
            //   public bool OutwardOriented => toCylinder.Determinant > 0;
            // and an explicit interface implementation
            //   bool ICylinder.OutwardOriented => throw new NotImplementedException();

            var file = Path.Combine(this.TestContext.DeploymentDirectory, this.TestContext.TestName, "issue101.stp");
            Assert.IsTrue(File.Exists(file));
            var bmpFile = Path.Combine(this.TestContext.DeploymentDirectory, this.TestContext.TestName, "issue101.png");
            Assert.IsTrue(File.Exists(bmpFile));

            var project = Project.ReadFromFile(file, "stp");
            Assert.IsNotNull(project);
            var model = project.GetActiveModel();
            Assert.IsNotNull(model);
            Assert.AreEqual(1, model.AllObjects.Count);

            var solid = Assert.That.IsInstanceOfType<GeoObject.Solid>(model.AllObjects[0]);
            var shell = solid.Shells[0];
            // get a cylinder (order faces and cylinders to always get the same entity)
            var cylinder = shell
                .Faces
                .OrderBy(x => x.Area)
                .Select(x => x.Surface)
                .OfType<GeoObject.ICylinder>()
                .OrderBy(x => x.Radius)
                .FirstOrDefault();
            Assert.IsTrue(cylinder.OutwardOriented);

            using (var expected = (Bitmap)Image.FromFile(bmpFile))
            using (var actual = PaintToOpenGL.PaintToBitmap(model.AllObjects, GeoVector.NullVector, 200, 200))
            {
                Assert.That.BitmapsAreEqual(expected, actual);
            }
        }

        [TestMethod]
        public void export_dxf_issue_129_succeds()
        {
            // in master@82ffb34 exporting a text object to txt does not set the location,
            // so all texts are all not in the correct location / orientation

            // create a simple project and add a text object
            var project = Project.CreateSimpleProject();
            var model = project.GetActiveModel();
            var expected = GeoObject.Text.Construct();
            expected.Font = "Arial";
            expected.TextString = "Test";
            expected.Location = new GeoPoint(50, 50);
            model.Add(expected);

            // export the project and load it again
            var fileName = this.TestContext.TestName + ".dxf";
            project.Export(fileName, "dxf");
            project = Project.ReadFromFile(fileName, "dxf");
            model = project.GetActiveModel();
            var actual = model.AllObjects.Cast<GeoObject.Text>().Single();

            // verify some values
            Assert.AreEqual(expected.Location, actual.Location);
            Assert.AreEqual(expected.TextString, actual.TextString);
            Assert.AreEqual(expected.Font, actual.Font);

        }

        [TestMethod]
        [DeploymentItem(@"Files/Dxf/issue143.dxf", nameof(import_dxf_issue143_succeds))]
        [DeploymentItem(@"Files/Dxf/issue143.png", nameof(import_dxf_issue143_succeds))]
        public void import_dxf_issue143_succeds()
        {
            var file = Path.Combine(this.TestContext.DeploymentDirectory, this.TestContext.TestName, "issue143.dxf");
            Assert.IsTrue(File.Exists(file));
            var bmpFile = Path.Combine(this.TestContext.DeploymentDirectory, this.TestContext.TestName, "issue143.png");
            // uncomment after file exists
            Assert.IsTrue(File.Exists(bmpFile));

            var project = Project.ReadFromFile(file, "dxf");
            Assert.IsNotNull(project);
            var model = project.GetActiveModel();
            Assert.IsNotNull(model);
            Assert.AreEqual(3, model.AllObjects.Count);

            var ellipse1 = Assert.That.IsInstanceOfType<GeoObject.Ellipse>(model.AllObjects[1]);
            Assert.IsTrue(ellipse1.HasValidData());
            var ellipse2 = Assert.That.IsInstanceOfType<GeoObject.Ellipse>(model.AllObjects[2]);
            Assert.IsTrue(ellipse2.HasValidData());

            using (var expected = (Bitmap)Image.FromFile(bmpFile))
            using (var actual = PaintToOpenGL.PaintToBitmap(model.AllObjects, GeoVector.NullVector, 200, 200))
            {
                // Uncomment once to generate bitmap for later comparison
                //actual.Save(bmpFile);
                Assert.That.BitmapsAreEqual(expected, actual);
            }
        }

        [TestMethod]
        [DeploymentItem(@"Files/Dxf/issue171.dxf", nameof(import_dxf_issue171_succeds))]
        public void import_dxf_issue171_succeds()
        {
            // DXF file with Codepage DOS850

            var file = Path.Combine(this.TestContext.DeploymentDirectory, this.TestContext.TestName, "issue171.dxf");
            Assert.IsTrue(File.Exists(file));

            var project = Project.ReadFromFile(file, "dxf");
            Assert.IsNotNull(project);
        }

        [TestMethod]
        [DeploymentItem(@"Files/Dxf/Z273.dxf", nameof(import_dxf_Z273_succeeds))]
        public void import_dxf_Z273_succeeds()
        {
            // AC1009 (R12) DXF with no *Paper_Space block — importing this used to throw
            // KeyNotFoundException when FillPaperSpace accessed the missing block record.
            var file = Path.Combine(this.TestContext.DeploymentDirectory, this.TestContext.TestName, "Z273.dxf");
            Assert.IsTrue(File.Exists(file));

            var project = Project.ReadFromFile(file, "dxf");
            Assert.IsNotNull(project);
            var model = project.GetActiveModel();
            Assert.IsNotNull(model);
            // 3 TEXT + 23 LINE + 7 CIRCLE = 33 entities
            Assert.AreEqual(33, model.AllObjects.Count);
        }

        [TestMethod]
        [DeploymentItem(@"Files/Dxf/BVH_Bona.dxf", nameof(import_dxf_BVH_Bona_succeeds))]
        public void import_dxf_BVH_Bona_succeeds()
        {
            // AC1024 DXF with 508 arcs, 34 LwPolylines, 41 lines, 11 splines, 3 MTexts.
            // Arc.StartAngle/EndAngle from ACadSharp are in radians; using Angle.Deg() on them
            // shrinks all angles by π/180 making arcs nearly invisible ("totally obscured").
            var file = Path.Combine(this.TestContext.DeploymentDirectory, this.TestContext.TestName, "BVH_Bona.dxf");
            Assert.IsTrue(File.Exists(file));

            var project = Project.ReadFromFile(file, "dxf");
            Assert.IsNotNull(project);
            var model = project.GetActiveModel();
            Assert.IsNotNull(model);

            // Verify the 508 arcs are imported as arcs (Ellipse with IsCircle==false)
            var arcs = model.AllObjects.Cast<GeoObject.IGeoObject>()
                .OfType<GeoObject.Ellipse>()
                .Where(e => !e.IsCircle)
                .ToList();
            Assert.AreEqual(508, arcs.Count);

            // Verify that arcs have sensible sweep angles (not near-zero due to radian/degree confusion)
            foreach (var arc in arcs)
                Assert.IsTrue(arc.SweepParameter > 0.001, $"Arc sweep {arc.SweepParameter} is near zero — angle unit bug?");
        }

        [TestMethod]
        public void import_dxf_arc_quarter_sweep_is_correct()
        {
            // Regression: Arc.StartAngle / EndAngle from ACadSharp are in RADIANS.
            // The old code called Angle.Deg() on them, which treats radians as degrees
            // and divides by 180/π — shrinking every arc by factor ~57.
            // A 90° arc (DXF: start=0°, end=90°) must import with SweepParameter = π/2,
            // not the bugged value of π/2 × π/180 ≈ 0.027 (a near-invisible sliver).
            const string dxf = @"  0
SECTION
  2
HEADER
  9
$ACADVER
  1
AC1009
  0
ENDSEC
  0
SECTION
  2
ENTITIES
  0
ARC
  8
0
 10
0.0
 20
0.0
 30
0.0
 40
100.0
 50
0.0
 51
90.0
  0
ENDSEC
  0
EOF
";
            var file = this.TestContext.TestName + ".dxf";
            File.WriteAllText(file, dxf);
            var model = Project.ReadFromFile(file, "dxf").GetActiveModel();
            var arc = Assert.That.IsInstanceOfType<GeoObject.Ellipse>(model.AllObjects[0]);
            Assert.AreEqual(100.0, arc.MajorRadius, 1e-6, "Radius");
            Assert.AreEqual(Math.PI / 2, arc.SweepParameter, 1e-4,
                "90° arc sweep must be π/2 rad; bug caused ≈0.027 rad");
        }

        [TestMethod]
        public void import_dxf_arc_crossing_zero_sweep_is_correct()
        {
            // An arc from 315° to 45° crosses 0° and sweeps 90° CCW.
            // With the angle-unit bug, ACadSharp's radian values are further shrunk so the
            // crossing-zero logic (sweep += 2π) doesn't trigger correctly either.
            const string dxf = @"  0
SECTION
  2
HEADER
  9
$ACADVER
  1
AC1009
  0
ENDSEC
  0
SECTION
  2
ENTITIES
  0
ARC
  8
0
 10
0.0
 20
0.0
 30
0.0
 40
50.0
 50
315.0
 51
45.0
  0
ENDSEC
  0
EOF
";
            var file = this.TestContext.TestName + ".dxf";
            File.WriteAllText(file, dxf);
            var model = Project.ReadFromFile(file, "dxf").GetActiveModel();
            var arc = Assert.That.IsInstanceOfType<GeoObject.Ellipse>(model.AllObjects[0]);
            Assert.AreEqual(50.0, arc.MajorRadius, 1e-6, "Radius");
            // 315°→45° CCW: sweep = 45 - 315 = -270, +360 = 90° = π/2 rad
            Assert.AreEqual(Math.PI / 2, arc.SweepParameter, 1e-4,
                "315°→45° arc sweep must be π/2 rad");
        }

        [TestMethod]
        public void import_dxf_text_rotation_correct()
        {
            // Regression: TextEntity.Rotation from ACadSharp is in RADIANS.
            // Old code called Angle.Deg() on it, making 90° text appear at ~1.57° (nearly horizontal).
            // A text at DXF rotation=90° must import with LineDirection ≈ (0, 1, 0).
            const string dxf = @"  0
SECTION
  2
HEADER
  9
$ACADVER
  1
AC1009
  0
ENDSEC
  0
SECTION
  2
ENTITIES
  0
TEXT
  8
0
 10
0.0
 20
0.0
 30
0.0
 40
10.0
  1
Test
 41
1.0
 50
90.0
  0
ENDSEC
  0
EOF
";
            var file = this.TestContext.TestName + ".dxf";
            File.WriteAllText(file, dxf);
            var model = Project.ReadFromFile(file, "dxf").GetActiveModel();
            var text = Assert.That.IsInstanceOfType<GeoObject.Text>(model.AllObjects[0]);
            var dir = text.LineDirection.Normalized;
            // 90° rotation: text flows upward → LineDirection ≈ (0, 1, 0)
            Assert.AreEqual(0.0, dir.x, 1e-3, "90° rotated text: LineDirection.x must be ≈ 0");
            Assert.AreEqual(1.0, dir.y, 1e-3, "90° rotated text: LineDirection.y must be ≈ 1");
        }

        [TestMethod]
        public void import_dxf_lwpolyline_cw_bulge_correct()
        {
            // Regression: BulgeToArc for CW arcs (bulge < 0) was flipping the plane normal
            // and passing ccw=false, which caused SetArcPlaneCenterStartEndPoint to produce
            // a -270° sweep (the long way round) instead of -90°.
            // An LWPOLYLINE with vertices (0,0)→(10,0) and bulge=-1 (CW semicircle) must
            // produce an arc with SweepParameter ≈ -π (180° CW).
            // bulge = tan(angle/4) = 1 → angle = 4*atan(1) = π → semicircle
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
 90
2
 70
0
 10
0.0
 20
0.0
 42
-1.0
 10
10.0
 20
0.0
  0
ENDSEC
  0
EOF
";
            var file = this.TestContext.TestName + ".dxf";
            File.WriteAllText(file, dxf);
            var model = Project.ReadFromFile(file, "dxf").GetActiveModel();
            Assert.AreEqual(1, model.AllObjects.Count);
            var arc = Assert.That.IsInstanceOfType<GeoObject.Ellipse>(model.AllObjects[0]);
            // bulge=-1 → included angle = π → radius = chord/2 / sin(π/2) = 5
            Assert.AreEqual(5.0, arc.MajorRadius, 1e-4, "Radius of CW semicircle");
            // SweepParameter must be negative (CW) and ≈ -π (180°), not -3π/2 (270°)
            Assert.IsTrue(arc.SweepParameter < 0, "CW arc must have negative SweepParameter");
            Assert.AreEqual(-Math.PI, arc.SweepParameter, 1e-4,
                "CW semicircle sweep must be -π; bug caused -3π/2 (270° the wrong way)");
        }

        [TestMethod]
        public void import_dxf_lwpolyline_cw_quarter_arc_correct()
        {
            // A CW quarter-circle: vertices (0,0)→(1,0) with bulge=-tan(π/8) ≈ -0.4142.
            // This produces a 90° CW arc (SweepParameter ≈ -π/2).
            // The bug caused a 270° CW arc (-3π/2) because the plane flip reversed start/end
            // angle ordering before the sweep direction forced the long way round.
            double bulge = -Math.Tan(Math.PI / 8); // -tan(22.5°) for a 90° arc
            string dxf = $@"  0
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
 90
2
 70
0
 10
0.0
 20
0.0
 42
{bulge:F6}
 10
2.0
 20
0.0
  0
ENDSEC
  0
EOF
";
            var file = this.TestContext.TestName + ".dxf";
            File.WriteAllText(file, dxf);
            var model = Project.ReadFromFile(file, "dxf").GetActiveModel();
            Assert.AreEqual(1, model.AllObjects.Count);
            var arc = Assert.That.IsInstanceOfType<GeoObject.Ellipse>(model.AllObjects[0]);
            Assert.IsTrue(arc.SweepParameter < 0, "CW arc must have negative SweepParameter");
            Assert.AreEqual(-Math.PI / 2, arc.SweepParameter, 1e-3,
                "CW quarter-arc sweep must be -π/2; bug caused -3π/2");
        }

        [TestMethod]
        [DeploymentItem(@"Files/Dxf/0583667_008.dxf", nameof(import_dxf_0583667_succeeds))]
        public void import_dxf_0583667_succeeds()
        {
            // AC1027 DXF with ACAD_TABLE entities (*T1/*T2/*T3 blocks) containing MTEXT
            // with non-default attachment points. Verifies the file imports without exceptions
            // and that the model space contains the expected inserted block objects.
            var file = Path.Combine(this.TestContext.DeploymentDirectory, this.TestContext.TestName, "0583667_008.dxf");
            Assert.IsTrue(File.Exists(file));

            var project = Project.ReadFromFile(file, "dxf");
            Assert.IsNotNull(project);
            var model = project.GetActiveModel();
            Assert.IsNotNull(model);
            // Model space contains: 5 INSERTs + 3 ACAD_TABLEs + 3 DIMENSIONs + 5 LINEs + 12 SPLINEs + 5 ARCs
            Assert.IsTrue(model.AllObjects.Count > 0, "Model must contain imported entities");
        }

        [TestMethod]
        public void import_dxf_mtext_topcenter_placement_correct()
        {
            // Regression: CreateMText ignored AttachmentPoint so all MTEXT rendered as
            // Left/Baseline. For table cells with attachment TopCenter (71=2) the text
            // appears shifted right and down of where it should be.
            // An MTEXT at (50,30) with attachment TopCenter must import with
            // LineAlignment=Center and Alignment=Top so the text is centered on x=50 and
            // its top edge is at y=30.
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
MTEXT
  8
0
 10
50.0
 20
30.0
 30
0.0
 40
5.0
 71
2
  1
Hello
  0
ENDSEC
  0
EOF
";
            var file = this.TestContext.TestName + ".dxf";
            File.WriteAllText(file, dxf);
            var model = Project.ReadFromFile(file, "dxf").GetActiveModel();
            var text = Assert.That.IsInstanceOfType<GeoObject.Text>(model.AllObjects[0]);
            // Anchor is at (50, 30): location must stay at that point
            Assert.AreEqual(50.0, text.Location.x, 1e-4, "MTEXT TopCenter: Location.x must be anchor x");
            Assert.AreEqual(30.0, text.Location.y, 1e-4, "MTEXT TopCenter: Location.y must be anchor y");
            Assert.AreEqual(GeoObject.Text.LineAlignMode.Center, text.LineAlignment,
                "MTEXT attach=2 (TopCenter) must import as LineAlignment=Center");
            Assert.AreEqual(GeoObject.Text.AlignMode.Top, text.Alignment,
                "MTEXT attach=2 (TopCenter) must import as Alignment=Top");
        }

        [TestMethod]
        public void import_dxf_mtext_bottomright_placement_correct()
        {
            // MTEXT with attachment BottomRight (71=9) must have Right+Bottom alignment.
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
MTEXT
  8
0
 10
100.0
 20
10.0
 30
0.0
 40
3.5
 71
9
  1
Test
  0
ENDSEC
  0
EOF
";
            var file = this.TestContext.TestName + ".dxf";
            File.WriteAllText(file, dxf);
            var model = Project.ReadFromFile(file, "dxf").GetActiveModel();
            var text = Assert.That.IsInstanceOfType<GeoObject.Text>(model.AllObjects[0]);
            Assert.AreEqual(GeoObject.Text.LineAlignMode.Right, text.LineAlignment,
                "MTEXT attach=9 (BottomRight) must import as LineAlignment=Right");
            Assert.AreEqual(GeoObject.Text.AlignMode.Bottom, text.Alignment,
                "MTEXT attach=9 (BottomRight) must import as Alignment=Bottom");
        }

        [TestMethod]
        public void import_dxf_spline_degenerate_does_not_crash()
        {
            // Regression: CreateSplineAsPolyline used `lines as ICollection<IGeoObject>` where
            // `lines` is `List<GeoObject.Line>`. C# generic interfaces are invariant, so the cast
            // returns null. Passing null to GeoObjectList(ICollection<IGeoObject>) throws
            // NullReferenceException at list.Count (GeoObjectList.cs:50).
            // A cubic SPLINE whose consecutive control points 1 and 2 are identical (distance 0 <
            // Precision.eps = 1e-6) triggers forcePolyline2D=true which calls CreateSplineAsPolyline.
            // The import must complete without throwing.
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
SPLINE
  8
0
 70
8
 71
3
 72
8
 73
4
 74
0
 40
0.0
 40
0.0
 40
0.0
 40
0.0
 40
1.0
 40
1.0
 40
1.0
 40
1.0
 10
0.0
 20
0.0
 30
0.0
 10
5.0
 20
5.0
 30
0.0
 10
5.0
 20
5.0
 30
0.0
 10
10.0
 20
0.0
 30
0.0
  0
ENDSEC
  0
EOF
";
            var file = this.TestContext.TestName + ".dxf";
            File.WriteAllText(file, dxf);
            var project = Project.ReadFromFile(file, "dxf");
            Assert.IsNotNull(project, "Import must not crash and must return a project");
            var model = project.GetActiveModel();
            Assert.IsNotNull(model);
            Assert.IsTrue(model.AllObjects.Count > 0,
                "Degenerate spline (duplicate consecutive control points) must import as a path");
        }

        [TestMethod]
        public void import_dxf_tolerance_fcf_imported_as_text()
        {
            // DXF TOLERANCE (AcDbFcf / Feature Control Frame) entities were silently dropped
            // because there was no case for ACadSharp.Entities.Tolerance in GeoObjectFromEntity.
            // They must now be imported as Text objects at the insertion point.
            // The FCF content "{\Fgdt;f}%%v2 CZ%%v%%vA%%v%%v%%v^J" should produce something like
            // "f|2 CZ||A|||" (GDT symbol letter, pipes as cell separators).
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
TOLERANCE
  8
0
100
AcDbEntity
  8
0
100
AcDbFcf
  3
Standard
 10
50.0
 20
30.0
 30
0.0
  1
{\Fgdt;f}%%v0.1%%v%%vA%%v%%v%%v
  0
ENDSEC
  0
EOF
";
            var file = this.TestContext.TestName + ".dxf";
            File.WriteAllText(file, dxf);
            var project = Project.ReadFromFile(file, "dxf");
            Assert.IsNotNull(project, "Import must not crash");
            var model = project.GetActiveModel();
            Assert.IsNotNull(model);
            Assert.IsTrue(model.AllObjects.Count > 0, "TOLERANCE entity must be imported as a text object");
            var text = Assert.That.IsInstanceOfType<GeoObject.Text>(model.AllObjects[0]);
            Assert.IsTrue(text.TextString.Contains("|"), "FCF text must use | as cell separator (%%v → |)");
        }

        [TestMethod]
        [DeploymentItem(@"Files/Step/issue153.stp", nameof(import_step_issue153_succeeds))]
        public void import_step_issue153_succeeds()
        {
            //The file issue153.stp is using Metres as unit. But it's imported as mm in CADability.

            var file = Path.Combine(this.TestContext.DeploymentDirectory, this.TestContext.TestName, "issue153.stp");
            Assert.IsTrue(File.Exists(file));

            var project = Project.ReadFromFile(file, "stp");
            Assert.IsNotNull(project);

            var allObjects = project.GetActiveModel().AllObjects;
            var solid = (CADability.GeoObject.Solid)allObjects[0];
            var vol = solid.Volume(0);

            //If this file is imported as mm instead of m the volume will be around 0.00055309963466116513
            //The real volumen should be around 553099.66263763292
            double rightVolume = 553099.66263763292;
            Debug.Assert((Math.Abs(vol - rightVolume) < Precision.eps));
        }
    }
}
