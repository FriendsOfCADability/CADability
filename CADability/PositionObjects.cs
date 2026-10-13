using CADability.GeoObject;
using System;
using System.Collections;


namespace CADability
{
    /// <summary>
    /// Static helper methods that align and distribute a list of objects as seen in a <see cref="Projection"/>:
    /// align the left, right, top or bottom edges or the centers of their 2d extents to the last object of the
    /// list, center them on the origin or on a page, space them evenly in horizontal or vertical direction,
    /// or scale them to the same width or height. The objects are moved parallel to the drawing plane of
    /// the projection and each call is one undo step. Used by the align commands of
    /// <see cref="CADability.Actions.SelectObjectsAction"/>.
    /// </summary>
    internal class PositionObjects
    {
        public PositionObjects()
        {
            // 
            // TODO: Add constructor logic here
            //
        }

        private class CompareGeoObject : IComparer
        {	// lokale Klasse, wird gebraucht zum Sortieren in SpaceAcross und SpaceDown
            private bool dirx;
            private Projection projection;

            public CompareGeoObject(bool dirx, Projection projection)
            {	// Parameter des Constructors merken für diese Instanz
                this.dirx = dirx;
                this.projection = projection;
            }
            #region IComparer Members

            public int Compare(object x, object y)
            {	// Typ-Casting
                IGeoObject o1 = (IGeoObject)x;
                IGeoObject o2 = (IGeoObject)y;
                // Sortierung nach Mittelpunkten
                BoundingRect re = IGeoObjectImpl.GetExtent(o1, projection, false);
                GeoPoint2D p1 = re.GetCenter();
                re = IGeoObjectImpl.GetExtent(o2, projection, false);
                GeoPoint2D p2 = re.GetCenter();
                if (dirx)
                {
                    if (p1.x < p2.x) return -1;
                    if (p1.x > p2.x) return 1;
                }
                else
                {
                    if (p1.y < p2.y) return -1;
                    if (p1.y > p2.y) return 1;
                }
                return 0;
            }

            #endregion
        }

        static public void AlignLeft(GeoObjectList gl, Projection projection, Project pr)
        {
            using (pr.Undo.UndoFrame)
            {
                BoundingRect re1 = IGeoObjectImpl.GetExtent(gl[gl.Count - 1], projection, false);
                for (int i = 0; i < gl.Count - 1; ++i)
                {
                    BoundingRect re = IGeoObjectImpl.GetExtent(gl[i], projection, false);
                    GeoVector2D trans2D = new GeoVector2D(re1.Left - re.Left, 0);
                    GeoVector trans = projection.DrawingPlane.ToGlobal(trans2D);
                    ModOp m = ModOp.Translate(trans);
                    gl[i].Modify(m);
                }
            }
        }

        static public void AlignHcenter(GeoObjectList gl, Projection projection, Project pr)
        {
            using (pr.Undo.UndoFrame)
            {
                BoundingRect re1 = IGeoObjectImpl.GetExtent(gl[gl.Count - 1], projection, false);
                double center1 = (re1.Left + re1.Right) / 2;
                if (gl.Count == 1) center1 = 0.0; // um den Ursprung, wenns nur eines ist
                for (int i = 0; i < Math.Max(1, gl.Count - 1); ++i)
                {
                    BoundingRect re = IGeoObjectImpl.GetExtent(gl[i], projection, false);
                    GeoVector2D trans2D = new GeoVector2D(center1 - (re.Left + re.Right) / 2, 0);
                    GeoVector trans = projection.DrawingPlane.ToGlobal(trans2D);
                    ModOp m = ModOp.Translate(trans);
                    gl[i].Modify(m);
                }
            }
        }

        static public void AlignRight(GeoObjectList gl, Projection projection, Project pr)
        {
            using (pr.Undo.UndoFrame)
            {
                BoundingRect re1 = IGeoObjectImpl.GetExtent(gl[gl.Count - 1], projection, false);
                for (int i = 0; i < gl.Count - 1; ++i)
                {
                    BoundingRect re = IGeoObjectImpl.GetExtent(gl[i], projection, false);
                    GeoVector2D trans2D = new GeoVector2D(re1.Right - re.Right, 0);
                    GeoVector trans = projection.DrawingPlane.ToGlobal(trans2D);
                    ModOp m = ModOp.Translate(trans);
                    gl[i].Modify(m);
                }
            }
        }

