using System;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CADability;
using CADability.GeoObject;

namespace CADability.ImportTests
{
    /// <summary>
    /// Regression for issue #287: a HATCH polyline boundary whose "is closed" flag (group code
    /// 73) is 0 lost its closing edge, so a square became a triangle. Hatch boundary loops are
    /// closed by definition, so the import must close the loop regardless of that flag, and it
    /// must not add a zero-length closing edge when the last vertex already repeats the first.
    /// </summary>
    [TestClass]
    public class DxfHatchBoundaryTest
    {
        public TestContext TestContext { get; set; }

        // The boundary is the square (0,0) (10,0) (10,10) (0,10). With bulge, the first edge
        // from (0,0) to (10,0) is a counterclockwise half circle bulging below the square,
        // which adds half a circle of radius 5.
        private const double SquareArea = 100.0;
        private const double BulgedSquareArea = 100.0 + Math.PI * 25.0 / 2.0;

        [DataTestMethod]
        [DataRow(true, false, false, DisplayName = "closed flag set")]
        [DataRow(false, false, false, DisplayName = "closed flag not set")]
        [DataRow(true, true, false, DisplayName = "closed flag set, with bulge")]
        [DataRow(false, true, false, DisplayName = "closed flag not set, with bulge")]
        [DataRow(true, false, true, DisplayName = "closed flag set, last vertex repeats first")]
        [DataRow(false, false, true, DisplayName = "closed flag not set, last vertex repeats first")]
        [DataRow(false, true, true, DisplayName = "closed flag not set, with bulge, last vertex repeats first")]
        public void import_dxf_hatch_polyline_boundary_is_always_closed(bool closedFlag, bool withBulge, bool repeatFirstVertex)
        {
            var model = ImportDxf(HatchDxf(closedFlag, withBulge, repeatFirstVertex));
            Assert.AreEqual(1, model.AllObjects.Count, "the HATCH should import as a single object");
            var hatch = model.AllObjects[0] as Hatch;
            Assert.IsNotNull(hatch, "a solid HATCH should import as a Hatch");

            Assert.AreEqual(1, hatch.CompoundShape.SimpleShapes.Length, "one boundary loop");
            Assert.AreEqual(withBulge ? BulgedSquareArea : SquareArea, hatch.CompoundShape.Area, 1e-6, "hatch area");
            Assert.AreEqual(4, hatch.CompoundShape.SimpleShapes[0].Outline.Count,
                "the boundary has four edges: the closing edge must be there, but not as an extra zero-length edge");
        }

        // --- helpers -------------------------------------------------------------------------

        // A solid HATCH with a single polyline boundary path (path type flag 2).
        private static string HatchDxf(bool closedFlag, bool withBulge, bool repeatFirstVertex)
        {
            double[,] vertices = { { 0, 0 }, { 10, 0 }, { 10, 10 }, { 0, 10 } };
            int count = vertices.GetLength(0) + (repeatFirstVertex ? 1 : 0);

            var sb = new StringBuilder();
            void Group(int code, object value)
            {
                sb.Append(code.ToString(CultureInfo.InvariantCulture).PadLeft(3)).Append('\n');
                sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture)).Append('\n');
            }

            Group(0, "SECTION"); Group(2, "HEADER");
            Group(9, "$ACADVER"); Group(1, "AC1015");
            Group(0, "ENDSEC");
            Group(0, "SECTION"); Group(2, "ENTITIES");
            Group(0, "HATCH");
            Group(100, "AcDbEntity"); Group(8, "0");
            Group(100, "AcDbHatch");
            Group(10, 0.0); Group(20, 0.0); Group(30, 0.0);
            Group(210, 0.0); Group(220, 0.0); Group(230, 1.0);
            Group(2, "SOLID");
            Group(70, 1); // solid fill
            Group(71, 0); // not associative
            Group(91, 1); // number of boundary paths
            Group(92, 2); // boundary path type: polyline
            Group(72, 1); // has bulge
            Group(73, closedFlag ? 1 : 0);
            Group(93, count);
            for (int i = 0; i < count; i++)
            {
                int v = i % vertices.GetLength(0);
                Group(10, vertices[v, 0]);
                Group(20, vertices[v, 1]);
                Group(42, withBulge && i == 0 ? 1.0 : 0.0);
            }
            Group(97, 0); // number of source boundary objects
            Group(75, 0); // hatch style
            Group(76, 1); // pattern type: predefined
            Group(98, 0); // number of seed points
            Group(0, "ENDSEC");
            Group(0, "EOF");
            return sb.ToString();
        }

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
    }
}
