using CADability.Attribute;
using CADability.Curve2D;
using CADability.GeoObject;
using CADability.Shapes;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("CADability.DebuggerVisualizers, PublicKey=0024000004800000940000000602000000240000525341310004000001000100b1ad8b0ed092aad57de8d0423919856eab910629e254ad40d6de3709a87cc161162d71827d65177e862b4822364c691d32f20beb81f0f7c17690662c2b397fe7bf556ac85e9dc66c7de56f435df1c5899a22b6fde65c423c1ec4fe3e4bb316838dbc7332ff1d31995a1657f754f942b36b82787d4c9c8e4325b4fb2871bdedbb")]

namespace CADability
{
    /// <summary>
    /// Ein Kontainer für den DebuggerVisualizer, der verschiedene Dinge (Punkte, Vektoren, GeoObjekte) im Zusammenhang
    /// darstellen kann.
    /// Wenn man spontan Arrays von etwas darstellbarem zeigen will, dann in CommandWindow gehen und ??DebuggerContainer.Show(array) eingeben
    /// </summary>    
    [Serializable()]
    public class DebuggerContainer : IDebuggerVisualizer
    {
        public static System.Drawing.Color FromInt(int c)
        {
            int[] rgb = new int[3];
            for (int i = 0; i < 16; i++)
            {
                if ((c & (1 << i)) != 0)
                {
                    rgb[i % 3] += 128 >> (i / 3);
                }
            }
            return System.Drawing.Color.FromArgb(rgb[0], rgb[1], rgb[2]);
        }
        public GeoObjectList toShow;
        public DebuggerContainer()
        {
            toShow = new GeoObjectList();
        }
        public void Add(IGeoObject toAdd, System.Drawing.Color color, int debugHint = 0)
        {
            if (toAdd != null)
            {
                ColorDef cd = new ColorDef(color.Name, color);
                if (toAdd is IColorDef) (toAdd as IColorDef).ColorDef = cd;
                toShow.Add(toAdd);
            }

        }
        public void Add(IGeoObject toAdd)
        {
            if (toAdd is IColorDef cd && cd.ColorDef==null) toAdd = toAdd.Clone(); // otherwise we would change the color of the original
            AssertColor(toAdd);
            if (toAdd != null) toShow.Add(toAdd);
        }
        public void Add(IGeoObject toAdd, int debugHint)
        {
            if (toAdd == null) return;
            IntegerProperty ip = new IntegerProperty(debugHint, "Debug.Hint");
            if (toAdd is IColorDef cd && cd.ColorDef == null) toAdd = toAdd.Clone(); // otherwise we would change the color of the original
            toAdd.UserData.Add("Debug", ip);
            AssertColor(toAdd);
            toShow.Add(toAdd);
        }
        public void Add(GeoObjectList toAdd)
        {
            for (int i = 0; i < toAdd.Count; i++)
            {
                IGeoObject clone = toAdd[i].Clone();
                IntegerProperty ip = new IntegerProperty(i, "Debug.Hint");
                clone.UserData.Add("Debug", ip);
                AssertColor(clone);
                toShow.Add(clone);
            }
        }
        public void Add(GeoPoint from, GeoVector dir, double size, System.Drawing.Color color)
        {
            Line l = Line.Construct();
            l.StartPoint = from;
            l.EndPoint = from + size * dir.Normalized;
            ColorDef cd = new ColorDef(color.Name, color);
            l.ColorDef = cd;
            toShow.Add(l);
            GeoVector perp = dir ^ GeoVector.ZAxis;
            if (Precision.IsNullVector(perp))
            {
                perp = dir ^ GeoVector.XAxis;
            }
            GeoPoint p1 = from + size * 0.9 * dir.Normalized + size * 0.1 * perp.Normalized;
            GeoPoint p2 = from + size * 0.9 * dir.Normalized - size * 0.1 * perp.Normalized;
            Line l1 = Line.Construct();
            l1.StartPoint = l.EndPoint;
            l1.EndPoint = p1;
            l1.ColorDef = cd;
            toShow.Add(l1);
            Line l2 = Line.Construct();
            l2.StartPoint = l.EndPoint;
            l2.EndPoint = p2;
            l2.ColorDef = cd;
            toShow.Add(l2);
        }
        public void Add(ICurve2D c2d)
        {
            IGeoObject go = c2d.MakeGeoObject(Plane.XYPlane);
            AssertColor(go);
            toShow.Add(go);
        }
        public void Add(ICurve2D c2d, System.Drawing.Color color, int debugHint)
        {
            if (c2d == null) return;
            IGeoObject go = c2d.MakeGeoObject(Plane.XYPlane);
            ColorDef cd = new ColorDef(color.Name, color);
            (go as IColorDef).ColorDef = cd;
            IntegerProperty ip = new IntegerProperty(debugHint, "Debug.Hint");
            go.UserData.Add("Debug", ip);
            //if (c2d.UserData!=null) // userdata kopieren
            //{
            //    foreach (KeyValuePair<string,object> item in c2d.UserData)
            //    {
            //        go.UserData.Add(item.Key, item.Value);
            //    }
            //}
            toShow.Add(go);
        }
        public void Add(ICurve2D c2d, System.Drawing.Color color, string debugHint)
        {
            if (c2d == null) return;
            IGeoObject go = c2d.MakeGeoObject(Plane.XYPlane);
            ColorDef cd = new ColorDef(color.Name, color);
            (go as IColorDef).ColorDef = cd;
            StringProperty sp = new StringProperty(debugHint, "Debug.Hint");
            go.UserData.Add("Debug", sp);
            toShow.Add(go);
        }
        public void Add(Border bdr, System.Drawing.Color color, int debugHint)
        {
            IGeoObject go = bdr.AsPath().MakeGeoObject(Plane.XYPlane);
            ColorDef cd = new ColorDef(color.Name, color);
            (go as IColorDef).ColorDef = cd;
            IntegerProperty ip = new IntegerProperty(debugHint, "Debug.Hint");
            go.UserData.Add("Debug", ip);
            if (go is CADability.GeoObject.Path)
            {
                CADability.GeoObject.Path path = (go as CADability.GeoObject.Path);
                for (int i = 0; i < path.CurveCount; ++i)
                {
                    (path.Curves[i] as IGeoObject).UserData.Add("Debug", ip);
                    toShow.Add(path.Curves[i] as IGeoObject);
                }
            }
        }
        public void Add(ICurve2D[] cvs)
        {
            for (int i = 0; i < cvs.Length; ++i)
            {
                Add(cvs[i], System.Drawing.Color.Red, i);
            }
        }
        public void Add(GeoPoint2D pnt, System.Drawing.Color color, int debugHint)
        {
            GeoObject.Point point = GeoObject.Point.Construct();
            point.Location = new GeoPoint(pnt);
            point.Symbol = PointSymbol.Circle;
            ColorDef cd = new ColorDef(color.Name, color);
            point.ColorDef = cd;
            IntegerProperty ip = new IntegerProperty(debugHint, "Debug.Hint");
            point.UserData.Add("Debug", ip);
            toShow.Add(point);
        }
        public void Add(GeoPoint2D pnt, System.Drawing.Color color, string debugHint)
        {
            GeoObject.Point point = GeoObject.Point.Construct();
            point.Location = new GeoPoint(pnt);
            point.Symbol = PointSymbol.Circle;
            ColorDef cd = new ColorDef(color.Name, color);
            point.ColorDef = cd;
            StringProperty sp = new StringProperty(debugHint, "Debug.Hint");
            point.UserData.Add("Debug", sp);
            toShow.Add(point);
        }
        public void Add(GeoPoint pnt, System.Drawing.Color color, int debugHint)
        {
            GeoObject.Point point = GeoObject.Point.Construct();
            point.Location = pnt;
            point.Symbol = PointSymbol.Circle;
            ColorDef cd = new ColorDef(color.Name, color);
            point.ColorDef = cd;
            IntegerProperty ip = new IntegerProperty(debugHint, "Debug.Hint");
            point.UserData.Add("Debug", ip);
            toShow.Add(point);
        }
        private ColorDef pointColor = null;
        private ColorDef PointColor
        {
            get
            {
                if (pointColor == null)
                {
                    pointColor = new ColorDef("auto point", Color.Brown);
                }
                return pointColor;
            }
        }
        private ColorDef curveColor = null;
        private ColorDef CurveColor
        {
            get
            {
                if (curveColor == null)
                {
                    curveColor = new ColorDef("auto curve", Color.DarkCyan);
                }
                return curveColor;
            }
        }
        private ColorDef faceColor = null;
        private ColorDef FaceColor
        {
            get
            {
                if (faceColor == null)
                {
                    faceColor = new ColorDef("auto face", Color.GreenYellow);
                }
                return faceColor;
            }
        }
        private void AssertColor(IGeoObject go)
        {
            if (go is IColorDef cd && cd.ColorDef == null)
            {
                if (go is GeoObject.Point) cd.ColorDef = PointColor;
                if (go is ICurve) cd.ColorDef = CurveColor;
                if (go is Face) cd.ColorDef = FaceColor;
                if (go is Shell) cd.ColorDef = FaceColor;
                if (go is Solid) cd.ColorDef = FaceColor;
            }
        }
        #region IDebuggerVisualizer Members
        GeoObjectList IDebuggerVisualizer.GetList()
        {
            return toShow;
        }
        #endregion
        public GeoObjectList Squared2DList
        {
            get
            {
                BoundingCube bc = toShow.GetExtent();
                ModOp mop;
                if (bc.Xmax - bc.Xmin > bc.Ymax - bc.Ymin)
                {
                    mop = ModOp.Scale(1.0, (bc.Xmax - bc.Xmin) / (bc.Ymax - bc.Ymin), 1.0);
                }
                else
                {
                    mop = ModOp.Scale((bc.Ymax - bc.Ymin) / (bc.Xmax - bc.Xmin), 1.0, 1.0);
                }
                GeoObjectList res = toShow.CloneObjects();
                res.Modify(mop);
                return res;
            }
        }
        //public static DebuggerContainer Show(object[] obj)
        //{
        //    DebuggerContainer res = new CADability.DebuggerContainer();
        //    for (int i = 0; i < obj.Length; i++)
        //    {
        //        if (obj[i] is Face) res.Add(obj[i] as IGeoObject, (obj[i] as Face).GetHashCode());
        //        else if (obj[i] is IGeoObject) res.Add(obj[i] as IGeoObject);
        //        else if (obj[i] is Edge && (obj[i] as Edge).Curve3D!=null) res.Add((obj[i] as Edge).Curve3D as IGeoObject, (obj[i] as Edge).GetHashCode());
        //        else if (obj[i] is ICurve2D) res.Add(obj[i] as ICurve2D);
        //    }
        //    return res;
        //}
        public static DebuggerContainer Show(IArray<GeoPoint> pnts)
        {
            return Show(pnts.ToArray());
        }
        public static DebuggerContainer Show(IEnumerable<object> obj)
        {
            DebuggerContainer res = new DebuggerContainer();
            ColorDef cd = new ColorDef("debug", System.Drawing.Color.Red);
            int i = 0;
            foreach (object obji in obj)
            {

                if (obji is Face)
                {
                    res.Add(obji as IGeoObject, (obji as Face).GetHashCode());
                    Face fc = obji as Face;
                    double ll = fc.GetExtent(0.0).Size * 0.01;
                    SimpleShape ss = fc.Area;
                    GeoPoint2D c = ss.GetExtent().GetCenter();
                    GeoPoint pc = fc.Surface.PointAt(c);
                    GeoVector nc = fc.Surface.GetNormal(c);
                    Line l = Line.Construct();
                    l.SetTwoPoints(pc, pc + ll * nc.Normalized);
                    l.ColorDef = cd;
                    res.Add(l);
                }
                else if (obji is IGeoObject) res.Add(obji as IGeoObject);
                else if (obji is Edge && (obji as Edge).Curve3D != null) res.Add((obji as Edge).Curve3D as IGeoObject, (obji as Edge).GetHashCode());
                else if (obji is ICurve2D) res.Add(obji as ICurve2D, System.Drawing.Color.Red, i);
                else if (obji is Vertex) res.Add((obji as Vertex).DebugPoint, (obji as Vertex).GetHashCode());
                else if (obji is IDebuggerVisualizer) res.Add((obji as IDebuggerVisualizer).GetList());
                ++i;
            }
            return res;
        }
        public static DebuggerContainer Show(GeoPoint2D[] points)
        {
            DebuggerContainer res = new DebuggerContainer();
            Polyline2D pl2d = new Polyline2D(points);
            res.Add(pl2d);
            return res;
        }
        public static DebuggerContainer Show(GeoPoint[] points)
        {
            DebuggerContainer res = new DebuggerContainer();
            Polyline pl = Polyline.Construct();
            pl.SetPoints(points, false);
            res.Add(pl);
            return res;
        }