        static public void AlignTop(GeoObjectList gl, Projection projection, Project pr)
        {
            using (pr.Undo.UndoFrame)
            {
                BoundingRect re1 = IGeoObjectImpl.GetExtent(gl[gl.Count - 1], projection, false);
                for (int i = 0; i < gl.Count - 1; ++i)
                {
                    BoundingRect re = IGeoObjectImpl.GetExtent(gl[i], projection, false);
                    GeoVector2D trans2D = new GeoVector2D(0, re1.Top - re.Top);
                    GeoVector trans = projection.DrawingPlane.ToGlobal(trans2D);
                    ModOp m = ModOp.Translate(trans);
                    gl[i].Modify(m);
                }
            }
        }
        static public void AlignCenter(GeoObjectList gl, Projection projection, Project pr)
        {   // nur zum Nullpunkt zentieren
            using (pr.Undo.UndoFrame)
            {
                for (int i = 0; i < gl.Count; ++i)
                {
                    BoundingRect re = IGeoObjectImpl.GetExtent(gl[i], projection, false);
                    GeoVector2D trans2D = new GeoVector2D(-(re.Left + re.Right) / 2, -(re.Bottom + re.Top) / 2);
                    GeoVector trans = projection.DrawingPlane.ToGlobal(trans2D);
                    ModOp m = ModOp.Translate(trans);
                    gl[i].Modify(m);
                }
            }
        }
        static public void AlignVcenter(GeoObjectList gl, Projection projection, Project pr)
        {
            using (pr.Undo.UndoFrame)
            {
                BoundingRect re1 = IGeoObjectImpl.GetExtent(gl[gl.Count - 1], projection, false);
                double center1 = (re1.Bottom + re1.Top) / 2;
                if (gl.Count == 1) center1 = 0.0; // um den Ursprung
                for (int i = 0; i < Math.Max(1, gl.Count - 1); ++i)
                {
                    BoundingRect re = IGeoObjectImpl.GetExtent(gl[i], projection, false);
                    GeoVector2D trans2D = new GeoVector2D(0, center1 - (re.Bottom + re.Top) / 2);
                    GeoVector trans = projection.DrawingPlane.ToGlobal(trans2D);
                    ModOp m = ModOp.Translate(trans);
                    gl[i].Modify(m);
                }
            }
        }

        static public void AlignPageHCenter(GeoObjectList gl, double width, Project pr)
        {
            using (pr.Undo.UndoFrame)
            {   // nur in der Draufsicht, sonst macht das keinen Sinn
                BoundingRect re = gl.GetExtent(Projection.FromTop, false, false);
                GeoVector2D trans2D = new GeoVector2D(width / 2.0 - (re.Left + re.Right) / 2, 0);
                GeoVector trans = Plane.XYPlane.ToGlobal(trans2D);
                ModOp m = ModOp.Translate(trans);
                gl.Modify(m);
            }
        }
        static public void AlignPageVCenter(GeoObjectList gl, double height, Project pr)
        {
            using (pr.Undo.UndoFrame)
            {   // nur in der Draufsicht, sonst macht das keinen Sinn
                BoundingRect re = gl.GetExtent(Projection.FromTop, false, false);
                GeoVector2D trans2D = new GeoVector2D(0, height / 2.0 - (re.Bottom + re.Top) / 2);
                GeoVector trans = Plane.XYPlane.ToGlobal(trans2D);
                ModOp m = ModOp.Translate(trans);
                gl.Modify(m);
            }
        }
        static public void AlignPageCenter(GeoObjectList gl, double width, double height, Project pr)
        {
            using (pr.Undo.UndoFrame)
            {   // nur in der Draufsicht, sonst macht das keinen Sinn
                BoundingRect re = gl.GetExtent(Projection.FromTop, false, false);
                GeoVector2D trans2D = new GeoVector2D(width / 2.0 - (re.Bottom + re.Top) / 2, height / 2.0 - (re.Bottom + re.Top) / 2);
                GeoVector trans = Plane.XYPlane.ToGlobal(trans2D);
                ModOp m = ModOp.Translate(trans);
                gl.Modify(m);
            }
        }

