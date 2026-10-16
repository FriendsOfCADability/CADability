#if WINDOWS
using CADability.Attribute;
using CADability.Forms;
using CADability.GeoObject;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;

namespace CADability.Tests
{
    /// <summary>
    /// The printing extensions of <see cref="GdiPrinting"/> in CADability.Forms replace the obsolete printing members of the
    /// kernel (issue #412). In CADability 1.x they forward to the kernel, so they must produce the same output.
    /// </summary>
    [TestClass]
    public class GdiPrintingTests
    {
        [TestMethod]
        public void DrawModelRendersLikePrintToGdi()
        {
            Project project = Project.CreateSimpleProject();
            Model model = project.GetActiveModel();
            Line line = Line.Construct();
            line.SetTwoPoints(new GeoPoint(10, 10, 0), new GeoPoint(90, 90, 0));
            model.Add(line);
            Layer[] layers = project.LayerList.ToArray();

            Bitmap viaExtension = Render((bitmap, projection) => bitmap.DrawModel(model, projection, layers, false, true));
#pragma warning disable CS0618 // the obsolete kernel implementation as the reference
            Bitmap viaKernel = Render((bitmap, projection) => new PrintToGDI(bitmap).PrintToBitmap(model, projection, layers, false, true));
#pragma warning restore CS0618

            int drawn = 0;
            for (int x = 0; x < viaKernel.Width; x++)
                for (int y = 0; y < viaKernel.Height; y++)
                {
                    Color expected = viaKernel.GetPixel(x, y);
                    Assert.AreEqual(expected.ToArgb(), viaExtension.GetPixel(x, y).ToArgb(), $"pixel {x}, {y}");
                    if (expected.ToArgb() != Color.White.ToArgb()) ++drawn;
                }
            Assert.IsTrue(drawn > 0, "the line is drawn");
        }

        private static Bitmap Render(System.Action<Bitmap, Projection> draw)
        {
            Bitmap bitmap = new Bitmap(100, 100);
            Projection projection = new Projection(Projection.StandardProjection.FromTop);
            projection.ShowFaces = false;
            projection.SetPlacement(new Rectangle(0, 0, 100, 100), new BoundingRect(0, 0, 100, 100));
            draw(bitmap, projection);
            return bitmap;
        }
    }
}
#endif
