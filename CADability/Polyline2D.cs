using CADability.GeoObject;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.Serialization;

namespace CADability.Curve2D
{

    public class Polyline2DException : ApplicationException
    {
        public Polyline2DException(string msg)
            : base(msg)
        {
        }
    }

    /// <summary>
    /// Implements a polyline in 2D space. By implementing the ICurve2D interface this polyline
    /// can be handled as any 2D curve.
    /// </summary>
    [Serializable()]
    public class Polyline2D : GeneralCurve2D, ISerializable
    {
        private GeoPoint2D[] vertex;
        private double length;
        public Polyline2D(GeoPoint2D[] vertex)
        {
            if (vertex.Length < 2) throw new Polyline2DException("Constructing Polyline2D with less than two points");
            List<GeoPoint2D> pnts = new List<GeoPoint2D>();
            length = 0.0;
            // identische Punkte aus dem Array entfernen
            pnts.Add(vertex[0]);
            for (int i = 1; i < vertex.Length; ++i)
            {
                double d = Geometry.Dist(vertex[i], pnts[pnts.Count - 1]);
                if (d > Precision.eps)
                {
                    length += d;
                    pnts.Add(vertex[i]);
                }
            }
            if (pnts.Count < 2) throw new Polyline2DException("Constructing Polyline2D with less than two points");
            this.vertex = pnts.ToArray();
        }
        public static Polyline2D MakePolyline2D(GeoPoint2D[] vertex)
        {   // analog zum Konstruktor, nur ohne exception
            if (vertex.Length < 2) return null;
            List<GeoPoint2D> pnts = new List<GeoPoint2D>();
            // identische Punkte aus dem Array entfernen
            pnts.Add(vertex[0]);
            for (int i = 1; i < vertex.Length; ++i)
            {
                double d = Geometry.Dist(vertex[i], pnts[pnts.Count - 1]);
                if (d > Precision.eps)
                {
                    pnts.Add(vertex[i]);
                }
            }
            if (pnts.Count < 2) return null;
            return new Polyline2D(pnts.ToArray());
        }
        public static Polyline2D MakePolygon(GeoPoint2D center, GeoPoint2D vertex, int numberOfVertices)
        {
            GeoPoint2D[] vtx = new GeoPoint2D[numberOfVertices + 1];
            ModOp2D rot = ModOp2D.Rotate(center, SweepAngle.Full / numberOfVertices);
            GeoPoint2D current = vertex;
            for (int i = 0; i < numberOfVertices; i++)
            {
                vtx[i] = current;
                current = rot * current;
            }
            vtx[numberOfVertices] = vtx[0];
            return new Polyline2D(vtx);
        }
        public void SetVertices(GeoPoint2D[] vertices)
        {
            vertex = vertices;
            length = 0.0;
        }
        public GeoPoint2D GetVertex(int index)
        {
            return vertex[index];
        }
        public int VertexCount
        {
            get { return vertex.Length; }
        }
        public GeoPoint2D[] Vertex
        {
            get
            {
                return (GeoPoint2D[])vertex.Clone();
            }
        }
        public void Modify(ModOp2D m) // gehört das nicht in das ICurve Interface?
        {
            for (int i = 0; i < vertex.Length; ++i)
            {
                vertex[i] = m * vertex[i];
            }
        }
        public override ICurve2D GetModified(ModOp2D m)
        {
            Polyline2D res = this.Clone() as Polyline2D;
            res.Modify(m);
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Clone ()"/>
        /// </summary>
        /// <returns></returns>
        public override ICurve2D Clone()
        {
            Polyline2D res = new Polyline2D(vertex);
            res.UserData.CloneFrom(this.UserData);
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.CloneReverse (bool)"/>
        /// </summary>
        /// <param name="reverse"></param>
        /// <returns></returns>
        public override ICurve2D CloneReverse(bool reverse)
        {
            Polyline2D res = (Polyline2D)this.Clone();
            if (reverse) res.Reverse();
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Copy (ICurve2D)"/>
        /// </summary>
        /// <param name="toCopyFrom"></param>
        public override void Copy(ICurve2D toCopyFrom)
        {
            Polyline2D c = toCopyFrom as Polyline2D;
            this.vertex = (GeoPoint2D[])c.vertex.Clone();
            UserData.CloneFrom(c.UserData);
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.AddToGraphicsPath (System.Drawing.Drawing2D.GraphicsPath, bool)"/>
        /// </summary>
        /// <param name="path"></param>
        /// <param name="forward"></param>
        public override void AddToGraphicsPath(System.Drawing.Drawing2D.GraphicsPath path, bool forward)
        {
            if (forward)
            {
                for (int i = 1; i < vertex.Length; ++i)
                {
                    path.AddLine((float)vertex[i - 1].x, (float)vertex[i - 1].y, (float)vertex[i].x, (float)vertex[i].y);
                }
            }
            else
            {
                for (int i = vertex.Length - 1; i > 0; --i)
                {
                    path.AddLine((float)vertex[i].x, (float)vertex[i].y, (float)vertex[i - 1].x, (float)vertex[i - 1].y);
                }
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.DirectionAt (double)"/>
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public override GeoVector2D DirectionAt(double Position)
        {	// normiert auf 0 bis 1
            double tot = Length * Position;
            for (int i = 1; i < vertex.Length; ++i)
            {
                tot -= Geometry.Dist(vertex[i - 1], vertex[i]);
                if (tot < 0.0) return vertex[i] - vertex[i - 1];
            }
            return vertex[vertex.Length - 1] - vertex[vertex.Length - 2];
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Distance (GeoPoint2D)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public override double Distance(GeoPoint2D p)
        {
            double mindist = double.MaxValue;
            for (int i = 1; i < vertex.Length; ++i)
            {
                GeoPoint2D sp = vertex[i - 1];
                GeoPoint2D ep = vertex[i];
                GeoVector2D dir = ep - sp;
                GeoPoint2D dr = Geometry.DropPL(p, sp, ep);
                double pos = Geometry.LinePar(sp, dir, p);
                double d;
                if (pos >= 0.0 && pos <= 1.0) d = Geometry.DistPL(p, sp, dir);
                else d = Math.Min(Geometry.Dist(p, sp), Geometry.Dist(p, ep));
                if (Math.Abs(d) < Math.Abs(mindist)) mindist = d;
                // das ist der seitenrichtige Abstand (Vorzeichen), außer bei Eckpunkten
                // da muss evtl. noch nachgebessert werden
            }
            return mindist;
        }
        public override GeoVector2D EndDirection
        {
            get
            {
                return vertex[vertex.Length - 1] - vertex[vertex.Length - 2];
            }
        }
        public override GeoPoint2D EndPoint
        {
            get
            {
                return vertex[vertex.Length - 1];
            }
            set
            {
                vertex[vertex.Length - 1] = value;
                length = -1;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.GetAreaFromPoint (GeoPoint2D)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public override double GetAreaFromPoint(GeoPoint2D p)
        {
            double area = 0.0;
            for (int i = 1; i < vertex.Length; ++i)
            {
                GeoPoint2D sp = vertex[i - 1];
                GeoPoint2D ep = vertex[i];
                area += ((sp.x - p.x) * (ep.y - p.y) - (sp.y - p.y) * (ep.x - p.x)) / 2.0;
            }
            return area;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.GetExtendedHitTest ()"/>
        /// </summary>
        /// <returns></returns>
        public override IQuadTreeInsertable GetExtendedHitTest()
        {
            return new InfinitePath2D(this);
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.GetExtent ()"/>
        /// </summary>
        /// <returns></returns>
        public override BoundingRect GetExtent()
        {
            BoundingRect res = BoundingRect.EmptyBoundingRect;
            for (int i = 0; i < vertex.Length; ++i)
            {
                res.MinMax(vertex[i]);
            }
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.HitTest (ref BoundingRect, bool)"/>
        /// </summary>
        /// <param name="Rect"></param>
        /// <param name="IncludeControlPoints"></param>
        /// <returns></returns>
        public override bool HitTest(ref BoundingRect Rect, bool IncludeControlPoints)
        {
            ClipRect clr = new ClipRect(Rect);
            for (int i = 1; i < vertex.Length; ++i)
            {
                GeoPoint2D sp = vertex[i - 1];
                GeoPoint2D ep = vertex[i];
                if (clr.LineHitTest(sp, ep)) return true;
            }
            return false;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Intersect (ICurve2D)"/>
        /// </summary>
        /// <param name="IntersectWith"></param>
        /// <returns></returns>
        public override GeoPoint2DWithParameter[] Intersect(ICurve2D IntersectWith)
        {
            ArrayList res = new ArrayList();
            for (int i = 1; i < vertex.Length; ++i)
            {
                GeoPoint2D sp = vertex[i - 1];
                GeoPoint2D ep = vertex[i];
                Line2D l2d = new Line2D(sp, ep);
                GeoPoint2DWithParameter[] isp = l2d.Intersect(IntersectWith);
                for (int j = 0; j < isp.Length; ++j)
                {
                    if (l2d.IsParameterOnCurve(isp[j].par1) ||
                        (i == 1 && isp[j].par1 < 0.5) ||
                        (i == vertex.Length - 1 && isp[j].par1 > 0.5)
                        )
                    {
                        isp[j].par1 = this.PositionOf(isp[j].p);
                        res.Add(isp[j]);
                    }
                }
            }
            return (GeoPoint2DWithParameter[])res.ToArray(typeof(GeoPoint2DWithParameter));
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Intersect (GeoPoint2D, GeoPoint2D)"/>
        /// </summary>
        /// <param name="StartPoint"></param>
        /// <param name="EndPoint"></param>
        /// <returns></returns>
        public override GeoPoint2DWithParameter[] Intersect(GeoPoint2D StartPoint, GeoPoint2D EndPoint)
        {
            Line2D lp = new Line2D(StartPoint, EndPoint);
            return Intersect(lp);
        }
        public override double[] GetSelfIntersections()
        {
            List<double> res = new List<double>();
            for (int i = 1; i < vertex.Length - 2; i++)
            {
                for (int j = i + 1; j < vertex.Length - 1; j++)
                {
                    double pos1, pos2;
                    if (Geometry.IntersectLLparInside(vertex[i - 1], vertex[i], vertex[j], vertex[j + 1], out pos1, out pos2))
                    {
                        double pos = pos1 * (vertex[i] | vertex[i - 1]);
                        for (int k = 0; k < i - 1; k++)
                        {
                            pos += vertex[k] | vertex[k + 1];
                        }
                        res.Add(pos / Length);
                        pos = pos2 * (vertex[j] | vertex[j + 1]);
                        for (int k = 0; k < j; k++)
                        {
                            pos += vertex[k] | vertex[k + 1];
                        }
                        res.Add(pos / Length);
                    }
                }
            }
            return res.ToArray();
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.IsParameterOnCurve (double)"/>
        /// </summary>
        /// <param name="par"></param>
        /// <returns></returns>
        public override bool IsParameterOnCurve(double par)
        {
            return base.IsParameterOnCurve(par);
        }
        public override double Length
        {
            get
            {	// ggf. neu berechnen
                if (length <= 0.0)
                {
                    length = 0.0;
                    for (int i = 1; i < vertex.Length; ++i)
                    {
                        length += Geometry.Dist(vertex[i - 1], vertex[i]);
                    }
                }
                return length;
            }
        }
        public override double Sweep
        {
            get
            {
                double sweep = 0.0;
                for (int i = 1; i < vertex.Length - 1; ++i)
                {
                    sweep += new SweepAngle(vertex[i] - vertex[i - 1], vertex[i + 1] - vertex[i]);
                }
                return sweep;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.MakeGeoObject (Plane)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public override CADability.GeoObject.IGeoObject MakeGeoObject(Plane p)
        {
            Polyline pl = Polyline.Construct();
            GeoPoint[] pp = new GeoPoint[vertex.Length];
            for (int i = 0; i < vertex.Length; ++i)
            {
                pp[i] = p.ToGlobal(vertex[i]);
            }
            pl.SetPoints(pp, false); // ggf. besseren Test auf geschlossen
            return pl;
        }
        public override GeoVector2D MiddleDirection
        {
            get
            {
                return DirectionAt(0.5);
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.MinDistance (ICurve2D)"/>
        /// </summary>
        /// <param name="Other"></param>
        /// <returns></returns>
        public override double MinDistance(ICurve2D Other)
        {
            double mindist = double.MaxValue;
            for (int i = 1; i < vertex.Length; ++i)
            {
                GeoPoint2D sp = vertex[i - 1];
                GeoPoint2D ep = vertex[i];
                Line2D l2d = new Line2D(sp, ep);
                double d = l2d.MinDistance(Other);
                if (d < mindist) mindist = d;
            }
            return mindist;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Parallel (double, bool, double, double)"/>
        /// </summary>
        /// <param name="Dist"></param>
        /// <param name="approxSpline"></param>
        /// <param name="precision"></param>
        /// <param name="roundAngle"></param>
        /// <returns></returns>
        public override ICurve2D Parallel(double Dist, bool approxSpline, double precision, double roundAngle)
        {
            Line2D[] subcurves = new Line2D[vertex.Length - 1];
            for (int i = 1; i < vertex.Length; ++i)
            {
                GeoPoint2D sp = vertex[i - 1];
                GeoPoint2D ep = vertex[i];
                subcurves[i - 1] = new Line2D(sp, ep);
            }
            Path2D p2d = new Path2D(subcurves);
            return p2d.Parallel(Dist, approxSpline, precision, roundAngle);
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.PerpendicularFoot (GeoPoint2D)"/>
        /// </summary>
        /// <param name="FromHere"></param>
        /// <returns></returns>
        public override GeoPoint2D[] PerpendicularFoot(GeoPoint2D FromHere)
        {
            ArrayList res = new ArrayList();
            for (int i = 1; i < vertex.Length; ++i)
            {
                GeoPoint2D sp = vertex[i - 1];
                GeoPoint2D ep = vertex[i];
                Line2D l2d = new Line2D(sp, ep);
                GeoPoint2D[] tmp = l2d.PerpendicularFoot(FromHere);
                for (int j = 0; j < tmp.Length; ++j)
                {	// Fußpunkte müssen auf den einzelnen Kurven liegen oder 
                    // in der vorderen bzw. hinteren Verlängerung
                    double pos = l2d.PositionOf(tmp[j]);
                    bool add = l2d.IsParameterOnCurve(pos);
                    if (i == 0) add |= pos < 0.5;
                    if (i == vertex.Length - 1) add |= pos > 0.5;
                    if (add) res.Add(tmp[j]);
                }
            }
            return (GeoPoint2D[])res.ToArray(typeof(GeoPoint2D));
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.PointAt (double)"/>
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public override GeoPoint2D PointAt(double Position)
        {
            double tot = Length * Position;
            for (int i = 1; i < vertex.Length; ++i)
            {
                double d = tot;
                tot -= Geometry.Dist(vertex[i - 1], vertex[i]);
                if (tot < 0.0)
                {
                    GeoVector2D dir = vertex[i] - vertex[i - 1];
                    dir.Length = d; // der letzte positive Rest
                    return vertex[i - 1] + dir;
                }
            }
            return vertex[vertex.Length - 1]; // der letzte Punkt
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.PositionOf (GeoPoint2D)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public override double PositionOf(GeoPoint2D p)
        {
            double mindist = double.MaxValue;
            int foundind = -1;
            double foundpos = -1.0;
            double seglength = 0.0;
            for (int i = 1; i < vertex.Length; ++i)
            {
                GeoPoint2D sp = vertex[i - 1];
                GeoPoint2D ep = vertex[i];
                GeoVector2D dir = ep - sp;
                double pos = Geometry.LinePar(sp, dir, p);
                if ((pos >= 0 && pos <= 1.0) ||
                    (i == 1 && pos < 0.5) ||
                    (i == vertex.Length - 1 && pos > 0.5)
                    )
                {
                    GeoPoint2D pp = sp + pos * dir;
                    double d = Geometry.Dist(p, pp);
                    // folgendes Problem ist hier im Wege: es ist schon ein Punkt in der Verlängerung
                    // des Anfangs gefunden und der neue Punkt hat keinen besseren Abstand. Dieser Missstand
                    // wird mit folgender Zeile beseitigt: (foundpos ist am Anfang -1)
                    if (!IsParameterOnCurve(foundpos) && IsParameterOnCurve(pos)) d /= 10.0;
                    if (Math.Abs(d - mindist) < Precision.eps)
                    {
                        if (IsParameterOnCurve(pos) && !IsParameterOnCurve(foundpos))
                        {   // die neue Position ist besser als die alte, der Abstand ist der gleiche
                            mindist = d;
                            foundind = i;
                            foundpos = pos;
                            seglength = pos * dir.Length;
                        }
                    }
                    else if (d < mindist)
                    {
                        mindist = d;
                        foundind = i;
                        foundpos = pos;
                        seglength = pos * dir.Length;
                    }
                }
                if (Geometry.Dist(sp, p) < mindist)
                {
                    mindist = Geometry.Dist(sp, p);
                    foundind = i;
                    foundpos = 0.0;
                    seglength = 0.0;
                }
                if (Geometry.Dist(ep, p) < mindist)
                {
                    mindist = Geometry.Dist(ep, p);
                    foundind = i;
                    foundpos = 1.0;
                    seglength = dir.Length;
                }
            }
            if (foundind > 0)
            {
                // kommt hier nur rein wenn vertex.Length>1
                // return (foundind-1+foundpos)/(vertex.Length-1);
                // obige Zeile ausgeklammert, da nicht im Einklang mit PointAt.
                // Der Parameterwert scheint als echter Prozentwert auf der Länge der Polyline
                // definiert zu sein. Hierbei ist auch Trimm und Split zu beachten
                // die ebenfalls mit Parameterwerten arebiten
                double l = 0.0;
                for (int i = 1; i < foundind; ++i) l += Geometry.Dist(vertex[i - 1], vertex[i]);
                l += seglength;
                return l / Length;
            }
            else
            {
                return 0.0; // kommt nie vor
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Project (Plane, Plane)"/>
        /// </summary>
        /// <param name="fromPlane"></param>
        /// <param name="toPlane"></param>
        /// <returns></returns>
        public override ICurve2D Project(Plane fromPlane, Plane toPlane)
        {
            GeoPoint2D[] v = new GeoPoint2D[vertex.Length];
            for (int i = 0; i < vertex.Length; ++i)
            {
                v[i] = toPlane.Project(fromPlane.ToGlobal(vertex[i]));
            }
            return new Polyline2D(v);
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Reverse ()"/>
        /// </summary>
        public override void Reverse()
        {
            for (int i = 0; i < vertex.Length / 2; ++i)
            {
                GeoPoint2D tmp = vertex[i];
                vertex[i] = vertex[vertex.Length - i - 1];
                vertex[vertex.Length - i - 1] = tmp;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Move (double, double)"/>
        /// </summary>
        /// <param name="dx"></param>
        /// <param name="dy"></param>
        public override void Move(double dx, double dy)
        {
            GeoVector2D offset = new GeoVector2D(dx, dy);
            for (int i = 0; i < vertex.Length; ++i)
            {
                vertex[i] = vertex[i] + offset;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Split (double)"/>
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public override ICurve2D[] Split(double Position)
        {
            int ind = -1;
            double tot = Length * Position;
            GeoPoint2D SplitHere = GeoPoint2D.Origin;
            for (int i = 1; i < vertex.Length; ++i)
            {
                double d = tot;
                tot -= Geometry.Dist(vertex[i - 1], vertex[i]);
                if (tot < 0.0)
                {
                    ind = i;
                    GeoVector2D dir = vertex[i] - vertex[i - 1];
                    dir.Length = d; // der letzte positive Rest
                    SplitHere = vertex[i - 1] + dir;
                    break;
                }
            }
            if (ind < 0)
            {	// Schnitt am Endpunkt
                return new ICurve2D[] { Clone() };
            }
            Polyline2D[] res = new Polyline2D[2];
            GeoPoint2D[] v1 = new GeoPoint2D[ind + 1];
            GeoPoint2D[] v2 = new GeoPoint2D[vertex.Length - ind + 1];
            for (int i = 0; i < ind; ++i)
            {
                v1[i] = vertex[i];
            }
            v1[ind] = SplitHere;
            v2[0] = SplitHere;
            for (int i = ind; i < vertex.Length; ++i)
            {
                v2[i - ind + 1] = vertex[i];
            }
            Polyline2D pl1 = null, pl2 = null;
            try
            {
                pl1 = Polyline2D.MakePolyline2D(v1);
            }
            catch (Polyline2DException)
            {
                pl1 = null;
            }
            try
            {
                pl2 = Polyline2D.MakePolyline2D(v2);
            }
            catch (Polyline2DException)
            {
                pl2 = null;
            }
            if (pl1 == null && pl2 == null) return null; // sollte nicht vorkommen
            if (pl1 == null) return new ICurve2D[] { pl2 };
            if (pl2 == null) return new ICurve2D[] { pl1 };
            return new ICurve2D[] { pl1, pl2 };
        }
        public override GeoVector2D StartDirection
        {
            get
            {
                return vertex[1] - vertex[0];
            }
        }
        public override GeoPoint2D StartPoint
        {
            get
            {
                return vertex[0];
            }
            set
            {
                vertex[0] = value;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.TangentPoints (GeoPoint2D, GeoPoint2D)"/>
        /// </summary>
        /// <param name="FromHere"></param>
        /// <param name="CloseTo"></param>
        /// <returns></returns>
        public override GeoPoint2D[] TangentPoints(GeoPoint2D FromHere, GeoPoint2D CloseTo)
        {
            return new GeoPoint2D[0];
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.TangentPointsToAngle (Angle, GeoPoint2D)"/>
        /// </summary>
        /// <param name="ang"></param>
        /// <param name="CloseTo"></param>
        /// <returns></returns>
        public override GeoPoint2D[] TangentPointsToAngle(Angle ang, GeoPoint2D CloseTo)
        {
            return new GeoPoint2D[0];
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Trim (double, double)"/>
        /// </summary>
        /// <param name="StartPos"></param>
        /// <param name="EndPos"></param>
        /// <returns></returns>
        public override ICurve2D Trim(double StartPos, double EndPos)
        {
            if ((EndPos - StartPos) < (1.0 / vertex.Length))
            {
                if (Precision.IsEqual(PointAt(StartPos), PointAt(EndPos)))
                {
                    return null;
                }
            }

            Polyline2D res = this.Clone() as Polyline2D;
            try
            {
                res.TrimOff(StartPos, EndPos);
                return res;
            }
            catch (Polyline2DException)
            {	// ein zu kurzes Stück wird dort nicht akzeptiert
                return null;
            }
        }
        private void TrimOff(double StartPos, double EndPos)
        {
            if (StartPos == 0.0 && EndPos == 0.0) return; // nichts wird abgeschnitten
            if (StartPos == 1.0 && EndPos == 1.0) return;

            bool beginning = (StartPos == 0.0 || (StartPos < 1.0 / vertex.Length && Precision.IsEqual(StartPoint, PointAt(StartPos))));
            bool ending = (EndPos == 1.0 || (EndPos > (1.0 - (1.0 / vertex.Length)) && Precision.IsEqual(EndPoint, PointAt(EndPos))));
            if (beginning && ending) return;
            if (beginning)
            {
                ICurve2D[] splt = Split(EndPos);
                this.vertex = (GeoPoint2D[])(splt[0] as Polyline2D).vertex.Clone();
                this.length = -1;
            }
            else if (ending)
            {
                ICurve2D[] splt = Split(StartPos);
                this.vertex = (GeoPoint2D[])(splt[1] as Polyline2D).vertex.Clone();
                this.length = -1;
            }
            else
            {
                GeoPoint2D endpoint = PointAt(EndPos);
                TrimOff(StartPos, 1.0);
                TrimOff(0.0, PositionOf(endpoint));
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Approximate (bool, double)"/>
        /// </summary>
        /// <param name="linesOnly"></param>
        /// <param name="maxError"></param>
        /// <returns></returns>
        public override ICurve2D Approximate(bool linesOnly, double maxError)
        {
            return Clone();
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.GetArea ()"/>
        /// </summary>
        /// <returns></returns>
        public override double GetArea()
        {
            double res = 0.0;
            GeoPoint2D startPoint = vertex[0];
            for (int i = 1; i < vertex.Length; i++)
            {
                GeoPoint2D endPoint = vertex[i];
                res += startPoint.x * endPoint.y - startPoint.y * endPoint.x;
                startPoint = endPoint;
            }
            return res / 2.0;
        }
        public override ICurve2D GetFused(ICurve2D toFuseWith, double precision)
        {
            if (toFuseWith is Line2D)
            {
                toFuseWith = new Polyline2D(new GeoPoint2D[] { toFuseWith.StartPoint, toFuseWith.EndPoint });
            }
            if (toFuseWith is Polyline2D)
            {
                Polyline2D pl1, pl2;
                if (length < toFuseWith.Length)
                {
                    pl1 = toFuseWith.Clone() as Polyline2D; // 1. die längere
                    pl2 = this.Clone() as Polyline2D;
                }
                else
                {
                    pl1 = this.Clone() as Polyline2D;
                    pl2 = toFuseWith.Clone() as Polyline2D;
                }
                // die kürzere muss wenigstens start oder endpunkt auf der längeren haben
                double pos1 = pl1.PositionOf(pl2.StartPoint);
                double pos2 = pl1.PositionOf(pl2.EndPoint);
                bool pos1inside = false;
                bool pos2inside = false;
                if (pos1 >= 0.0 && pos1 <= 1.0)
                {   // Startpunkt liegt drauf
                    if (Math.Abs(pl1.Distance(pl2.StartPoint)) < precision) pos1inside = true;
                }
                if (pos2 >= 0.0 && pos2 <= 1.0)
                {   // Startpunkt liegt drauf
                    if (Math.Abs(pl1.Distance(pl2.EndPoint)) < precision) pos2inside = true;
                }
                GeoPoint2D split1 = GeoPoint2D.Origin, split2 = GeoPoint2D.Origin;
                bool split = false;
                if (pos1inside && pos2inside)
                {
                    bool ok = true;
                    for (int i = 0; i < pl2.VertexCount; i++)
                    {
                        if (Math.Abs(pl1.Distance(pl2.vertex[i])) > precision) ok = false;
                    }
                    if (ok) return pl1; // die ist es bereits, und es ist ja auch ein clone
                    else return null; // zu großer Abstand
                }
                else if (pos2inside)
                {   // der Endpunkt von pl2 liegt innerhalb, der Startpunkt außerhalb
                    double p = pl2.PositionOf(pl1.StartPoint);
                    if (p >= 0.0 && p <= 1.0)
                    {
                        if (Math.Abs(pl2.Distance(pl1.StartPoint)) < precision)
                        {
                            // fängt an mit pl2, endet mid pl1, beide schon richtige Richtung
                            split1 = pl1.StartPoint;
                            split2 = pl2.EndPoint;
                            split = true;
                            Polyline2D tmp = pl2;
                            pl2 = pl1;
                            pl1 = tmp;

                        }
                    }
                    if (!split)
                    {
                        p = pl2.PositionOf(pl1.EndPoint);
                        if (p >= 0.0 && p <= 1.0)
                        {
                            if (Math.Abs(pl2.Distance(pl1.EndPoint)) < precision)
                            {
                                // fängt an mit pl2, endet mid pl1, pl1 muss umgedreht werden
                                split1 = pl1.EndPoint;
                                split2 = pl2.EndPoint;
                                split = true;
                                pl1.Reverse();
                                Polyline2D tmp = pl2;
                                pl2 = pl1;
                                pl1 = tmp;

                            }
                        }
                    }
                }
                else if (pos1inside)
                {   // der Startpunkt von pl2 liegt innerhalb, der Endpunkt außerhalb
                    double p = pl2.PositionOf(pl1.StartPoint);
                    if (p >= 0.0 && p <= 1.0)
                    {
                        if (Math.Abs(pl2.Distance(pl1.StartPoint)) < precision)
                        {
                            // fängt an mit pl1, endet mid pl2, pl1 falschrum
                            split1 = pl2.StartPoint;
                            split2 = pl1.StartPoint;
                            split = true;
                            pl1.Reverse();

                        }
                    }
                    if (!split)
                    {
                        p = pl2.PositionOf(pl1.EndPoint);
                        if (p >= 0.0 && p <= 1.0)
                        {
                            if (Math.Abs(pl2.Distance(pl1.EndPoint)) < precision)
                            {
                                // fängt an mit pl1, endet mid pl2, beide richtigrum
                                split1 = pl2.StartPoint;
                                split2 = pl1.EndPoint;
                                split = true;
                            }
                        }
                    }
                }
                if (split)
                {   // sie überlappen sich, das kann im Extramfall acuh einfach zusammenhängend sein
#if DEBUG
                    DebuggerContainer dc = new DebuggerContainer();
                    dc.Add(pl1, Color.Red, 1);
                    dc.Add(pl2, Color.Green, 2);
                    dc.Add(split1, Color.Red, 3);
                    dc.Add(split2, Color.Blue, 4);
#endif
                    // if (pl1.ConnectWith(pl2, precision)) return pl1; // damit ist das schon erledigt, sonst gibt es beim Splitten einen Fehler
                    // das macht Probleme bei zwei fast identischichen Polylines, die werden sonst in Gegenrichtung miteinander verbunden
                    ICurve2D[] spl1 = pl1.Split(pl1.PositionOf(split1));
                    ICurve2D[] spl2 = pl2.Split(pl2.PositionOf(split2));
                    // Der Anfang von spl1 und das Ende von spl2 sind die Endstücke, das Mittelteil ist doppelt und muss identisch sein
                    if (spl1.Length == 2 && spl2.Length == 2)
                    {
                        Polyline2D m1 = spl1[1] as Polyline2D;
                        Polyline2D m2 = spl2[0] as Polyline2D;
                        bool ok = true;
                        for (int i = 0; i < m1.VertexCount; i++)
                        {
                            if (Math.Abs(m2.Distance(m1.vertex[i])) > precision) ok = false;
                        }
                        if (!ok) return null;
                        List<GeoPoint2D> pnts = new List<GeoPoint2D>((spl1[0] as Polyline2D).Vertex);
                        for (int i = 1; i < m2.VertexCount; i++)
                        {
                            pnts.Add(m2.vertex[i]);
                        }
                        m2 = spl2[1] as Polyline2D;
                        for (int i = 1; i < m2.VertexCount; i++)
                        {
                            pnts.Add(m2.vertex[i]);
                        }
                        return new Polyline2D(pnts.ToArray());
                    }
                }
            }
            return null;
        }
        public ICurve2D[] GetSubCurves()
        {
            Line2D[] subcurves = new Line2D[vertex.Length - 1];
            for (int i = 1; i < vertex.Length; ++i)
            {
                GeoPoint2D sp = vertex[i - 1];
                GeoPoint2D ep = vertex[i];
                subcurves[i - 1] = new Line2D(sp, ep);
            }
            return subcurves;
        }
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected Polyline2D(SerializationInfo info, StreamingContext context)
        {
            vertex = (GeoPoint2D[])info.GetValue("Vertex", typeof(GeoPoint2D[]));
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Vertex", vertex, typeof(GeoPoint2D[]));
        }

        #endregion

        internal bool ConnectWith(Line2D line2D, double Precision)
        {
            if ((line2D.StartPoint | vertex[vertex.Length - 1]) < Precision)
            {
                GeoPoint2D[] newvert = new GeoPoint2D[vertex.Length + 1];
                Array.Copy(vertex, newvert, vertex.Length);
                newvert[vertex.Length] = line2D.EndPoint;
                vertex = newvert;
                this.length = -1;
                return true;
            }
            if ((line2D.EndPoint | vertex[vertex.Length - 1]) < Precision)
            {
                GeoPoint2D[] newvert = new GeoPoint2D[vertex.Length + 1];
                Array.Copy(vertex, newvert, vertex.Length);
                newvert[vertex.Length] = line2D.StartPoint;
                vertex = newvert;
                this.length = -1;
                return true;
            }
            if ((line2D.StartPoint | vertex[0]) < Precision)
            {
                GeoPoint2D[] newvert = new GeoPoint2D[vertex.Length + 1];
                Array.Copy(vertex, 0, newvert, 1, vertex.Length);
                newvert[0] = line2D.EndPoint;
                vertex = newvert;
                this.length = -1;
                return true;
            }
            if ((line2D.EndPoint | vertex[0]) < Precision)
            {
                GeoPoint2D[] newvert = new GeoPoint2D[vertex.Length + 1];
                Array.Copy(vertex, 0, newvert, 1, vertex.Length);
                newvert[0] = line2D.StartPoint;
                vertex = newvert;
                this.length = -1;
                return true;
            }
            return false;
        }

        internal bool ConnectWith(Polyline2D polyline2D, double Precision)
        {
            double minDist = double.MaxValue;
            int mode = -1;
            double dist = polyline2D.StartPoint | this.EndPoint;
            if (dist < minDist)
            {
                minDist = dist;
                mode = 0;
            }
            dist = polyline2D.EndPoint | this.EndPoint;
            if (dist < minDist)
            {
                minDist = dist;
                mode = 1;
            }
            dist = polyline2D.StartPoint | this.StartPoint;
            if (dist < minDist)
            {
                minDist = dist;
                mode = 2;
            }
            dist = polyline2D.EndPoint | this.StartPoint;
            if (dist < minDist)
            {
                minDist = dist;
                mode = 3;
            }
            if (minDist < Precision)
            {
                GeoPoint2D[] newvert;
                switch (mode)
                {
                    case 0:
                        newvert = new GeoPoint2D[vertex.Length + polyline2D.VertexCount - 1];
                        Array.Copy(vertex, newvert, vertex.Length);
                        Array.Copy(polyline2D.vertex, 1, newvert, vertex.Length, polyline2D.VertexCount - 1);
                        vertex = newvert;
                        this.length = -1;
                        return true;
                    case 1:
                        newvert = new GeoPoint2D[vertex.Length + polyline2D.VertexCount - 1];
                        Array.Copy(vertex, newvert, vertex.Length);
                        GeoPoint2D[] old = polyline2D.Vertex; // das ist eine Kopie
                        Array.Reverse(old);
                        Array.Copy(old, 1, newvert, vertex.Length, polyline2D.VertexCount - 1);
                        vertex = newvert;
                        this.length = -1;
                        return true;
                    case 2:
                        this.Reverse();
                        return ConnectWith(polyline2D, Precision);
                    case 3:
                        this.Reverse();
                        return ConnectWith(polyline2D, Precision);
                }
            }
            //if ((polyline2D.StartPoint | vertex[vertex.Length - 1]) < Precision)
            //{
            //    GeoPoint2D[] newvert = new GeoPoint2D[vertex.Length + polyline2D.VertexCount - 1];
            //    Array.Copy(vertex, newvert, vertex.Length);
            //    Array.Copy(polyline2D.vertex, 1, newvert, vertex.Length, polyline2D.VertexCount - 1);
            //    vertex = newvert;
            //    this.length = -1;
            //    return true;
            //}
            //if ((polyline2D.EndPoint | vertex[vertex.Length - 1]) < Precision)
            //{
            //    GeoPoint2D[] newvert = new GeoPoint2D[vertex.Length + polyline2D.VertexCount - 1];
            //    Array.Copy(vertex, newvert, vertex.Length);
            //    GeoPoint2D[] old = polyline2D.Vertex; // das ist eine Kopie
            //    Array.Reverse(old);
            //    Array.Copy(old, 1, newvert, vertex.Length, polyline2D.VertexCount - 1);
            //    vertex = newvert;
            //    this.length = -1;
            //    return true;
            //}
            //// wenn die zuzufügende vornedran käme, dann halt umdrehen
            //if ((polyline2D.StartPoint | vertex[0]) < Precision)
            //{
            //    this.Reverse();
            //    return ConnectWith(polyline2D, Precision);
            //}
            //if ((polyline2D.EndPoint | vertex[0]) < Precision)
            //{
            //    this.Reverse();
            //    return ConnectWith(polyline2D, Precision);
            //}
            return false;
        }

        internal bool Reduce(double precision)
        {
            // zuerst die Möglichkeit checken, dass die ganze Kurve in sich zurückgeht:
            int splitat = -1;
            double maxBend = 0;
            for (int i = 2; i < vertex.Length; i++)
            {
                SweepAngle sw = new SweepAngle(vertex[i - 1] - vertex[i - 2], vertex[i] - vertex[i - 1]);
                if (Math.Abs(sw.Radian) > Math.PI - 0.1)
                {
                    if (Math.Abs(sw.Radian) > maxBend)
                    {
                        maxBend = Math.Abs(sw.Radian);
                        splitat = i - 1;
                    }
                }
            }
            if (splitat >= 0)
            {
                GeoPoint2D[] v1 = new GeoPoint2D[splitat + 1];
                GeoPoint2D[] v2 = new GeoPoint2D[vertex.Length - splitat];
                Array.Copy(vertex, v1, splitat + 1);
                Array.Copy(vertex, splitat, v2, 0, vertex.Length - splitat);
                Polyline2D pl1 = Polyline2D.MakePolyline2D(v1);
                Polyline2D pl2 = Polyline2D.MakePolyline2D(v2);
                if (pl1 != null && pl2 != null)
                {
                    ICurve2D fused = pl1.GetFused(pl2, precision);
                    if (fused != null && (fused as Polyline2D).VertexCount < vertex.Length)
                    {
                        this.SetVertices((fused as Polyline2D).Vertex);
                        return Reduce(precision); // sollte nicht rekursiv werden, es sei denn, es gibt mehrere Spitzkehren, dann ist es ja gut
                    }
                }
            }

            List<GeoPoint2D> vlist = new List<GeoPoint2D>();
            vlist.Add(vertex[0]);
            int lastInserted = 0;
            bool reverts = false;
            for (int i = 2; i < vertex.Length; i++)
            {
                bool inserted = true;
                while (inserted)
                {
                    inserted = false;
                    for (int j = lastInserted + 1; j < i; j++)
                    {
                        double lp = Geometry.LinePar(vertex[lastInserted], vertex[i], vertex[j]); // Problem: die Polyline geht in sich selbst zurück
                        if (Math.Abs(Geometry.DistPL(vertex[j], vertex[lastInserted], vertex[i])) > precision || lp < 0 || lp > 1)
                        {
                            if (lp < 0 || lp > 1) reverts = true;
                            lastInserted = j;
                            vlist.Add(vertex[j]);
                            inserted = true;
                            break;
                        }
                    }
                }
            }
            vlist.Add(vertex[vertex.Length - 1]);
            if (reverts && vlist.Count >= 3)
            {   // versuche wenigstens am Anfang oder Ende die Situation, dass die Polyline in sich zurückgeht zu bereinigen
                double lp = Geometry.LinePar(vlist[vlist.Count - 3], vlist[vlist.Count - 2], vlist[vlist.Count - 1]); // der letzte Punkt im vorletzten Segment
                if (lp >= 0 && lp <= 1 && Math.Abs(Geometry.DistPL(vlist[vlist.Count - 1], vlist[vlist.Count - 2], vlist[vlist.Count - 3])) < precision)
                {
                    vlist.RemoveAt(vlist.Count - 1);
                }
                if (vlist.Count >= 3)
                {
                    lp = Geometry.LinePar(vlist[2], vlist[1], vlist[0]); // der letzte Punkt im vorletzten Segment
                    if (lp >= 0 && lp <= 1 && Math.Abs(Geometry.DistPL(vlist[0], vlist[1], vlist[2])) < precision)
                    {
                        vlist.RemoveAt(0);
                    }
                }
            }
            if (vlist.Count < vertex.Length)
            {
                SetVertices(vlist.ToArray());
                return true;
            }
            return false;
        }

#if DEBUG
        GeoObjectList Debug
        {
            get
            {
                return new GeoObjectList(MakeGeoObject(Plane.XYPlane));
            }
        }
#endif
    }

}