        static public void AlignBottom(GeoObjectList gl, Projection projection, Project pr)
        {
            using (pr.Undo.UndoFrame)
            {
                BoundingRect re1 = IGeoObjectImpl.GetExtent(gl[gl.Count - 1], projection, false);
                for (int i = 0; i < gl.Count - 1; ++i)
                {
                    BoundingRect re = IGeoObjectImpl.GetExtent(gl[i], projection, false);
                    GeoVector2D trans2D = new GeoVector2D(0, re1.Bottom - re.Bottom);
                    GeoVector trans = projection.DrawingPlane.ToGlobal(trans2D);
                    ModOp m = ModOp.Translate(trans);
                    gl[i].Modify(m);
                }
            }
        }
        static public void SpaceAcross(GeoObjectList gl, Projection projection, Project pr)
        {
            using (pr.Undo.UndoFrame)
            {
                IGeoObject[] geoArray = new IGeoObject[gl.Count];
                for (int i = 0; i < gl.Count; ++i) // umkopieren auf ein Array, um den Sort machen zu können
                { geoArray[i] = gl[i]; }
                // hier nun Sort, der Compare steht oben als lokal class und braucht die Parameter: Sort nach x = true und pm
                Array.Sort(geoArray, 0, geoArray.Length, new CompareGeoObject(true, projection));
                // die Rechtecke des ersten und letzten Objeks für die Gesamtausdehnung
                BoundingRect reStart = IGeoObjectImpl.GetExtent(geoArray[0], projection, false);
                BoundingRect reEnd = IGeoObjectImpl.GetExtent(geoArray[geoArray.Length - 1], projection, false);
                double reTotal = reEnd.Right - reStart.Left; // Gesamtausdehnung
                double distRe = 0;
                for (int i = 0; i < geoArray.Length; ++i) // Summe der Ausdehnung der Einzelnen:
                {
                    BoundingRect re = IGeoObjectImpl.GetExtent(geoArray[i], projection, false);
                    distRe = distRe + re.Width;
                }
                double space = (reTotal - distRe) / (geoArray.Length - 1); // Gesamt - Summe Einzelne / Zwischenräume
                double pos = reStart.Right;
                for (int i = 1; i < geoArray.Length - 1; ++i) // vom zweiten bis zum vorletzten, die Äußeren bleiben unverändert
                {
                    BoundingRect re = IGeoObjectImpl.GetExtent(geoArray[i], projection, false);
                    GeoVector2D trans2D = new GeoVector2D(pos + space - re.Left, 0);
                    pos = pos + space + re.Width; // pos hochzählen auf den rechten Rand des aktuellen
                    GeoVector trans = projection.DrawingPlane.ToGlobal(trans2D);
                    ModOp m = ModOp.Translate(trans);
                    geoArray[i].Modify(m);
                }
            }
        }
        static public void SpaceDown(GeoObjectList gl, Projection projection, Project pr)
        {
            using (pr.Undo.UndoFrame)
            {
                IGeoObject[] geoArray = new IGeoObject[gl.Count];
                for (int i = 0; i < gl.Count; ++i) // umkopieren auf ein Array, um den Sort machen zu können
                { geoArray[i] = gl[i]; }
                // hier nun Sort, der Compare steht oben als lokal class und braucht die Parameter: Sort nach y = false und pm
                Array.Sort(geoArray, 0, geoArray.Length, new CompareGeoObject(false, projection));
                // die Rechtecke des ersten und letzten Objeks für die Gesamtausdehnung
                BoundingRect reStart = IGeoObjectImpl.GetExtent(geoArray[0], projection, false);
                BoundingRect reEnd = IGeoObjectImpl.GetExtent(geoArray[geoArray.Length - 1], projection, false);
                double reTotal = reEnd.Top - reStart.Bottom; // Gesamtausdehnung
                double distRe = 0;
                for (int i = 0; i < geoArray.Length; ++i) // Summe der Ausdehnung der Einzelnen:
                {
                    BoundingRect re = IGeoObjectImpl.GetExtent(geoArray[i], projection, false);
                    distRe = distRe + re.Height;
                }
                double space = (reTotal - distRe) / (geoArray.Length - 1); // Gesamt - Summe Einzelne / Zwischenräume
                double pos = reStart.Top;
                for (int i = 1; i < geoArray.Length - 1; ++i) // vom zweiten bis zum vorletzten, die Äußeren bleiben unverändert
                {
                    BoundingRect re = IGeoObjectImpl.GetExtent(geoArray[i], projection, false);
                    GeoVector2D trans2D = new GeoVector2D(0, pos + space - re.Bottom);
                    pos = pos + space + re.Height; // pos hochzählen auf den oberen Rand des aktuellen
                    GeoVector trans = projection.DrawingPlane.ToGlobal(trans2D);
                    ModOp m = ModOp.Translate(trans);
                    geoArray[i].Modify(m);
                }
            }
        }

        static public void SameWidth(GeoObjectList gl, Projection projection, Project pr)
        {
            using (pr.Undo.UndoFrame)
            {
                BoundingRect re1 = IGeoObjectImpl.GetExtent(gl[gl.Count - 1], projection, false);
                for (int i = 0; i < gl.Count - 1; ++i)
                {
                    BoundingRect re = IGeoObjectImpl.GetExtent(gl[i], projection, false);
                    double wid = 1;
                    if (re.Width != 0) wid = re1.Width / re.Width;
                    GeoPoint refPkt = projection.DrawingPlane.ToGlobal(re.GetCenter());
                    ModOp m = ModOp.Scale(refPkt, new GeoVector(1.0, 0.0, 0.0), wid);
                    gl[i].Modify(m);
                }
            }
        }
        static public void SameHeight(GeoObjectList gl, Projection projection, Project pr)
        {
            using (pr.Undo.UndoFrame)
            {
                BoundingRect re1 = IGeoObjectImpl.GetExtent(gl[gl.Count - 1], projection, false);
                for (int i = 0; i < gl.Count - 1; ++i)
                {
                    BoundingRect re = IGeoObjectImpl.GetExtent(gl[i], projection, false);
                    double hig = 1;
                    if (re.Height != 0) hig = re1.Height / re.Height;
                    GeoPoint refPkt = projection.DrawingPlane.ToGlobal(re.GetCenter());
                    ModOp m = ModOp.Scale(refPkt, new GeoVector(0.0, 1.0, 0.0), hig);
                    gl[i].Modify(m);
                }
            }
        }
    }
}
