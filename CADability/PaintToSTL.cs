using CADability.Attribute;
using CADability.GeoObject;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
#if WEBASSEMBLY
using CADability.WebDrawing;
using Point = CADability.WebDrawing.Point;
#else
using System.Drawing;
using Point = System.Drawing.Point;
#endif

namespace CADability
{
    public class PaintToSTL : IDisposable, IPaintTo3D
    {
        string fileName;
        double precision;
        private StreamWriter stream;
        public PaintToSTL(string fileName, double precision)
        {
            this.fileName = fileName;
            this.precision = precision;
        }

        bool IPaintTo3D.PaintSurfaces
        {
            get { return true; }
        }

        bool IPaintTo3D.PaintEdges
        {
            get { return false; }
        }

        bool IPaintTo3D.PaintSurfaceEdges
        {
            get
            {
                return false;
            }
            set
            {

            }
        }

        bool IPaintTo3D.UseLineWidth
        {
            get
            {
                return false;
            }
            set
            {

            }
        }

        double IPaintTo3D.Precision
        {
            get
            {
                return precision;
            }
            set
            {
                precision = value;
            }
        }

        double IPaintTo3D.PixelToWorld
        {
            get { throw new NotImplementedException(); }
        }

        bool IPaintTo3D.SelectMode
        {
            get
            {
                return false;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        Color IPaintTo3D.SelectColor
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        bool IPaintTo3D.DelayText
        {
            get
            {
                return false;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        bool IPaintTo3D.DelayAll
        {
            get
            {
                return false;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        bool IPaintTo3D.TriangulateText
        {
            get { return false; }
            set { }
        }

        bool IPaintTo3D.DontRecalcTriangulation
        {
            get
            {
                return false;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        PaintCapabilities IPaintTo3D.Capabilities
        {
            get { return PaintCapabilities.Standard; }
        }

        public void Init()
        {
            stream = File.CreateText(fileName);
            stream.WriteLine("solid " + fileName);
        }


        void IPaintTo3D.MakeCurrent()
        {

        }

        void IPaintTo3D.SetColor(Color color, int lockColor)
        {
            // hier könnte für binärausgabe die Farbe gesetzt werden
        }

        void IPaintTo3D.AvoidColor(Color color)
        {

        }

        void IPaintTo3D.SetLineWidth(LineWidth lineWidth)
        {

        }

        void IPaintTo3D.SetLinePattern(LinePattern pattern)
        {

        }

        void IPaintTo3D.Polyline(GeoPoint[] points)
        {

        }

        void IPaintTo3D.FilledPolyline(GeoPoint[] points)
        {

        }

        void IPaintTo3D.Triangle(GeoPoint[] vertex, GeoVector[] normals, int[] indextriples)
        {
            string format = "0.#";
            // #.## entfernt überflüssige Nullen nach dem Komma, 0.00 erzwingt die Stellenanzahl, also auch mit Nullen
            if (precision < 1.0) format = "0.#";
            if (precision < 0.1) format = "0.##";
            if (precision < 0.01) format = "0.###";
            if (precision < 0.001) format = "0.####";
            if (precision < 0.0001) format = "0.#####";
            if (precision < 0.00001) format = "0.######";
            if (precision < 0.000001) format = "0.#######";
            if (precision < 0.0000001) format = "0.########";
            for (int i = 0; i < indextriples.Length; i += 3)
            {
                int i1 = indextriples[i];
                int i2 = indextriples[i + 1];
                int i3 = indextriples[i + 2];
                stream.WriteLine("facet normal " + ((normals[i1].x + normals[i2].x + normals[i3].x) / 3.0).ToString("0.###", NumberFormatInfo.InvariantInfo) + " " + ((normals[i1].y + normals[i2].y + normals[i3].y) / 3.0).ToString("0.###", NumberFormatInfo.InvariantInfo) + " " + ((normals[i1].z + normals[i2].z + normals[i3].z) / 3.0).ToString("0.###", NumberFormatInfo.InvariantInfo));
                stream.WriteLine("outer loop");
                stream.WriteLine("vertex " + vertex[i1].x.ToString(format, NumberFormatInfo.InvariantInfo) + " " + vertex[i1].y.ToString(format, NumberFormatInfo.InvariantInfo) + " " + vertex[i1].z.ToString(format, NumberFormatInfo.InvariantInfo));
                stream.WriteLine("vertex " + vertex[i2].x.ToString(format, NumberFormatInfo.InvariantInfo) + " " + vertex[i2].y.ToString(format, NumberFormatInfo.InvariantInfo) + " " + vertex[i2].z.ToString(format, NumberFormatInfo.InvariantInfo));
                stream.WriteLine("vertex " + vertex[i3].x.ToString(format, NumberFormatInfo.InvariantInfo) + " " + vertex[i3].y.ToString(format, NumberFormatInfo.InvariantInfo) + " " + vertex[i3].z.ToString(format, NumberFormatInfo.InvariantInfo));
                stream.WriteLine("endloop");
                stream.WriteLine("endfacet");

            }
        }

        void IPaintTo3D.PrepareText(string fontName, string textString, FontStyle fontStyle)
        {

        }

        void IPaintTo3D.PrepareIcon(Bitmap icon)
        {

        }

        void IPaintTo3D.PrepareBitmap(Bitmap bitmap, int xoffset, int yoffset)
        {

        }

        void IPaintTo3D.PrepareBitmap(Bitmap bitmap)
        {

        }

        void IPaintTo3D.RectangularBitmap(Bitmap bitmap, GeoPoint location, GeoVector directionWidth, GeoVector directionHeight)
        {

        }

        void IPaintTo3D.Text(GeoVector lineDirection, GeoVector glyphDirection, GeoPoint location, string fontName, string textString, FontStyle fontStyle, Text.AlignMode alignment, Text.LineAlignMode lineAlignment)
        {

        }

        void IPaintTo3D.List(IPaintTo3DList paintThisList)
        {

        }

        void IPaintTo3D.SelectedList(IPaintTo3DList paintThisList, int wobbleRadius)
        {

        }

        void IPaintTo3D.Nurbs(GeoPoint[] poles, double[] weights, double[] knots, int degree)
        {

        }

        void IPaintTo3D.Line2D(int sx, int sy, int ex, int ey)
        {

        }

        void IPaintTo3D.Line2D(PointF p1, PointF p2)
        {

        }

        void IPaintTo3D.FillRect2D(PointF p1, PointF p2)
        {

        }

        void IPaintTo3D.Point2D(int x, int y)
        {

        }

        void IPaintTo3D.DisplayIcon(GeoPoint p, Bitmap icon)
        {

        }

        void IPaintTo3D.DisplayBitmap(GeoPoint p, Bitmap bitmap)
        {

        }

        void IPaintTo3D.SetProjection(Projection projection, BoundingCube boundingCube)
        {

        }

        void IPaintTo3D.Clear(Color background)
        {

        }

        void IPaintTo3D.Resize(int width, int height)
        {

        }

        void IPaintTo3D.OpenList(string name)
        {

        }

        IPaintTo3DList IPaintTo3D.CloseList()
        {
            return null;
        }

        IPaintTo3DList IPaintTo3D.MakeList(List<IPaintTo3DList> sublists)
        {
            return null;
        }

        void IPaintTo3D.OpenPath()
        {

        }

        void IPaintTo3D.ClosePath(Color color)
        {

        }

        void IPaintTo3D.CloseFigure()
        {

        }

        void IPaintTo3D.Arc(GeoPoint center, GeoVector majorAxis, GeoVector minorAxis, double startParameter, double sweepParameter)
        {

        }

        void IPaintTo3D.FreeUnusedLists()
        {

        }

        void IPaintTo3D.UseZBuffer(bool use)
        {

        }

        void IPaintTo3D.Blending(bool on)
        {

        }

        void IPaintTo3D.FinishPaint()
        {

        }

        void IPaintTo3D.PaintFaces(PaintTo3D.PaintMode paintMode)
        {

        }

        IDisposable IPaintTo3D.FacesBehindEdgesOffset
        {
            get { return null; }
        }

        bool IPaintTo3D.IsBitmap => throw new NotImplementedException();

        void IPaintTo3D.Dispose()
        {
            stream.WriteLine("endsolid +" + fileName);
            stream.Close();
        }

        void IPaintTo3D.PushState()
        {

        }

        void IPaintTo3D.PopState()
        {

        }

        void IPaintTo3D.PushMultModOp(ModOp insertion)
        {

        }

        void IPaintTo3D.PopModOp()
        {

        }

        void IPaintTo3D.SetClip(Rectangle clipRectangle)
        {

        }

        void IDisposable.Dispose()
        {
            stream.WriteLine("endsolid " + fileName);
            stream.Close();
        }

        void IPaintTo3D.Points(GeoPoint[] points, float size, PointSymbol pointSymbol)
        {
        }

        void IPaintTo3D.PreparePointSymbol(PointSymbol pointSymbol)
        {
        }
    }
}
