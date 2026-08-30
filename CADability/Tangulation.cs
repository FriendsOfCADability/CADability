using CADability.Curve2D;
using CADability.GeoObject;
using System;
using System.Collections.Generic;

namespace CADability
{
    /// <summary>
    /// Tangentiale Triangulierung:
    /// Am gegebenen Rand und ggf. an weiteren inneren Stellen werden tangentiale Ebenen aufgespannt und miteinander verschnitten. So entstehen (konvexe) Polygone,
    /// die zusammen die Fläche triangulieren
    /// </summary>
    class Tangulation
    {
        private class PlanePolygon : IOctTreeInsertable
        {
            Plane plane; // die Ebene, gegeben durch Mittelpunkt, welche gleichzeitig berührpunkt ist
            GeoPoint2D uvCenter; // hier berührt die Ebene die Fläche (in surface Koordinaten)
            List<GeoPoint2D> polygon; // das offene oder geschlossene Polygon in plane Koordinaten, welches das Flächenstück beschreibt
            bool isOpen; // wenn true, dann ist das Polygon offen und der 1. Punkt nach rückwärts und der letzte Punkt nach vorwärts beliebig verlängerbar
            ModOp2D toUV; // Umwandlung vom Ebenen Koordinatensystem in das tangentiale surface uv-System
            Tangulation tangulation; // Zeigt nach außen
            BoundingBox extend;

            public PlanePolygon(GeoPoint2D uv, GeoPoint loc, GeoVector diru, GeoVector dirv, GeoPoint edgeStart, GeoPoint edgeEnd, Tangulation tangulation)
            {
                // TODO: Complete member initialization
                plane = new Plane(loc, diru, dirv);
                toUV = ModOp2D.Translate(uv.x, uv.y) * ModOp2D.Fit(new GeoVector2D[] { GeoVector2D.XAxis, GeoVector2D.YAxis }, new GeoVector2D[] { plane.Project(diru), plane.Project(dirv) });
                polygon = new List<GeoPoint2D>();
                polygon.Add(plane.Project(edgeStart)); // die beiden Punkte geben die Linie an, die die Ebene begrenzt
                polygon.Add(plane.Project(edgeEnd));
                isOpen = true; // die Linie "halbiert" die Ebene, links davon ist innerhalb
                extend = BoundingBox.EmptyBoundingCube;
            }

            BoundingBox IOctTreeInsertable.GetExtent(double precision)
            {
                if (isOpen) return tangulation.octtree.Extend; // nicht endlich, also alles
                if (extend.IsEmpty)
                {
                    for (int i = 0; i < polygon.Count; i++)
                    {
                        extend.MinMax(plane.ToGlobal(polygon[i]));
                    }
                }
                return extend;
            }

            bool IOctTreeInsertable.HitTest(ref BoundingBox cube, double precision)
            {
                throw new NotImplementedException();
            }

            bool IOctTreeInsertable.HitTest(Projection projection, BoundingRect rect, bool onlyInside)
            {   // kommt nicht dran
                throw new NotImplementedException();
            }

            bool IOctTreeInsertable.HitTest(Projection.PickArea area, bool onlyInside)
            {   // kommt nicht dran
                throw new NotImplementedException();
            }

            double IOctTreeInsertable.Position(GeoPoint fromHere, GeoVector direction, double precision)
            {   // kommt nicht dran
                throw new NotImplementedException();
            }
        }


        OctTree<PlanePolygon> octtree;
        BoundingBox extend;

        public Tangulation(GeoPoint2D[][] points, ISurface surface, double maxDeflection, Angle maxBending)
        {
#if DEBUG
            // DEBUG:
            DebuggerContainer dc = new DebuggerContainer();
            for (int i = 0; i < points.Length; ++i)
            {
                for (int j = 0; j < points[i].Length - 1; ++j)
                {
                    Line2D l2d = new Line2D(points[i][j], points[i][j + 1]);
                    dc.Add(l2d, System.Drawing.Color.Red, j);
                }
            }
#endif
            for (int i = 0; i < points.Length; ++i)
            {
                GeoPoint lastPoint, firstPoint;
                lastPoint = firstPoint = surface.PointAt(points[i][0]);
                for (int j = 0; j < points[i].Length; ++j)
                {
                    GeoPoint nextPoint;
                    GeoPoint2D nextUVPoint;
                    if (j < points[i].Length - 1)
                    {
                        nextUVPoint = points[i][j + 1];
                        nextPoint = surface.PointAt(nextUVPoint);
                    }
                    else
                    {
                        nextUVPoint = points[i][0];
                        nextPoint = firstPoint;
                    }
                    GeoPoint2D uvMiddle = new GeoPoint2D(points[i][j], nextUVPoint);
                    GeoPoint loc;
                    GeoVector diru, dirv;
                    surface.DerivationAt(uvMiddle, out loc, out diru, out dirv);
                    PlanePolygon pp = new PlanePolygon(uvMiddle, loc, diru, dirv, lastPoint, nextPoint, this);
                }
            }
        }
    }
}
