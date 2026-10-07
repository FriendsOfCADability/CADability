using System;
using System.Globalization;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CADability;
using CADability.GeoObject;

namespace CADability.ImportTests
{
    /// <summary>
    /// Regression for issue #356: circles, arcs and texts that were rotated (or flipped) out of
    /// the XY plane came back from a DXF export at the wrong position. DXF reads ARC and CIRCLE
    /// centers and TEXT points in the OCS that the entity normal spans, but the exporter wrote
    /// them in world coordinates. A flip turns the normal into (0,0,-1), whose OCS X axis is (-1,0,0), so
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

        // TEXT points (groups 10 and 11) are OCS points as well. A text flipped about the X
        // axis reads mirrored, which gives it the normal (0,0,-1).
        [TestMethod]
        public void export_dxf_flipped_text_keeps_position()
        {
            RequireGdi();
            foreach (bool aligned in new[] { false, true })
            {
                Text text = MakeText("Flipped", new GeoPoint(10, 5, 2));
                if (aligned)
                {
                    // non-default alignment: group 11 becomes the authoritative point
                    text.LineAlignment = Text.LineAlignMode.Center;
                    text.Alignment = Text.AlignMode.Center;
                }
                text.Modify(ModOp.Rotate(GeoPoint.Origin, GeoVector.XAxis, new SweepAngle(Math.PI)));
                Assert.IsTrue((text.LineDirection ^ text.GlyphDirection).z < 0, "test setup: the flip must turn the normal down");

                string file = Export(text);
                DxfCenter written = ReadFirstCenter(file, "TEXT");
                Assert.IsNotNull(written, "exported file must contain a TEXT entity");
                Assert.AreEqual(-1.0, written.Nz, 1e-12, "normal");
                GeoPoint p = text.Location; // (10,-5,-2)
                Assert.AreEqual(-p.x, written.X, 1e-8, "OCS x of the insertion point");
                Assert.AreEqual(p.y, written.Y, 1e-8, "OCS y of the insertion point");
                Assert.AreEqual(-p.z, written.Z, 1e-8, "OCS z of the insertion point");

                AssertSameText(text, ReimportSingle<Text>(file));
            }
        }

        [TestMethod]
        public void export_dxf_tilted_text_keeps_position()
        {
            RequireGdi();
            Text text = MakeText("Tilted", new GeoPoint(-3, 8, 1));
            text.Modify(ModOp.Rotate(new GeoPoint(2, 3, 4), new GeoVector(1, 1, 0.3), new SweepAngle(0.65)));
            AssertSameText(text, ReimportSingle<Text>(Export(text)));
        }

        // A multi-line text goes out as MTEXT, whose insertion point is a world point and whose
        // group 11 is the text direction as a world vector, not an OCS angle.
        [TestMethod]
        public void export_dxf_flipped_and_tilted_mtext_keep_position()
        {
            RequireGdi();
            Text text = MakeText("first line\nsecond line", new GeoPoint(10, 5, 2));
            text.Modify(ModOp.Rotate(GeoPoint.Origin, GeoVector.XAxis, new SweepAngle(Math.PI)));
            string file = Export(text);
            DxfCenter written = ReadFirstCenter(file, "MTEXT");
            Assert.IsNotNull(written, "exported file must contain an MTEXT entity");
            Assert.AreEqual(-1.0, written.Nz, 1e-12, "normal");
            Assert.AreEqual(0.0, new GeoPoint(written.X, written.Y, written.Z) | text.Location, 1e-8,
                "MTEXT insertion point is written in world coordinates");
            GeoVector dir = text.LineDirection.Normalized;
            Assert.AreEqual(0.0, (new GeoVector(written.DirX, written.DirY, written.DirZ) - dir).Length, 1e-8,
                "MTEXT group 11 is the world text direction");
            AssertSameText(text, ReimportSingle<Text>(file));

            Text tilted = MakeText("first line\nsecond line", new GeoPoint(-3, 8, 1));
            tilted.Modify(ModOp.Rotate(new GeoPoint(2, 3, 4), new GeoVector(1, 1, 0.3), new SweepAngle(0.65)));
            AssertSameText(tilted, ReimportSingle<Text>(Export(tilted)));
        }

        // --- helpers -------------------------------------------------------------------------

        // Adding a Text to a model computes its extent through GDI, which only exists on Windows.
        private static void RequireGdi()
        {
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                Assert.Inconclusive("text needs GDI, which is only available on Windows");
        }

        private static Text MakeText(string value, GeoPoint location)
        {
            Text text = Text.Construct();
            text.Font = "Arial";
            text.TextString = value;
            text.TextSize = 2.5;
            text.Location = location;
            text.LineDirection = new GeoVector(2.5, 0, 0);
            text.GlyphDirection = new GeoVector(0, 2.5, 0);
            return text;
        }

        private static void AssertSameText(Text original, Text back)
        {
            Assert.AreEqual(0.0, back.Location | original.Location, 1e-8, "re-imported text location");
            Assert.AreEqual(0.0, (back.LineDirection.Normalized - original.LineDirection.Normalized).Length, 1e-8,
                "re-imported line direction");
            Assert.AreEqual(0.0, (back.GlyphDirection.Normalized - original.GlyphDirection.Normalized).Length, 1e-8,
                "re-imported glyph direction");
        }

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

        private static Ellipse ReimportSingle(string file) => ReimportSingle<Ellipse>(file);

        private static T ReimportSingle<T>(string file) where T : class, IGeoObject
        {
            Project project = Project.ReadFromFile(file, "dxf");
            Assert.IsNotNull(project);
            Model model = project.GetActiveModel();
            Assert.AreEqual(1, model.AllObjects.Count, "one entity expected after re-import");
            T e = model.AllObjects[0] as T;
            Assert.IsNotNull(e, "the entity must re-import as " + typeof(T).Name);
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

        /// <summary>Group codes 10/20/30, 11/21/31 and 230 of an entity as written to the file.</summary>
        private sealed class DxfCenter
        {
            public double X, Y, Z, DirX, DirY, DirZ, Nz = 1.0;
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
                        case "11": c.DirX = value; break;
                        case "21": c.DirY = value; break;
                        case "31": c.DirZ = value; break;
                        case "230": c.Nz = value; break;
                    }
                }
                return c;
            }
            return null;
        }
    }
}
