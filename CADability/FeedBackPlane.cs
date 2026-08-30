#if WEBASSEMBLY
using CADability.WebDrawing;
using Point = CADability.WebDrawing.Point;
#else
using System.Drawing;
using Point = System.Drawing.Point;
#endif

namespace CADability.Actions
{
    /// <summary>
    /// A translucent section of a plane for the display during an interactive action. Use this object in a call
    /// to  <see cref="ConstructAction.FeedBack"/>.
    /// </summary>

    public class FeedBackPlane : IFeedBack
    {
        Plane pln;
        double width;
        double height;
        Color color;
        /// <summary>
        /// Constructs a feedback plane. The plane will usually be modified according to the user action 
        /// (e.g. mouse movement)
        /// </summary>
        /// <param name="pln">The plane position (center)</param>
        /// <param name="width">Width of the section beeing displayed</param>
        /// <param name="height">Height of the plane</param>
        public FeedBackPlane(Plane pln, double width, double height)
        {
            this.pln = pln;
            this.width = width;
            this.height = height;
            object o = Settings.GlobalSettings.GetValue("Setting.Colors.Feedback");
            if (o is ColorSetting) color = (o as ColorSetting).Color;
            else color = Color.LightYellow;
            // color = Color.Indigo;
            color = Color.FromArgb(128, color.R, color.G, color.B);
        }

        /// <summary>
        /// Change the position of the plane. If currently displayed, the position will change in the view.
        /// </summary>
        /// <param name="pln">The plane position (center)</param>
        /// <param name="width">Width of the section beeing displayed</param>
        /// <param name="height">Height of the plane</param>
        public void Set(Plane pln, double width, double height)
        {
            this.pln = pln;
            this.width = width;
            this.height = height;
            if (FeedBackChangedEvent != null) FeedBackChangedEvent(this);
        }
        #region IFeedBack Members
        public event FeedBackChangedDelegate FeedBackChangedEvent;
        void IFeedBack.PaintTo3D(IPaintTo3D paintTo3D)
        {
            paintTo3D.UseZBuffer(true);
            paintTo3D.Blending(true);
            paintTo3D.SetColor(color);
            GeoPoint[] pnts = new GeoPoint[4];
            pnts[0] = pln.ToGlobal(new GeoPoint2D(-width / 2, -height / 2));
            pnts[1] = pln.ToGlobal(new GeoPoint2D(width / 2, -height / 2));
            pnts[2] = pln.ToGlobal(new GeoPoint2D(width / 2, height / 2));
            pnts[3] = pln.ToGlobal(new GeoPoint2D(-width / 2, height / 2));
            GeoVector[] norm = new GeoVector[4];
            norm[0] = pln.Normal;
            norm[1] = pln.Normal;
            norm[2] = pln.Normal;
            norm[3] = pln.Normal;
            int[] ind = new int[6];
            ind[0] = 0;
            ind[1] = 1;
            ind[2] = 2;
            ind[3] = 0;
            ind[4] = 2;
            ind[5] = 3;
            paintTo3D.Triangle(pnts, norm, ind);
            paintTo3D.Blending(false);
            paintTo3D.UseZBuffer(true);
        }

        BoundingBox IFeedBack.GetExtent()
        {
            GeoPoint[] pnts = new GeoPoint[4];
            pnts[0] = pln.ToGlobal(new GeoPoint2D(-width / 2, -height / 2));
            pnts[1] = pln.ToGlobal(new GeoPoint2D(width / 2, -height / 2));
            pnts[2] = pln.ToGlobal(new GeoPoint2D(width / 2, height / 2));
            pnts[3] = pln.ToGlobal(new GeoPoint2D(-width / 2, height / 2));
            BoundingBox res = BoundingBox.EmptyBoundingBox;
            for (int i = 0; i < 4; i++)
            {
                res.MinMax(pnts[i]);
            }
            return res;
        }
        #endregion
    }
}
