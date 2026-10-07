using System;
using System.Globalization;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CADability;
using CADability.GeoObject;

namespace CADability.ImportTests
{
    /// <summary>
    /// Regression for issue #356: circles and arcs that were rotated (or flipped) out of the
    /// XY plane came back from a DXF export at the wrong position. DXF reads ARC and CIRCLE
    /// centers in the OCS that the entity normal spans, but the exporter wrote them in world
    /// coordinates. A flip turns the normal into (0,0,-1), whose OCS X axis is (-1,0,0), so
    /// the re-imported center ended up mirrored about the Y axis.
    /// </summary>
    [TestClass]
    public class DxfOcsExportTest
    {
        public TestContext TestContext { get; set; }

        // Flipping a circle by 180° about the X axis gives it the normal (0,0,-1).
        [TestMethod]
        public void export_dxf_flipped_circle_keeps_position()
        {
            Ellipse circle = Ellipse.Construct();
            circle.SetCirclePlaneCenterRadius(Plane.XYPlane, new GeoPoint(10, 5, 2), 3);
            circle.Modify(ModOp.Rotate(GeoPoint.Origin, GeoVector.XAxis, new SweepAngle(Math.PI)));
            Assert.IsTrue(circle.Plane.Normal.z < 0, "test setup: the flip must turn the normal down");

            string file = Export(circle);

            // Plain DXF semantics, independent of CADability's importer: with a normal of
            // (0,0,-1) the OCS axes are X = (-1,0,0), Y = (0,1,0), Z = (0,0,-1).
            DxfCenter written = ReadFirstCenter(file, "CIRCLE");
            Assert.IsNotNull(written, "exported file must contain a CIRCLE entity");
            Assert.AreEqual(-1.0, written.Nz, 1e-12, "normal");
            GeoPoint c = circle.Center; // (10,-5,-2)
            Assert.AreEqual(-c.x, written.X, 1e-8, "OCS x of the center");
            Assert.AreEqual(c.y, written.Y, 1e-8, "OCS y of the center");
            Assert.AreEqual(-c.z, written.Z, 1e-8, "OCS z (elevation) of the center");

            Ellipse back = ReimportSingle(file);
            Assert.AreEqual(0.0, back.Center | circle.Center, 1e-8, "re-imported circle center");
            Assert.AreEqual(circle.Radius, back.Radius, 1e-8, "re-imported circle radius");
        }

        // The same for arcs, flipped about the Y axis, in both orientations.
        [TestMethod]
        public void export_dxf_flipped_arc_keeps_position()
        {
            foreach (bool reverse in new[] { false, true })
            {
                Ellipse arc = MakeArc(new GeoPoint(10, 5, 0), 3, 0.3, 1.2);
                if (reverse) (arc as ICurve).Reverse();
                arc.Modify(ModOp.Rotate(GeoPoint.Origin, GeoVector.YAxis, new SweepAngle(Math.PI)));

                string file = Export(arc);
                DxfCenter written = ReadFirstCenter(file, "ARC");
                Assert.IsNotNull(written, "exported file must contain an ARC entity");
                Assert.AreEqual(-1.0, written.Nz, 1e-12, "normal");
                Assert.AreEqual(-arc.Center.x, written.X, 1e-8, "OCS x of the arc center");

                AssertSameArc(arc, ReimportSingle(file));
            }
        }

        // An arbitrary rotation leaves a normal that is neither +Z nor -Z, so all three OCS
        // axes differ from the world axes.
        [TestMethod]
        public void export_dxf_tilted_circle_and_arc_keep_position()
        {
            ModOp tilt = ModOp.Rotate(new GeoPoint(2, 3, 4), new GeoVector(1, 1, 0.3), new SweepAngle(0.65));

            Ellipse circle = Ellipse.Construct();
            circle.SetCirclePlaneCenterRadius(Plane.XYPlane, new GeoPoint(-7, 4, 1), 2.5);
            circle.Modify(tilt);
            Ellipse circleBack = ReimportSingle(Export(circle));
            Assert.AreEqual(0.0, circleBack.Center | circle.Center, 1e-8, "re-imported circle center");
            Assert.AreEqual(0.0, (circleBack.Plane.Normal ^ circle.Plane.Normal).Length, 1e-8,
                "re-imported circle plane");

            Ellipse arc = MakeArc(new GeoPoint(6, -2, 1), 4, 2.0, 2.5);
            arc.Modify(tilt);
            AssertSameArc(arc, ReimportSingle(Export(arc)));
        }

        // --- helpers -------------------------------------------------------------------------

        private static Ellipse MakeArc(GeoPoint center, double radius, double start, double sweep)
        {
            Ellipse arc = Ellipse.Construct();
            arc.SetArcPlaneCenterRadiusAngles(Plane.XYPlane, center, radius, start, sweep);
            return arc;
        }

        private string Export(IGeoObject geoObject)
        {
            Project project = Project.CreateSimpleProject();
            project.GetModel(0).Add(geoObject.Clone());
            string file = this.TestContext.TestName + ".dxf";
            Assert.IsTrue(project.Export(file, "dxf"), "export must succeed");
            return file;
        }

        private static Ellipse ReimportSingle(string file)
        {
            Project project = Project.ReadFromFile(file, "dxf");
            Assert.IsNotNull(project);
            Model model = project.GetActiveModel();
            Assert.AreEqual(1, model.AllObjects.Count, "one entity expected after re-import");
            Ellipse e = model.AllObjects[0] as Ellipse;
            Assert.IsNotNull(e, "the entity must re-import as an Ellipse");
            return e;
        }

        // Clockwise arcs are exported as their counterclockwise twin, so the direction may
        // come back reversed; the point set (start, end and midpoint) must be the same.
        private static void AssertSameArc(Ellipse original, Ellipse back)
        {
            ICurve o = original, b = back;
            double ends = Math.Min((o.StartPoint | b.StartPoint) + (o.EndPoint | b.EndPoint),
                                   (o.StartPoint | b.EndPoint) + (o.EndPoint | b.StartPoint));
            Assert.AreEqual(0.0, back.Center | original.Center, 1e-8, "re-imported arc center");
            Assert.AreEqual(0.0, ends, 1e-8, "re-imported arc start/end points");
            Assert.AreEqual(0.0, o.PointAt(0.5) | b.PointAt(0.5), 1e-8, "re-imported arc midpoint");
        }

        /// <summary>Group codes 10/20/30 and 230 of an entity as written to the file.</summary>
        private sealed class DxfCenter
        {
            public double X, Y, Z, Nz = 1.0;
        }

        private static DxfCenter ReadFirstCenter(string file, string entityName)
        {
            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                if (lines[i].Trim() != "0" || lines[i + 1].Trim() != entityName) continue;
                var c = new DxfCenter();
                for (int j = i + 2; j + 1 < lines.Length; j += 2)
                {
                    string code = lines[j].Trim();
                    if (code == "0") break;
                    if (!double.TryParse(lines[j + 1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)) continue;
                    switch (code)
                    {
                        case "10": c.X = value; break;
                        case "20": c.Y = value; break;
                        case "30": c.Z = value; break;
                        case "230": c.Nz = value; break;
                    }
                }
                return c;
            }
            return null;
        }
    }
}
