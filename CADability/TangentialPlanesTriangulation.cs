using CADability.GeoObject;
using CADability.Shapes;
using System.Collections.Generic;

namespace CADability
{
#if DEBUG
    public
#else
    internal
#endif
    class TangentialPlanesTriangulation
    {
        class TangentPlane
        {
            private TangentialPlanesTriangulation tpt; // back pointer

            public readonly GeoPoint location; // Berührpunkt zu uv
            public readonly GeoVector udirection, vdirection; // 3d Richtungen von u und v
            public readonly GeoVector normal; // der Normalenvektor
            public readonly GeoPoint2D uv; // Berührpunkt im 2d auf surface

            // die folgenden Listen sind synchron:
            internal List<GeoPoint2D> lineStartPoint; // Liste aller Schnittlinien im System dieser Ebene (location, udirection, vdirection), d.h. location in 2d ist (0,0) 
            internal List<GeoVector2D> lineDirection;
            internal List<TangentPlane> other; // andere Ebene, die zu dieser Schnittlinie gehört
            // bis hierher synchron

            //internal List<GeoPoint2D> polygon; // das konvexe Polygon, welches diese Ebene eingrenzt
            public readonly ModOp toThis;

            public TangentPlane(TangentialPlanesTriangulation tpt, GeoPoint2D uv, GeoPoint location, GeoVector udirection, GeoVector vdirection)
            {
                this.tpt = tpt;
                this.location = location;
                this.udirection = udirection;
                this.vdirection = vdirection;
                this.normal = udirection ^ vdirection;
                this.uv = uv;
                // ModOp geht evtl. auch schneller:
                toThis = ModOp.Fit(location, new GeoVector[] { udirection, vdirection }, GeoPoint.Origin, new GeoVector[] { GeoVector.XAxis, GeoVector.YAxis });

                lineStartPoint = new List<GeoPoint2D>();
                lineDirection = new List<GeoVector2D>();
                other = new List<TangentPlane>();
            }

            internal bool IntersectWith(TangentPlane other, bool onEdge)
            {
                GeoVector common = this.normal ^ other.normal;
                if (common.IsNullVector()) return false;
                GeoVector perp = other.normal ^ common; // Vektor senkrecht zur Schnittrichtung und in anderer Ebene
                GeoPoint oloc = toThis * other.location; // anderer Nullpunkt in diesem System
                GeoVector operp = toThis * perp; // senkrechter Vektor in diesem System
                double l = oloc.z / operp.z;
                GeoPoint ip = oloc + l * operp; // Schnittpunkt in diesem System (z muss 0 sein)
                lineStartPoint.Add(new GeoPoint2D(ip));
                lineDirection.Add(new GeoVector2D(toThis * common));
                this.other.Add(other);
                other.AddLine(new GeoPoint2D(other.toThis * to3d(new GeoPoint2D(ip))), new GeoVector2D(-(other.toThis * common)), this);
                return true;
            }
            internal GeoPoint to3d(GeoPoint2D p)
            {
                return location + p.x * udirection + p.y * vdirection;
            }
            private void AddLine(GeoPoint2D sp, GeoVector2D dir, TangentPlane other)
            {
                this.lineStartPoint.Add(sp);
                this.lineDirection.Add(dir);
                this.other.Add(other);
            }
        }

        GeoPoint2D[][] points;
        ISurface surface;
        double maxDeflection;
        Angle maxBending;

        List<TangentPlane> allPlanes;

        public TangentialPlanesTriangulation(GeoPoint2D[][] points, ISurface surface, double maxDeflection, Angle maxBending)
        {
            this.points = points;
            this.surface = surface;
            this.maxDeflection = maxDeflection;
            this.maxBending = maxBending;
            allPlanes = new List<TangentPlane>();

            for (int i = 0; i < points.Length; i++)
            {
                int first = allPlanes.Count;
                for (int j = 0; j < points[i].Length; j++)
                {
                    GeoPoint location;
                    GeoVector udirection, vdirection;
                    surface.DerivationAt(points[i][j], out location, out udirection, out vdirection);
                    TangentPlane tp = new TangentPlane(this, points[i][j], location, udirection, vdirection);
                    allPlanes.Add(tp);
                    if (j > 0) tp.IntersectWith(allPlanes[first + j - 1], true); // schneiden mit der vorherigen
                }
                allPlanes[allPlanes.Count - 1].IntersectWith(allPlanes[first], true);
            }
        }

        internal GeoObjectList Debug
        {
            get
            {
                GeoObjectList res = new GeoObjectList();
                for (int i = 0; i < allPlanes.Count; i++)
                {
                    TangentPlane tp = allPlanes[i];
                    Face face = Face.MakeFace(new PlaneSurface(tp.location, tp.udirection, tp.vdirection), new SimpleShape(Border.MakeRectangle(new BoundingRect(-1, -1, 1, 1))));
                    res.Add(face);
                    for (int j = 0; j < tp.lineStartPoint.Count; j++)
                    {
                        GeoPoint sp = tp.to3d(tp.lineStartPoint[j]);
                        GeoPoint ep = tp.to3d(tp.lineStartPoint[j] + 10 * tp.lineDirection[j]);
                        Line line = Line.Construct();
                        line.SetTwoPoints(sp, ep);
                        res.Add(line);
                    }
                }
                return res;
            }
        }
    }
}
