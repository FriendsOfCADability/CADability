using CADability.Attribute;
using System.Drawing;
using System.Drawing.Printing;

namespace CADability.Forms
{
    /// <summary>
    /// Printing with GDI+ (System.Drawing.Printing): prints a <see cref="LayoutView"/> or a <see cref="GDI2DView"/> to a
    /// <see cref="PrintDocument"/> and renders a model into a <see cref="Bitmap"/>. These methods replace
    /// <see cref="LayoutView.Print(PrintDocument)"/>, <see cref="GDI2DView.PrintSinglePage(PrintDocument)"/> and the public
    /// members of <see cref="PrintToGDI"/>, which are obsolete because GDI+ leaves the kernel in CADability 2.0 (issue #412).
    /// In CADability 1.x they still use the implementation in the kernel; in 2.0 it moves here.
    /// </summary>
    public static class GdiPrinting
    {
#pragma warning disable CS0618 // the implementation is still in the kernel in CADability 1.x
        /// <summary>
        /// Prints the layout of <paramref name="layoutView"/> on a single page. Depending on the global setting
        /// "Printing.PrintingMode" the patches are either output as vector graphics (mode 1, with hidden line removal and shading)
        /// or each patch is rendered into a bitmap at the printer resolution which is placed on the page. The paper size of the
        /// layout is assumed to be in mm. Exceptions of the printing system are swallowed.
        /// </summary>
        /// <param name="printDocument">The print document to print to, with the printer and page settings already set</param>
        /// <param name="layoutView">The layout to print</param>
        public static void Print(this PrintDocument printDocument, LayoutView layoutView)
        {
            layoutView.Print(printDocument);
        }
        /// <summary>
        /// Prints the contents of <paramref name="gdiView"/> on a single page. The paper of the view
        /// (<see cref="GDI2DView.PaperWidth"/> by <see cref="GDI2DView.PaperHeight"/>) is fitted onto the page, so the paper size
        /// should be set. Exceptions of the printing system are swallowed.
        /// </summary>
        /// <param name="printDocument">The print document to print to, with the printer and page settings already set</param>
        /// <param name="gdiView">The view to print</param>
        public static void PrintSinglePage(this PrintDocument printDocument, GDI2DView gdiView)
        {
            gdiView.PrintSinglePage(printDocument);
        }
        /// <summary>
        /// Prints the <see cref="GDI2DView"/> named <paramref name="nameOfView"/> of <paramref name="project"/> on a single page.
        /// Nothing is printed if the project has no such view.
        /// </summary>
        /// <param name="printDocument">The print document to print to, null for the print document of the project</param>
        /// <param name="project">The project containing the view</param>
        /// <param name="nameOfView">The name of the view</param>
        /// <param name="print2D">True to print only the curves, false to print the faces too</param>
        public static void PrintSinglePage(this PrintDocument printDocument, Project project, string nameOfView, bool print2D)
        {
            PrintToGDI.PrintSinglePage(project, nameOfView, print2D, printDocument);
        }
        /// <summary>
        /// Renders <paramref name="model"/> with the <paramref name="projection"/> into <paramref name="bitmap"/>, which is
        /// filled white first.
        /// </summary>
        /// <param name="bitmap">The bitmap to render into</param>
        /// <param name="model">The model to render</param>
        /// <param name="projection">The projection to use</param>
        /// <param name="visibleLayers">The layers to render</param>
        /// <param name="shading">True to shade the faces</param>
        /// <param name="curvesOnly">Not used: whether only the curves are rendered follows <see cref="Projection.ShowFaces"/> of
        /// <paramref name="projection"/>, as in <see cref="PrintToGDI.PrintToBitmap"/></param>
        public static void DrawModel(this Bitmap bitmap, Model model, Projection projection, Layer[] visibleLayers, bool shading, bool curvesOnly)
        {
            new PrintToGDI(bitmap).PrintToBitmap(model, projection, visibleLayers, shading, curvesOnly);
        }
#pragma warning restore CS0618
    }
}
