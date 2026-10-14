using System.Collections.Generic;

namespace CADability
{

    /// <summary>
    /// Collects 2d points and removes near duplicates: <see cref="GetReduced"/> returns only those points which are not closer than
    /// the given precision to a point already kept, using a <see cref="QuadTree{T}"/> to find the neighbours. Internal helper,
    /// currently not used by the kernel.
    /// </summary>
    public class GeoPoint2DList
    {
        class GeoPoint2DQt : IQuadTreeInsertable
        {
            public GeoPoint2D p;
            public GeoPoint2DQt(GeoPoint2D p)
            {
                this.p = p;
            }
            #region IQuadTreeInsertable Members

            BoundingRect IQuadTreeInsertable.GetExtent()
            {
                return new BoundingRect(p);
            }

            bool IQuadTreeInsertable.HitTest(ref BoundingRect rect, bool includeControlPoints)
            {
                return rect.Contains(p);
            }

            object IQuadTreeInsertable.ReferencedObject
            {
                get { return null; }
            }

            #endregion
        }
        List<GeoPoint2D> list;
        public GeoPoint2DList()
        {
            list = new List<GeoPoint2D>();
        }
        public void Add(GeoPoint2D p)
        {
            list.Add(p);
        }
        public List<GeoPoint2D> GetReduced(double precision)
        {
            QuadTree<GeoPoint2DQt> qt = new QuadTree<GeoPoint2DQt>();
            for (int i = 0; i < list.Count; i++)
            {
                GeoPoint2DQt p = new GeoPoint2DQt(list[i]);
                GeoPoint2DQt[] close = qt.GetObjectsCloseTo(p);
                bool found = false;
                for (int j = 0; j < close.Length; j++)
                {
                    if ((close[j].p | list[i]) <= precision)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found) qt.AddObject(p);
            }
            GeoPoint2DQt[] all = qt.GetAllObjects();
            List<GeoPoint2D> res = new List<GeoPoint2D>(all.Length);
            for (int i = 0; i < all.Length; i++)
            {
                res.Add(all[i].p);
            }
            return res;
        }
    }
}
