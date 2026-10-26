using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CADability;
using CADability.GeoObject;

namespace CADability.ImportTests
{
    /// <summary>
    /// Regression for issue #506: the height of a DXF TEXT (group 40) is the height of the capital
    /// letters, which AutoCAD and other viewers scale the font to, while <see cref="Text.TextSize"/>
    /// is the em height of the font. The exporter wrote TextSize unchanged, so every other program
    /// showed the text 1.4 (Arial) to 1.6 (Consolas) times larger than CADability and its bounding
    /// box. The font is Liberation Sans, which the tests register (see <see cref="TestFonts"/>).
    /// </summary>
    [TestClass]
    public class DxfTextHeightTest
    {
        public TestContext TestContext { get; set; }

        private const string Font = "Liberation Sans";

        [TestMethod]
        public void export_dxf_text_height_is_the_height_of_the_capital_letters()
        {
            Text text = MakeText("H", 12.0);
            double capitalHeight = OutlineHeight(text);
            Assert.IsTrue(capitalHeight > 6.0 && capitalHeight < 10.0, "test setup: an H is about 0.7 em high");

            ACadSharp.Entities.TextEntity written = ReadSingleText(Export(text));
            Assert.AreEqual(capitalHeight, written.Height, 1e-6, "group 40 is the height of the H CADability draws");
            Assert.AreEqual(1.0, written.WidthFactor, 1e-12, "group 41 of an unstretched text");
        }

        [TestMethod]
        public void import_dxf_text_draws_capital_letters_as_high_as_the_text_height()
        {
            ACadSharp.CadDocument doc = new ACadSharp.CadDocument();
            ACadSharp.Tables.TextStyle style = new ACadSharp.Tables.TextStyle(Font) { Filename = Font + ".ttf" };
            doc.TextStyles.Add(style);
            doc.Entities.Add(new ACadSharp.Entities.TextEntity
            {
                Value = "H",
                Height = 10.0,
                Style = style,
                InsertPoint = new CSMath.XYZ(5, 7, 0),
            });
            string file = this.TestContext.TestName + ".dxf";
            using (ACadSharp.IO.DxfWriter writer = new ACadSharp.IO.DxfWriter(file, doc, false)) writer.Write();

            Text text = ReimportSingle(file);
            Assert.AreEqual(Font, text.Font);
            Assert.AreEqual(10.0, OutlineHeight(text), 1e-6, "the imported H is as high as group 40");
        }

        [TestMethod]
        public void export_dxf_text_round_trip_keeps_size_width_and_extent()
        {
            Text text = MakeText("only 6 days left", 12.0);
            // stretched to twice the width
            text.LineDirection = 2.0 * text.LineDirection;
            BoundingCube extent = text.GetBoundingCube();

            string file = Export(text);
            Assert.AreEqual(2.0, ReadSingleText(file).WidthFactor, 1e-9, "group 41 carries the stretch");

            Text back = ReimportSingle(file);
            Assert.AreEqual(text.TextSize, back.TextSize, 1e-6, "TextSize");
            Assert.AreEqual(text.LineDirection.Length, back.LineDirection.Length, 1e-6, "length of the line direction");
            BoundingCube backExtent = back.GetBoundingCube();
            Assert.AreEqual(extent.Xmin, backExtent.Xmin, 1e-6);
            Assert.AreEqual(extent.Xmax, backExtent.Xmax, 1e-6);
            Assert.AreEqual(extent.Ymin, backExtent.Ymin, 1e-6);
            Assert.AreEqual(extent.Ymax, backExtent.Ymax, 1e-6);
        }

        [TestMethod]
        public void export_dxf_mtext_round_trip_keeps_size_and_line_spacing()
        {
            Text text = MakeText("first line\nsecond line", 12.0);
            text.LineSpacing = 1.4;
            text.Alignment = Text.AlignMode.Bottom; // MTEXT has no baseline attachment
            BoundingCube extent = text.GetBoundingCube();

            string file = Export(text);
            ACadSharp.Entities.MText written = ACadSharp.IO.DxfReader.Read(file).Entities.OfType<ACadSharp.Entities.MText>().Single();
            Assert.AreEqual(OutlineHeight(MakeText("H", 12.0)), written.Height, 1e-6, "group 40 is the height of the capital letters");

            Text back = ReimportSingle(file);
            Assert.AreEqual(text.TextSize, back.TextSize, 1e-6, "TextSize");
            Assert.AreEqual(text.LineSpacing, back.LineSpacing, 1e-6, "LineSpacing");
            BoundingCube backExtent = back.GetBoundingCube();
            Assert.AreEqual(extent.Ymin, backExtent.Ymin, 1e-6);
            Assert.AreEqual(extent.Ymax, backExtent.Ymax, 1e-6);
        }

        // --- helpers -------------------------------------------------------------------------

        private static Text MakeText(string value, double size)
        {
            Text text = Text.Construct();
            text.Font = Font;
            text.TextString = value;
            text.Location = new GeoPoint(5, 7, 0);
            text.LineDirection = GeoVector.XAxis;
            text.GlyphDirection = GeoVector.YAxis;
            text.TextSize = size;
            return text;
        }

        /// <summary>
        /// The height of the glyphs as CADability draws them.
        /// </summary>
        private static double OutlineHeight(Text text)
        {
            BoundingCube ext = BoundingCube.EmptyBoundingCube;
            foreach (IGeoObject go in text.GetOutline()) ext.MinMax(go.GetBoundingCube());
            return ext.Ymax - ext.Ymin;
        }

        private string Export(IGeoObject geoObject)
        {
            Project project = Project.CreateSimpleProject();
            project.GetModel(0).Add(geoObject.Clone());
            string file = this.TestContext.TestName + ".dxf";
            Assert.IsTrue(project.Export(file, "dxf"), "export must succeed");
            return file;
        }

        private static ACadSharp.Entities.TextEntity ReadSingleText(string file)
        {
            return ACadSharp.IO.DxfReader.Read(file).Entities.OfType<ACadSharp.Entities.TextEntity>().Single();
        }

        private static Text ReimportSingle(string file)
        {
            Project project = Project.ReadFromFile(file, "dxf");
            Assert.IsNotNull(project);
            return project.GetActiveModel().AllObjects.OfType<Text>().Single();
        }
    }
}