        [CLSCompliant(false)]
        public static DebuggerContainer Show(GeoPoint[,] points)
        {
            DebuggerContainer res = new DebuggerContainer();
            for (int i = 0; i < points.GetLength(0); i++)
            {
                GeoPoint[] pnts = new GeoPoint[points.GetLength(1)];
                for (int j = 0; j < points.GetLength(1); j++)
                {
                    pnts[j] = points[i, j];
                }
                Polyline pl = Polyline.Construct();
                pl.SetPoints(pnts, false);
                res.Add(pl, i);
            }
            return res;
        }
        public static object SerializeAndDeserialize(ISerializable obj)
        {
            BinaryFormatter formatter = new BinaryFormatter(null, new StreamingContext(StreamingContextStates.File, null));
            formatter.AssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Simple;
            // formatter.TypeFormat = System.Runtime.Serialization.Formatters.FormatterTypeStyle.XsdString; // XsdString macht das wechseln von .NET Frameworks schwierig
            MemoryStream ms = new MemoryStream();
            formatter.Serialize(ms, obj);
            formatter = new BinaryFormatter();
            ms.Seek(0, SeekOrigin.Begin);
            try
            {
                return formatter.Deserialize(ms);
            }
            catch
            {
                return null;
            }
        }

        internal void Add(IEnumerable<Edge> edges, Face onThisFace, double arrowsize, System.Drawing.Color clr, int debugHint)
        {
            Random rnd = new Random();
            foreach (Edge edg in edges)
            {
                if (edg.Curve2D(onThisFace) == null) continue;
                int dbgh = debugHint;
                if (debugHint == -1) dbgh = edg.GetHashCode();
                Add(edg.Curve2D(onThisFace), clr, dbgh);
                // noch einen Richtungspfeil zufügen
                GeoPoint2D[] arrowpnts = new GeoPoint2D[3];
                double pos = 0.3 + rnd.NextDouble() * 0.4; // to have different positions when the same curve is displayed twice
                GeoVector2D dir = edg.Curve2D(onThisFace).DirectionAt(pos);
                if (!dir.IsNullVector())
                {
                    dir.Norm();
                    arrowpnts[1] = edg.Curve2D(onThisFace).PointAt(pos);
                    arrowpnts[0] = arrowpnts[1] - arrowsize * dir + arrowsize * dir.ToLeft();
                    arrowpnts[2] = arrowpnts[1] - arrowsize * dir + arrowsize * dir.ToRight();
                    Polyline2D pl2d = new Polyline2D(arrowpnts);
                    Add(pl2d, clr, edg.GetHashCode());
                }
            }
        }
        internal void Add(IEnumerable<ICurve2D> crvs, double arrowsize, System.Drawing.Color clr, int debugHint)
        {
            int i = 0;
            foreach (ICurve2D crv in crvs)
            {
                Add(crv, clr, debugHint);
                // noch einen Richtungspfeil zufügen
                GeoPoint2D[] arrowpnts = new GeoPoint2D[3];
                GeoVector2D dir = crv.DirectionAt(0.5);
                if (!dir.IsNullVector())
                {
                    dir.Norm();
                    arrowpnts[1] = crv.PointAt(0.5);
                    arrowpnts[0] = arrowpnts[1] - arrowsize * dir + arrowsize * dir.ToLeft();
                    arrowpnts[2] = arrowpnts[1] - arrowsize * dir + arrowsize * dir.ToRight();
                    Polyline2D pl2d = new Polyline2D(arrowpnts);
                    Add(pl2d, clr, debugHint * 100 + i);
                }
                ++i;
            }
        }
    }
    internal interface IDebuggerVisualizer
    {
        GeoObjectList GetList();
    }
}
